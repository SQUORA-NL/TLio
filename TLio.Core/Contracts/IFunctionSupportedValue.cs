using TLio.Core.Models;

namespace TLio.Core.Contracts;

/// <summary>
/// A value that can either be a fixed literal or the result of a function call.
/// Used as the "value" argument of commands like Set and Add.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface IFunctionSupportedValue<TNode>
{
    FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context);

    string ToScript();
}
