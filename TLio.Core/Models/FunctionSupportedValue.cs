using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// An IFunctionSupportedValue that delegates GetValue() to an inner IFunction.
/// Used by the script parser when it encounters an "=functionName(args)" string.
///
/// Mirrors JLio's FunctionSupportedValue — wraps any IFunction and surfaces
/// any errors via the execution logger rather than exceptions.
/// </summary>
public class FunctionSupportedValue<TNode> : IFunctionSupportedValue<TNode>
{
    private readonly IFunction<TNode> _function;

    public FunctionSupportedValue(IFunction<TNode> function)
    {
        _function = function;
    }

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        try
        {
            var result = _function.Execute(currentNode, dataContext, context);
            if (!result.Success)
                context.LogWarning("FunctionSupportedValue",
                    $"Function '{_function.FunctionName}' returned failure.");
            return result;
        }
        catch (Exception ex)
        {
            context.LogError("FunctionSupportedValue",
                $"Function '{_function.FunctionName}' threw: {ex.Message}");
            return FunctionResult<TNode>.Failed(currentNode);
        }
    }

    public string ToScript() => $"={_function.ToScript()}";
}
