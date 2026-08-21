namespace TLio.Extensions.Text;

/// <summary>
/// =right(str, count) — the last <c>count</c> characters of a string.
///
/// Bounds follow =substring exactly: a count at or above the string length returns the whole
/// string and a count of zero or less returns "". Siblings that clamp differently are a trap, so
/// this one does not invent its own rule.
///
/// There is deliberately no =left — that is already =substring(str, 0, count). =right earns a
/// function because the alternative reaches for arithmetic:
/// =substring(s, =subtract(=length(s), n), n).
/// </summary>
public class Right<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (str, count).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetIntArg(Arguments[1], out var count, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        count = System.Math.Max(0, System.Math.Min(count, str.Length));
        return FunctionResult<TNode>.Successful(
            context.NodeAdapter.CreateString(str[(str.Length - count)..]));
    }
}
