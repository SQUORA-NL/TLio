namespace TLio.Extensions.Text;

/// <summary>
/// =split(str, delimiter) — splits str by delimiter and returns a JSON array of strings.
/// Empty delimiter splits by character.
/// </summary>
public class Split<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: two arguments required (str, delimiter).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetStringArg(Arguments[1], out var delimiter, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        string[] parts = delimiter.Length == 0
            ? str.Select(c => c.ToString()).ToArray()
            : str.Split(delimiter, StringSplitOptions.None);

        var array = context.NodeAdapter.CreateArray();
        foreach (var part in parts)
            context.NodeAdapter.AppendToArray(array, context.NodeAdapter.CreateString(part));
        return FunctionResult<TNode>.Successful(array);
    }
}
