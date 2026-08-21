using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Collections;

/// <summary>
/// =sortby($.array, '$.key', 'asc'|'desc') — returns a new array of objects ordered by a key
/// read from each element. This is the reporting workhorse: coverages by premium, claims by
/// date, drivers by age.
///
/// The key path is resolved against <b>the element</b>, not against the document: with
/// <c>'$.premium'</c> each element answers with its own <c>premium</c> property. Only a plain
/// chain of property names is accepted — <c>'$.premium'</c>, <c>'premium'</c>,
/// <c>'$.rating.premium'</c>, and in XML <c>'/premium'</c> — because that is the one shape that
/// means the same thing in every format. A subscript, wildcard, predicate or recursive descent
/// is rejected: the fetchers disagree about what those mean when handed a single element rather
/// than the document root, and a per-format answer is worse than no answer.
///
/// Direction is optional and defaults to <c>'asc'</c>, and the same comparison rule as
/// <c>sort</c> applies to the extracted keys: numeric only when every key is numeric, ordinal
/// string comparison otherwise.
///
/// An element whose key path matches nothing sorts <b>last in both directions</b>. That is a
/// decision, not a fallout of the comparison: a record missing the key it is being ordered by
/// has no place in the order, so it stays out of the way at the end rather than jumping to the
/// front when the direction flips.
///
/// The sort is stable and the input array is never touched — the result is a fresh array of
/// deep clones.
/// </summary>
public class SortBy<TNode> : CollectionFunctionBase<TNode>
{
    public override string FunctionName => "sortby";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count is < 2 or > 3)
        {
            context.LogError(FunctionName,
                $"{FunctionName}() requires an array argument, a key path, and an optional direction ('{Ascending}' or '{Descending}').");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TryResolveElements(Arguments[0], out var elements, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var keyPathResult = Arguments[1].GetValue(currentNode, dataContext, context);
        var keyPath = keyPathResult.Success && keyPathResult.Data.Count > 0
            ? context.NodeAdapter.TryGetString(keyPathResult.Data[0])
            : null;
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            context.LogError(FunctionName, $"{FunctionName}(): the key path argument did not resolve to a string.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TrySplitKeyPath(keyPath, context.ItemsFetcher, out var segments))
        {
            context.LogError(FunctionName,
                $"{FunctionName}(): key path '{keyPath}' is not a plain property chain — it is resolved against each element, so subscripts, wildcards, predicates and recursive descent are not supported.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var descending = false;
        if (Arguments.Count == 3 &&
            !TryReadDirection(Arguments[2], out descending, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var adapter = context.NodeAdapter;
        var keyed = new List<TNode>();
        var keys = new List<TNode>();
        var missing = new List<TNode>();

        foreach (var element in elements)
        {
            if (TryWalk(element, segments, adapter, out var key))
            {
                keyed.Add(element);
                keys.Add(key);
            }
            else
            {
                missing.Add(element);
            }
        }

        var ordered = Order(keyed, keys, descending, adapter);
        ordered.AddRange(missing);

        return FunctionResult<TNode>.Successful(BuildArray(ordered, adapter));
    }
}
