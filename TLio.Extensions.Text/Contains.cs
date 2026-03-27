namespace TLio.Extensions.Text;

/// <summary>=contains(str, substring) — returns true if str contains substring (case-insensitive).</summary>
public class Contains<TNode> : TextFunctionBase<TNode>
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
        return FunctionResult<TNode>.Successful(
            context.NodeAdapter.CreateBoolean(str.Contains(substring, StringComparison.OrdinalIgnoreCase)));
    }
}
