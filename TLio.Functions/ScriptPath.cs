using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =scriptpath() — returns the current node's absolute path as a string node.
/// When called with a relative path argument (@.&lt;-- etc.) that path is resolved
/// first and the resulting node's path is returned.
///
/// Ported from JLio's ScriptPath function.
///
/// =scriptpath(*, kinds, recursive) — a second, unrelated shape: finds every descendant of
/// the current node whose kind is in <c>kinds</c> (an array of <c>'object'</c>, <c>'primitive'</c>
/// and/or <c>'null'</c>, singular or plural, case-insensitive) and returns them as a set of
/// <b>live nodes</b> (not a document array — see <see cref="FunctionResult{TNode}.Successful(TLio.Core.Models.SelectedNodes{TNode})"/>),
/// so a caller can write back into each one directly.
///
/// The first argument is reserved as <c>*</c> for now (every name at every level); arrays are
/// always transparent — walked through when <c>recursive</c> is true, never a match
/// themselves, since they have no name of their own to test. An object that matches is still
/// walked into when recursive: <c>kinds</c> decides what counts as a result, <c>recursive</c>
/// decides how deep the walk goes — the two are independent.
/// </summary>
public class ScriptPath<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "scriptpath";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 3)
            return ExecuteFind(currentNode, dataContext, context);

        TNode targetNode;

        if (Arguments.Count > 0)
        {
            var argResult = Arguments[0].GetValue(currentNode, dataContext, context);
            if (argResult.Success && argResult.Data.First != null)
            {
                // If the argument is written relative to the current node, resolve it and
                // navigate there before reading the path. What marks "relative" is the
                // fetcher's to declare — "@" is that marker only in the JSONPath-shaped
                // languages, and is an attribute reference in XPath.
                var argStr = context.NodeAdapter.TryGetString(argResult.Data.First);
                if (argStr != null &&
                    argStr.StartsWith(context.ItemsFetcher.CurrentItemPathIndicator, StringComparison.Ordinal))
                {
                    var resolvedPath = context.ItemsFetcher.ResolveRelativePath(argStr, currentNode, dataContext);
                    var nodes = context.ItemsFetcher.SelectNodes(resolvedPath, dataContext);
                    targetNode = nodes.Count > 0 ? nodes[0] : currentNode;
                }
                else
                {
                    targetNode = argResult.Data.First;
                }
            }
            else
            {
                targetNode = currentNode;
            }
        }
        else
        {
            targetNode = currentNode;
        }

        var path = context.ItemsFetcher.GetPath(targetNode);
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(path));
    }

    // ── Find mode: =scriptpath(*, kinds, recursive) ─────────────────────────────

    private static readonly HashSet<string> AcceptedKinds =
        new(StringComparer.OrdinalIgnoreCase) { "object", "primitive", "null", "array" };

    private FunctionResult<TNode> ExecuteFind(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var adapter = context.NodeAdapter;

        var nameResult = Arguments[0].GetValue(currentNode, dataContext, context);
        var name = nameResult.Success && nameResult.Data.First != null
            ? adapter.TryGetString(nameResult.Data.First)
            : null;
        if (name != "*")
        {
            context.LogError(FunctionName,
                $"{FunctionName}(name, kinds, recursive): the first argument only supports '*' (every name) currently.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var kindsResult = Arguments[1].GetValue(currentNode, dataContext, context);
        if (!kindsResult.Success || kindsResult.Data.First == null ||
            !TryParseKinds(kindsResult.Data.First, adapter, out var kinds))
        {
            context.LogError(FunctionName,
                $"{FunctionName}(name, kinds, recursive): the second argument must be an array containing " +
                "'object', 'primitive' and/or 'null'.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var recursiveResult = Arguments[2].GetValue(currentNode, dataContext, context);
        var recursive = recursiveResult.Success && recursiveResult.Data.First != null &&
                         adapter.TryGetBoolean(recursiveResult.Data.First) == true;

        var results = new SelectedNodes<TNode>();
        CollectChildren(currentNode, kinds, recursive, adapter, results);
        return FunctionResult<TNode>.Successful(results);
    }

    private static bool TryParseKinds(TNode kindsNode, INodeAdapter<TNode> adapter, out HashSet<string> kinds)
    {
        kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!adapter.IsArray(kindsNode)) return false;

        foreach (var element in adapter.GetArrayElements(kindsNode))
        {
            var text = adapter.TryGetString(element)?.Trim().TrimEnd('s');
            if (text == null || !AcceptedKinds.Contains(text)) return false;
            kinds.Add(text.ToLowerInvariant());
        }

        return kinds.Count > 0;
    }

    /// <summary>Tests and adds every direct child of <paramref name="node"/>, recursing per child.</summary>
    private static void CollectChildren(
        TNode node, HashSet<string> kinds, bool recursive, INodeAdapter<TNode> adapter, SelectedNodes<TNode> results)
    {
        if (adapter.IsObject(node))
        {
            foreach (var name in adapter.GetPropertyNames(node).ToList())
            {
                var child = adapter.GetProperty(node, name);
                if (child is not null)
                    TestAndDescend(child, kinds, recursive, adapter, results);
            }
        }
        else if (adapter.IsArray(node))
        {
            foreach (var child in adapter.GetArrayElements(node).ToList())
                TestAndDescend(child, kinds, recursive, adapter, results);
        }
    }

    /// <summary>
    /// An array is a transparent container — walked through, never itself a match, since it has
    /// no kind of its own to test that <c>kinds</c> could ever select. Everything else is tested
    /// against <c>kinds</c> and, if it is an object and <paramref name="recursive"/> is set,
    /// walked into regardless of whether it matched — a matched object is still a container.
    /// </summary>
    private static void TestAndDescend(
        TNode node, HashSet<string> kinds, bool recursive, INodeAdapter<TNode> adapter, SelectedNodes<TNode> results)
    {
        if (adapter.IsArray(node))
        {
            if (recursive) CollectChildren(node, kinds, recursive, adapter, results);
            return;
        }

        if (Matches(node, kinds, adapter))
            results.Add(node);

        if (recursive && adapter.IsObject(node))
            CollectChildren(node, kinds, recursive, adapter, results);
    }

    private static bool Matches(TNode node, HashSet<string> kinds, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsObject(node)) return kinds.Contains("object");
        if (adapter.IsNull(node)) return kinds.Contains("null");
        return kinds.Contains("primitive");
    }
}
