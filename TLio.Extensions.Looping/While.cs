using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Extensions.Looping;

/// <summary>
/// Repeats a nested script while <see cref="Condition"/> evaluates truthy, mirroring
/// <c>TLio.Commands.IfElse{TNode}</c>'s truthy rules (bool <c>true</c>, string "true", or an
/// <see cref="IFunctionSupportedValue{TNode}"/> result that reads as either). Condition is
/// re-evaluated against the mutated document before every pass, including the first, so a
/// condition that starts false never runs the body at all.
///
/// There is no per-pass "current item" here — unlike <see cref="ForEach{TNode}"/>, <c>while</c>
/// doesn't set <see cref="IExecutionContext{TNode}.CurrentNode"/> itself, so a <c>while</c> body
/// works against whatever absolute, evolving state its own commands choose to read and write
/// (advancing a scratch "next date" field with <c>dateAdd</c>, for example). <c>@</c> still
/// works inside a <c>while</c> nested inside a <c>forEach</c>, though — it reaches the enclosing
/// loop's current element, since nothing here clears it.
///
/// <see cref="MaxIterations"/> is required (no default): an unbounded <c>while</c> over a live
/// document is a real hang risk, so this command is the one place in the package that insists a
/// caller think about a bound instead of getting a generous default for free.
///
/// <c>{"command":"while","condition":"=lessOrEqual(...)","commands":[...],"maxIterations":1000}</c>
/// </summary>
public class While<TNode> : CommandBase<TNode>
{
    public override string CommandName => "while";

    public IFunctionSupportedValue<TNode>? Condition { get; set; }
    public TLioScript<TNode>? Commands { get; set; }
    public int MaxIterations { get; set; }

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, "(condition)", TraceOutcome.Failure, 0,
                $"{CommandName}: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var result = TLioExecutionResult<TNode>.Successful(dataContext);
        var iterations = 0;
        var hitLimit = false;

        while (IsConditionTrue(result.Data, context))
        {
            if (iterations >= MaxIterations)
            {
                hitLimit = true;
                break;
            }

            if (Commands is not { Count: > 0 })
                break; // condition true but nothing to run — never spin on unchanging state

            result = Commands.Execute(result.Data, context);
            iterations++;
            if (!result.Success) break;
        }

        if (hitLimit)
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: condition still true after {MaxIterations} iteration(s) — stopped.");

        if (!result.Success) MarkFailed();
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, "(condition)", result.Success ? TraceOutcome.Success : TraceOutcome.Failure, iterations,
            $"{CommandName}: ran {iterations} iteration(s){(hitLimit ? $", stopped at maxIterations={MaxIterations}" : "")}."));
        return new TLioExecutionResult<TNode>(result.Success, result.Data);
    }

    private bool IsConditionTrue(TNode dataContext, IExecutionContext<TNode> context)
    {
        // A while nested inside a forEach can still read @ (the enclosing loop's current
        // element) in its condition, even though while itself has no current item of its own.
        var conditionResult = Condition!.GetValue(context.CurrentNode ?? dataContext, dataContext, context);
        if (!conditionResult.Success || conditionResult.Data.First == null)
            return false;
        return NodeComparison.IsTruthy(conditionResult.Data.First, context.NodeAdapter);
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (Condition is null)
            result.AddError($"{CommandName}: Condition property is required.");
        if (MaxIterations <= 0)
            result.AddError($"{CommandName}: MaxIterations must be a positive integer.");
        return result;
    }
}
