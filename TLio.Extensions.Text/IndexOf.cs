namespace TLio.Extensions.Text;

/// <summary>=indexof(str, substring) — returns the zero-based index of the first occurrence, or -1 if not found.</summary>
public class IndexOf<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (str, substring).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[1], out var substring, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        long index = str.IndexOf(substring, StringComparison.OrdinalIgnoreCase);
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue(index));
    }
}
