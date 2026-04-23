using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// Sentinel IFunctionSupportedValue produced when a script references a function name
/// not in the registry. Logs "Unknown function: name" at execution time and returns a
/// failed result — mirrors the NotFoundCommand pattern for functions.
/// </summary>
public class NotFoundFunctionValue<TNode> : IFunctionSupportedValue<TNode>
{
    private readonly string _functionName;

    public NotFoundFunctionValue(string functionName) => _functionName = functionName;

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        context.LogWarning("FunctionConverter", $"Unknown function: {_functionName}");
        return FunctionResult<TNode>.Failed(currentNode);
    }

    public string ToScript() => $"={_functionName}(?)";
}
