namespace TLio.Extensions.Text;

/// <summary>=newguid() — generates a new GUID string in the format "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".</summary>
public class NewGuid<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(Guid.NewGuid().ToString()));
    }
}
