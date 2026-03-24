using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// An IFunctionSupportedValue that always returns a pre-set node — the simplest
/// possible "value provider" for use in command arguments.
/// </summary>
public class FixedValue<TNode> : IFunctionSupportedValue<TNode>
{
    private readonly TNode _value;

    public FixedValue(TNode value) => _value = value;

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context) =>
        FunctionResult<TNode>.Successful(_value);

    public string ToScript() => "[fixed]";
}
