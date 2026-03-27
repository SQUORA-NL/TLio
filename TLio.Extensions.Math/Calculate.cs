using System.Data;
using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// =calculate(expression) — evaluates a basic arithmetic expression string.
/// Supported operators: +, -, *, /, % and parentheses.
/// Examples: "2 + 3", "10 * (4 - 1)", "7 % 3".
///
/// Uses System.Data.DataTable.Compute for expression evaluation.
/// </summary>
public class Calculate<TNode> : MathFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: one expression argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var argResult = Arguments[0].GetValue(currentNode, dataContext, context);
        if (!argResult.Success || argResult.Data.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: argument path not found.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        var expression = context.NodeAdapter.TryGetString(argResult.Data.First!);
        if (expression == null)
        {
            context.LogError(FunctionName, $"{FunctionName}: expression argument must be a string.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        try
        {
            var dt = new DataTable();
            var computed = dt.Compute(expression, null);
            var numResult = Convert.ToDouble(computed, System.Globalization.CultureInfo.InvariantCulture);
            return FunctionResult<TNode>.Successful(CreateNumericResult(numResult, context.NodeAdapter));
        }
        catch (Exception ex)
        {
            context.LogError(FunctionName, $"{FunctionName}: could not evaluate expression '{expression}': {ex.Message}");
            return FunctionResult<TNode>.Failed(currentNode);
        }
    }
}
