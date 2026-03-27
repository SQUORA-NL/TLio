namespace TLio.Extensions.Text;

/// <summary>=substring(str, startIndex[, length]) — extracts a substring. Zero-based start index.</summary>
public class Substring<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least two arguments required (str, startIndex).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetIntArg(Arguments[1], out var start, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        start = System.Math.Max(0, System.Math.Min(start, str.Length));

        if (Arguments.Count >= 3)
        {
            if (!TryGetIntArg(Arguments[2], out var len, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
            len = System.Math.Max(0, System.Math.Min(len, str.Length - start));
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(str.Substring(start, len)));
        }
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(str[start..]));
    }
}
