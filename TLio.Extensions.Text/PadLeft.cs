namespace TLio.Extensions.Text;

/// <summary>=padleft(str, totalWidth[, padChar]) — right-aligns str in a field of totalWidth, padding left with padChar (default space).</summary>
public class PadLeft<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least two arguments required (str, totalWidth).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetIntArg(Arguments[1], out var totalWidth, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        char padChar = ' ';
        if (Arguments.Count >= 3)
        {
            if (!TryGetCharArg(Arguments[2], out padChar, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
        }
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(str.PadLeft(totalWidth, padChar)));
    }
}
