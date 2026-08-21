using System.Globalization;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =multiply(arg1, arg2, ...) — multiplies all numeric arguments. Arrays are flattened, so
/// =multiply($.factors) is the product of every element of that array.
///
/// Found-but-null multiplies as 0, not as the identity 1. Treating it as 1 would read better in
/// isolation, but it would make multiply the one Math function with its own null rule: every
/// other function in this pack maps a found null to 0, and a rate table with a missing factor
/// should collapse the premium to zero rather than quietly price as if the factor were neutral.
/// </summary>
public class Multiply<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        double product = 1;
        foreach (var arg in Arguments)
            if (!TryMultiplyArg(arg, ref product, currentNode, dataContext, context, FunctionName))
                return FunctionResult<TNode>.Failed(currentNode);
        return FunctionResult<TNode>.Successful(CreateNumericResult(product, context.NodeAdapter));
    }

    /// <summary>
    /// Multiplicative twin of <c>MathFunctionBase.TryAccumulateArg</c>: resolves one argument and
    /// multiplies every numeric value it matched into <paramref name="product"/>.
    /// </summary>
    private static bool TryMultiplyArg(
        IFunctionSupportedValue<TNode> arg,
        ref double product,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        foreach (var node in result.Data)
            if (!TryMultiplyNode(node, ref product, context.NodeAdapter, context, funcName))
                return false;
        return true;
    }

    private static bool TryMultiplyNode(
        TNode node, ref double product,
        INodeAdapter<TNode> adapter, IExecutionContext<TNode> context, string funcName)
    {
        if (adapter.IsNull(node)) { product = 0; return true; }   // found-null = 0, as everywhere in this pack
        if (adapter.IsArray(node))
        {
            foreach (var el in adapter.GetArrayElements(node))
                if (!TryMultiplyNode(el, ref product, adapter, context, funcName))
                    return false;
            return true;
        }
        var num = adapter.TryGetDouble(node);
        if (num.HasValue) { product *= num.Value; return true; }
        var str = adapter.TryGetString(node);
        if (str != null && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            product *= parsed;
            return true;
        }
        context.LogError(funcName, $"{funcName}: non-numeric value encountered.");
        return false;
    }
}
