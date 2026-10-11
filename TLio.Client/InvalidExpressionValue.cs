using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// Sentinel IFunctionSupportedValue produced when an infix expression such as
/// <c>=$.a * $.b</c> cannot be parsed. Logs the reason when the script executes and returns a
/// failed result — the same shape as <see cref="NotFoundFunctionValue{TNode}"/>.
/// </summary>
public class InvalidExpressionValue<TNode> : IFunctionSupportedValue<TNode>
{
    private readonly string _expression;
    private readonly string _reason;

    public InvalidExpressionValue(string expression, string reason)
    {
        _expression = expression;
        _reason = reason;
    }

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        context.LogWarning("FunctionConverter", $"Invalid expression '={_expression}': {_reason}");
        return FunctionResult<TNode>.Failed(currentNode);
    }

    public string ToScript() => $"={_expression}";
}
