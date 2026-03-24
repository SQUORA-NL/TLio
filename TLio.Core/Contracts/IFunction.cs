using TLio.Core.Models;

namespace TLio.Core.Contracts;

/// <summary>
/// A function computes a value (or set of values) from the current node and data
/// context. Functions are used as value-producing arguments inside commands.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface IFunction<TNode>
{
    string FunctionName { get; }

    IFunction<TNode> SetArguments(Arguments<TNode> arguments);

    FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context);

    string ToScript();
}
