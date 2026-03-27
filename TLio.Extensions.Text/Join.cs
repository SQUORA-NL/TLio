namespace TLio.Extensions.Text;

/// <summary>=join(array, separator) — joins array elements into a single string.</summary>
public class Join<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (array, separator).");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var items = ResolveList(Arguments[0], currentNode, dataContext, context);
        if (items == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: array argument not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TryGetStringArg(Arguments[1], out var separator, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var parts = items.Select(n =>
            context.NodeAdapter.IsNull(n) ? string.Empty
            : (context.NodeAdapter.TryGetString(n) ?? string.Empty));
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(string.Join(separator, parts)));
    }
}
