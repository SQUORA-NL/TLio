namespace TLio.Extensions.Text;

/// <summary>=endswith(str, suffix) — returns true if str ends with suffix (case-insensitive).</summary>
public class EndsWith<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (str, suffix).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[1], out var suffix, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        return FunctionResult<TNode>.Successful(
            context.NodeAdapter.CreateBoolean(str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
    }
}
