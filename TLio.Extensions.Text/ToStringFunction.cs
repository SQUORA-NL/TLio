namespace TLio.Extensions.Text;

/// <summary>
/// =toString(node) — converts any node to its string representation.
///
/// Primitives: returned via TryGetString (number, bool, string → their string form).
/// Objects/arrays: serialized to compact JSON via NodeAdapter.Serialize.
/// Null: returns empty string.
///
/// Named ToStringFunction to avoid conflict with object.ToString().
/// </summary>
public class ToStringFunction<TNode> : TextFunctionBase<TNode>
{
    // Override required: TextFunctionBase returns GetType().Name.ToLowerInvariant()
    // which would be "tostringfunction`1" — we need the registered name "toString".
    public override string FunctionName => "toString";

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
            context.LogWarning(FunctionName, $"{FunctionName}: argument path not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var node = result.Data.First!;

        if (context.NodeAdapter.IsNull(node))
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(string.Empty));

        // For objects and arrays: serialize to compact JSON
        if (context.NodeAdapter.IsObject(node) || context.NodeAdapter.IsArray(node))
        {
            var json = context.NodeAdapter.Serialize(node, false);
            context.LogInfo(FunctionName, $"{FunctionName}: serialized node to JSON string.");
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(json));
        }

        // For primitives: use TryGetString (handles bool, number, string)
        var str = context.NodeAdapter.TryGetString(node) ?? string.Empty;
        context.LogInfo(FunctionName, $"{FunctionName}: converted node to string.");
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(str));
    }
}
