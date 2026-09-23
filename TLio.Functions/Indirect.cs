using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =indirect($.pathToPath) — reads the string stored at the given path, then
/// selects nodes using that string as a second path expression.
///
/// Example: if $.ref = "$.person.name", then =indirect($.ref) returns the
/// value of $.person.name.
///
/// Ported from JLio's Indirect function.
/// </summary>
public class Indirect<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "indirect";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, "indirect() requires one path argument.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        // Step 1: argument gives the path to the reference holder (e.g. "$.pathRef")
        var argResult = Arguments[0].GetValue(currentNode, dataContext, context);
        if (!argResult.Success || argResult.Data.First == null)
            return FunctionResult<TNode>.Failed(currentNode);

        var refPath = context.NodeAdapter.TryGetString(argResult.Data.First);
        if (string.IsNullOrEmpty(refPath))
            return FunctionResult<TNode>.Failed(currentNode);

        // Step 2: read the string stored at refPath — this is the "real" path. A relative
        // refPath ("@.pathRef") is anchored on currentNode, the same rule every other path
        // resolution in the engine applies.
        var refNodes = RelativePathResolution.SelectRelative(refPath, currentNode, dataContext, context);
        if (refNodes.Count == 0)
            return FunctionResult<TNode>.Failed(currentNode);

        var actualPath = context.NodeAdapter.TryGetString(refNodes[0]);
        if (string.IsNullOrEmpty(actualPath))
        {
            context.LogWarning(FunctionName, "indirect() path reference did not resolve to a string.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        // Step 3: select nodes at the resolved path — same rule again.
        var nodes = RelativePathResolution.SelectRelative(actualPath, currentNode, dataContext, context);
        if (nodes.Count == 0)
            return FunctionResult<TNode>.Failed(currentNode);

        return FunctionResult<TNode>.Successful(nodes);
    }
}
