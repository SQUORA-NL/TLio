namespace TLio.Extensions.Text;

/// <summary>=concat(arg1, arg2, ...) — concatenates all arguments as strings.</summary>
public class Concat<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var parts = new List<string>();
        foreach (var arg in Arguments)
            if (!TryCollectStrings(arg, parts, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(string.Concat(parts)));
    }
}
