namespace TLio.Extensions.Text;

/// <summary>=replace(str, oldValue, newValue) — replaces all occurrences of oldValue with newValue.</summary>
public class Replace<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 3)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: three arguments required (str, oldValue, newValue).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[1], out var oldValue, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[2], out var newValue, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (oldValue.Length == 0)
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(str));
        return FunctionResult<TNode>.Successful(
            context.NodeAdapter.CreateString(str.Replace(oldValue, newValue, StringComparison.Ordinal)));
    }
}
