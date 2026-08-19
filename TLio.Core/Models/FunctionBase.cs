using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// Convenience base class for functions. Handles argument storage and ToScript().
/// </summary>
public abstract class FunctionBase<TNode> : IFunction<TNode>
{
    protected Arguments<TNode> Arguments { get; private set; } = new();

    public virtual string FunctionName => TypeName(GetType());

    /// <summary>
    /// The CLR name of a generic type carries an arity suffix (<c>Concat`1</c>), which would
    /// leak into log messages and into <see cref="ToScript"/> output — producing script text
    /// that cannot be parsed back. Strip it.
    /// </summary>
    protected static string TypeName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name.Substring(0, tick);
    }

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

    /// <summary>
    /// Evaluate a function argument and resolve it to its actual data node(s).
    /// When the argument evaluates to a single string that the fetcher recognises as a path
    /// expression (<see cref="IItemsFetcher{TNode}.IsPathExpression"/>), the string is
    /// re-evaluated against <paramref name="dataContext"/> via
    /// <see cref="IItemsFetcher{TNode}.SelectNodes"/>.
    /// This allows value-consuming functions (Math, Text, TimeDate) to accept inline
    /// path args written as =funcname($.field) without needing a dedicated PathValue.
    /// </summary>
    protected static FunctionResult<TNode> ResolveArg(
        IFunctionSupportedValue<TNode> arg,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context)
    {
        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count != 1)
            return result;

        var str = context.NodeAdapter.TryGetString(result.Data[0]);
        if (str != null && context.ItemsFetcher.IsPathExpression(str))
        {
            var nodes = context.ItemsFetcher.SelectNodes(str, dataContext);
            return nodes.Count > 0
                ? FunctionResult<TNode>.Successful(nodes)
                : FunctionResult<TNode>.Failed(currentNode);
        }

        return result;
    }
}
