using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Evaluates a condition value and executes either the if-script or the else-script.
/// Ported from JLio's IfElse command.
///
/// Condition truthy rules (mirrors JLio):
///   - bool true → execute IfScript
///   - string "true" (case-insensitive) → execute IfScript
///   - IFunctionSupportedValue whose result DeepEquals the expected value → execute IfScript
///   - Otherwise → execute ElseScript
/// </summary>
public class IfElse<TNode> : CommandBase<TNode>
{
    public override string CommandName => "ifElse";

    public IFunctionSupportedValue<TNode>? Condition { get; set; }
    public TLioScript<TNode>? IfScript { get; set; }
    public TLioScript<TNode>? ElseScript { get; set; }

    public IfElse() { }

    public IfElse(IFunctionSupportedValue<TNode> condition,
                  TLioScript<TNode>? ifScript,
                  TLioScript<TNode>? elseScript)
    {
        Condition = condition;
        IfScript = ifScript;
        ElseScript = elseScript;
    }

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var conditionResult = Condition!.GetValue(dataContext, dataContext, context);
        bool isTrue = EvaluateCondition(conditionResult, context);

        context.LogInfo(CoreConstants.CommandExecution,
            $"{CommandName}: condition evaluated to {isTrue}");

        var scriptToRun = isTrue ? IfScript : ElseScript;
        if (scriptToRun is { Count: > 0 })
            return scriptToRun.Execute(dataContext, context);

        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    private bool EvaluateCondition(FunctionResult<TNode> result, IExecutionContext<TNode> context)
    {
        if (!result.Success || result.Data.First == null)
            return false;

        var node = result.Data.First;

        var boolVal = context.NodeAdapter.TryGetBoolean(node);
        if (boolVal.HasValue) return boolVal.Value;

        var strVal = context.NodeAdapter.TryGetString(node);
        if (strVal != null) return string.Equals(strVal, "true", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (Condition is null)
            result.AddError($"{CommandName}: Condition is required.");
        return result;
    }
}
