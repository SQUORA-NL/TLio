namespace TLio.Core.Models;

public class FunctionResult<TNode>
{
    public FunctionResult(bool success, SelectedNodes<TNode> data)
    {
        Success = success;
        Data = data;
    }

    public FunctionResult(bool success, TNode data)
    {
        Success = success;
        Data = new SelectedNodes<TNode>(data);
    }

    public bool Success { get; }
    public SelectedNodes<TNode> Data { get; }

    public static FunctionResult<TNode> Successful(TNode data) => new(true, data);
    public static FunctionResult<TNode> Successful(SelectedNodes<TNode> data) => new(true, data);
    public static FunctionResult<TNode> Failed(TNode data) => new(false, data);
}
