namespace TLio.Extensions.Text;

/// <summary>=isempty(value) — returns true if value is null, empty string, or an empty array.</summary>
public class IsEmpty<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var result = ResolveArg(Arguments[0], currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: argument path not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        var node = result.Data.First!;
        bool empty;
        if (context.NodeAdapter.IsNull(node))
            empty = true;
        else if (context.NodeAdapter.IsArray(node))
            empty = context.NodeAdapter.GetArrayLength(node) == 0;
        else
            empty = string.IsNullOrEmpty(context.NodeAdapter.TryGetString(node));
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateBoolean(empty));
    }
}
