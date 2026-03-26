using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =promote($.path) — wraps the node at the given path in a new single-property
/// object using the node's own property name as the key.
///
/// Example: if $.item = {"id":1,"name":"Alice"} and the item lives under property
/// "item", then =promote($.item) returns {"item":{"id":1,"name":"Alice"}}.
///
/// Ported from JLio's Promote function.
/// </summary>
public class Promote<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "promote";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, "promote() requires one path argument.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var nodeResult = Arguments[0].GetValue(currentNode, dataContext, context);
        if (!nodeResult.Success || nodeResult.Data.First == null)
            return FunctionResult<TNode>.Failed(currentNode);

        var node = nodeResult.Data.First;
        var propertyName = context.NodeAdapter.GetParentPropertyName(node);

        if (propertyName == null)
        {
            context.LogWarning(FunctionName, "promote() target node has no parent property name.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var wrapper = context.NodeAdapter.CreateObject();
        context.NodeAdapter.SetProperty(wrapper, propertyName, context.NodeAdapter.DeepClone(node));
        return FunctionResult<TNode>.Successful(wrapper);
    }
}
