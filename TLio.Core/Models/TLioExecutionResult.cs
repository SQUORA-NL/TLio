namespace TLio.Core.Models;

public class TLioExecutionResult<TNode>
{
    public TLioExecutionResult(bool success, TNode data)
    {
        Success = success;
        Data = data;
    }

    public bool Success { get; }
    public TNode Data { get; }

    public static TLioExecutionResult<TNode> Successful(TNode data) => new(true, data);
    public static TLioExecutionResult<TNode> Failed(TNode data) => new(false, data);
}
