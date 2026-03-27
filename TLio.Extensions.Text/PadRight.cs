namespace TLio.Extensions.Text;

/// <summary>=padright(str, totalWidth[, padChar]) — left-aligns str in a field of totalWidth, padding right with padChar (default space).</summary>
public class PadRight<TNode> : TextFunctionBase<TNode>
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
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(str.PadRight(totalWidth, padChar)));
    }
}
