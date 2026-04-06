using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =newGuid() — generates a new random UUID string (RFC 4122 format).
/// Takes no arguments. Returns a string node.
/// </summary>
public class NewGuid<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "newGuid";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var guid = context.NodeAdapter.CreateString(Guid.NewGuid().ToString());
        context.LogInfo(FunctionName, "Generated new GUID.");
        return FunctionResult<TNode>.Successful(guid);
    }
}
