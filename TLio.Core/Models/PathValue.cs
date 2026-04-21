using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// An IFunctionSupportedValue that evaluates a JsonPath expression and returns the
/// first matched node. Used when a script argument is a path reference rather than
/// a literal or function call (e.g. the "from" argument in copy-like operations).
///
/// Returns a failed FunctionResult when the path matches nothing.
/// </summary>
public class PathValue<TNode> : IFunctionSupportedValue<TNode>
{
    private readonly string _path;

    public PathValue(string path) => _path = path;

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var absolutePath = _path.StartsWith(context.ItemsFetcher.CurrentItemPathIndicator)
            ? context.ItemsFetcher.ResolveRelativePath(_path, currentNode, dataContext)
            : _path;
        var resolved = context.ItemsFetcher.ProcessIndirectPath(absolutePath, dataContext) ?? absolutePath;
        var nodes = context.ItemsFetcher.SelectNodes(resolved, dataContext);

        if (nodes.Count == 0)
            return FunctionResult<TNode>.Failed(currentNode);

        return FunctionResult<TNode>.Successful(nodes);
    }

    public string ToScript() => _path;
}
