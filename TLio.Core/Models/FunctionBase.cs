using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// Convenience base class for functions. Handles argument storage and ToScript().
/// </summary>
public abstract class FunctionBase<TNode> : IFunction<TNode>
{
    protected Arguments<TNode> Arguments { get; private set; } = new();

    public virtual string FunctionName => GetType().Name;

    public IFunction<TNode> SetArguments(Arguments<TNode> arguments)
    {
        Arguments = arguments;
        return this;
    }

    public string ToScript()
    {
        var argScripts = Arguments.Select(a => a.ToScript());
        return $"{FunctionName}({string.Join(",", argScripts)})";
    }

    public abstract FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context);
}
