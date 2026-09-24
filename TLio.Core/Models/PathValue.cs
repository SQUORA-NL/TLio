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
        var nodes = RelativePathResolution.SelectRelative(_path, currentNode, dataContext, context);

        if (nodes.Count == 0)
        {
            // Without this the command simply writes nothing and the script looks like it
            // succeeded — a mistyped path has to be visible somewhere.
            context.LogWarning("PathValue",
                $"Path '{_path}' matched no nodes — no value was produced.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        return FunctionResult<TNode>.Successful(nodes);
    }

    public string ToScript() => _path;
}
