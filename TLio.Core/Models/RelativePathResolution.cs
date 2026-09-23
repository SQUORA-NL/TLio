using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// The one rule for turning an already-evaluated path *string* into node(s): a relative path
/// (one starting with the fetcher's current-item indicator, e.g. "@.field") is anchored on
/// <c>currentNode</c> before selection; an absolute path is used as-is; either way, selection
/// itself always runs against <c>dataContext</c>.
///
/// <see cref="PathValue{TNode}"/> applies this rule when a script value *is* a path. Every other
/// place that reads a path out of a string a function argument evaluated to — <c>ResolveArg</c>
/// (<see cref="FunctionBase{TNode}"/>), <c>fetch()</c>, <c>partial()</c>, the text pack's regex
/// argument helper — needs the identical rule, because "@.field" means the same thing wherever it
/// appears in a script. Before this existed, those call sites resolved the string straight
/// against <c>dataContext</c>, so a relative path written as a nested function argument (e.g.
/// <c>=concat(@.a,'-',@.b)</c> inside a decisionTable result, or <c>=fetch(@.to)</c> inside a
/// resolve setting) silently matched the wrong node — or nothing — everywhere except at the
/// document root.
/// </summary>
public static class RelativePathResolution
{
    public static SelectedNodes<TNode> SelectRelative<TNode>(
        string path, TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var absolutePath = path.StartsWith(context.ItemsFetcher.CurrentItemPathIndicator)
            ? context.ItemsFetcher.ResolveRelativePath(path, currentNode, dataContext)
            : path;
        var resolvedPath = context.ItemsFetcher.ProcessIndirectPath(absolutePath, dataContext) ?? absolutePath;
        return context.ItemsFetcher.SelectNodes(resolvedPath, dataContext);
    }
}
