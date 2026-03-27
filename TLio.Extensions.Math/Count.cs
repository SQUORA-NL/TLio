using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>=count(arg1, arg2, ...) — counts nodes. Arrays count as their element count.</summary>
public class Count<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        long count = 0;
        foreach (var arg in Arguments)
        {
            var result = arg.GetValue(currentNode, dataContext, context);
            if (!result.Success || result.Data.Count == 0) continue;
            foreach (var node in result.Data)
            {
                if (context.NodeAdapter.IsArray(node))
                    count += context.NodeAdapter.GetArrayLength(node);
                else
                    count += 1;
            }
        }
        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateValue(count));
    }
}
