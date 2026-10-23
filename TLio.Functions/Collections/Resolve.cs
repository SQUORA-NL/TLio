using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions.Logic;

namespace TLio.Functions.Collections;

/// <summary>
/// =resolve(collectionPath, keyField, keyValue [, returnField]) — looks up one element of a
/// collection by key and returns it, or one field of it.
///
/// It exists for the join a mapping script repeats for every field it copies out of a related
/// record: <c>$.parties[?(@.refKey == $.policy.partyRef)].firstName</c>, once per field. The
/// function states that lookup in one place and reads as what it is.
///
///   - <c>collectionPath</c> selects the elements to search. An array's elements
///     (<c>$.party</c>) and a multi-match selection (<c>$.party[*]</c>) both work, and the path
///     is resolved like every other path argument, so <c>@</c>-relative paths apply.
///   - <c>keyField</c> is the literal name of the property compared on each element.
///   - <c>keyValue</c> is the value to find — a literal or a nested call such as
///     <c>fetch($.policy.partyRef)</c>.
///   - <c>returnField</c> (optional) is a path relative to the matched element, in the
///     document's own path language (<c>firstName</c>, <c>address[0].city</c>). Left out, the
///     whole element is returned.
///
/// First match wins, in document order. Keys are compared by <see cref="NodeComparison.AreEqual"/>
/// — the comparison <c>equals</c> uses — so the number 1 matches the text "1". An element that
/// has no <c>keyField</c> is skipped, not an error.
///
/// No match, or a <c>returnField</c> that matches nothing, fails the function (and logs which
/// arguments) exactly as <c>fetch</c> does, so <c>add</c> no-ops and
/// <c>=coalesce(resolve(...), 'x')</c> supplies a default.
///
/// Nothing is cached between calls: a script edits the document between one call and the next,
/// so a cached collection would go stale. The cost is one pass over the collection per call.
///
/// Not to be confused with the ETL <c>resolve</c> <i>command</i>, which writes derived values
/// into every target row; this function fetches one value inside an expression.
/// </summary>
public class ResolveFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "resolve";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count is < 3 or > 4)
        {
            context.LogWarning(FunctionName,
                $"{FunctionName}: expected (collectionPath, keyField, keyValue[, returnField]) — got {Arguments.Count} argument(s).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var adapter = context.NodeAdapter;

        var collectionPath = ReadText(Arguments[0], currentNode, dataContext, context);
        var keyField = ReadText(Arguments[1], currentNode, dataContext, context);
        if (string.IsNullOrEmpty(collectionPath) || string.IsNullOrEmpty(keyField))
        {
            context.LogWarning(FunctionName,
                $"{FunctionName}: collectionPath and keyField must each resolve to text.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        string? returnField = null;
        if (Arguments.Count == 4)
        {
            returnField = ReadText(Arguments[3], currentNode, dataContext, context);
            if (string.IsNullOrEmpty(returnField))
            {
                context.LogWarning(FunctionName, $"{FunctionName}: returnField must resolve to a path.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
        }

        if (!TryResolveNode(Arguments[2], currentNode, dataContext, context, out var key))
        {
            context.LogWarning(FunctionName,
                $"{FunctionName}({collectionPath}, {keyField}, …): the key value resolved to nothing.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var selected = RelativePathResolution.SelectRelative(collectionPath, currentNode, dataContext, context);

        // One matched array stands for its members, so $.party and $.party[*] mean the same.
        IEnumerable<TNode> candidates = selected.Count == 1 && adapter.IsArray(selected[0])
            ? adapter.GetArrayElements(selected[0])
            : selected;

        foreach (var element in candidates)
        {
            if (!adapter.IsObject(element)) continue;

            var value = adapter.GetProperty(element, keyField);
            if (value is null) continue;
            if (!NodeComparison.AreEqual(value, key, adapter)) continue;

            if (returnField is null)
                return FunctionResult<TNode>.Successful(element);

            // A plain chain of names is walked through the adapter: that means the same thing in
            // every format and does not depend on the fetcher telling same-named siblings apart
            // (the slash-path XML fetcher cannot). Anything with a subscript or predicate goes
            // to the format's own path language, anchored on the matched element.
            if (CollectionFunctionBase<TNode>.TrySplitKeyPath(returnField, context.ItemsFetcher, out var segments))
            {
                if (CollectionFunctionBase<TNode>.TryWalk(element, segments, adapter, out var walked))
                    return FunctionResult<TNode>.Successful(walked);
            }
            else
            {
                var fetcher = context.ItemsFetcher;
                var relative = returnField.StartsWith(fetcher.CurrentItemPathIndicator, StringComparison.Ordinal)
                    ? returnField
                    : fetcher.CurrentItemPathIndicator + fetcher.PathDelimiter + returnField;

                var field = RelativePathResolution.SelectRelative(relative, element, dataContext, context);
                if (field.Count > 0)
                    return FunctionResult<TNode>.Successful(field.First!);
            }

            context.LogWarning(FunctionName,
                $"{FunctionName}({collectionPath}, {keyField}, {adapter.TryGetString(key) ?? "…"}, {returnField}): the matched element has no '{returnField}'.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        context.LogWarning(FunctionName,
            $"{FunctionName}({collectionPath}, {keyField}, {adapter.TryGetString(key) ?? "…"}): no element matched.");
        return FunctionResult<TNode>.Failed(currentNode);
    }

    private static string? ReadText(
        IFunctionSupportedValue<TNode> arg, TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0) return null;
        return context.NodeAdapter.TryGetString(result.Data[0]);
    }
}
