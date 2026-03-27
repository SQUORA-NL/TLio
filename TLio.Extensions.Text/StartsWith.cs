namespace TLio.Extensions.Text;

/// <summary>=startswith(str, prefix) — returns true if str starts with prefix (case-insensitive).</summary>
public class StartsWith<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (str, prefix).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[1], out var prefix, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        return FunctionResult<TNode>.Successful(
            context.NodeAdapter.CreateBoolean(str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
    }
}
