namespace TLio.Extensions.Text;

/// <summary>=length(value) — string length, or element count for arrays.</summary>
public class Length<TNode> : TextFunctionBase<TNode>
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
        long length;
        if (context.NodeAdapter.IsArray(node))
            length = context.NodeAdapter.GetArrayLength(node);
        else if (context.NodeAdapter.IsNull(node))
            length = 0;
        else
            length = (context.NodeAdapter.TryGetString(node) ?? string.Empty).Length;
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue(length));
    }
}
