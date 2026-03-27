namespace TLio.Extensions.Text;

/// <summary>
/// =format(template, arg1, arg2, ...) — string format using {0}, {1}, ... placeholders.
/// Arguments are resolved to their string representation.
/// </summary>
public class Format<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least one argument required (template).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var template, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var args = new List<object?>();
        for (int i = 1; i < Arguments.Count; i++)
        {
            if (!TryGetStringArg(Arguments[i], out var argStr, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
            args.Add(argStr);
        }

        try
        {
            var formatted = string.Format(template, args.ToArray());
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(formatted));
        }
        catch (FormatException ex)
        {
            context.LogError(FunctionName, $"{FunctionName}: format error: {ex.Message}");
            return FunctionResult<TNode>.Failed(currentNode);
        }
    }
}
