namespace TLio.Extensions.Text;

/// <summary>=trim(str) — removes leading and trailing whitespace.</summary>
public class Trim<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(str.Trim()));
    }
}
