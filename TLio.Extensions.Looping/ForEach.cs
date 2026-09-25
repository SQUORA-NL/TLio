using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Extensions.Looping;

/// <summary>
/// Executes a nested script once per element of an array, with <c>@</c> addressing the element
/// that iteration is on — the same nested-<see cref="TLioScript{TNode}"/> shape
/// <c>ifElse</c> already uses for <c>ifScript</c>/<c>elseScript</c>, and the same relative-path
/// token (<c>@</c>) <c>decisionTable</c>/<c>resolve</c>/<c>setProperties</c> already use, just
/// with the loop's current element as its anchor instead of a matched node those commands
/// already had in hand.
///
/// Each iteration threads the (possibly mutated) document into the next — the same
/// left-to-right accumulation <see cref="TLioScript{TNode}"/> already uses between top-level
/// commands — so running state elsewhere in the document (a total, an accrued amount, a
/// previous date) is just an ordinary field a <c>set path="$.state.x"</c> writes and the next
/// iteration's functions read back, while <c>@</c>/<c>@.field</c> addresses the item itself.
///
/// <c>{"command":"forEach","path":"$.schedule","commands":[
///   {"command":"set","path":"@","value":{"date":"@","total":"=multiply(@,2)"}}
/// ]}</c>
/// </summary>
public class ForEach<TNode> : CommandBase<TNode>
{
    public override string CommandName => "forEach";

    /// <summary>Path to the array node to iterate. Each element becomes one iteration.</summary>
    public string? Path { get; set; }

    /// <summary>Script executed once per element, with <c>@</c> addressing that element.</summary>
    public TLioScript<TNode>? Commands { get; set; }

    /// <summary>Safety cap on the number of elements processed. Defaults to 100,000.</summary>
    public int? MaxIterations { get; set; }

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.Failure, 0,
                $"{CommandName}: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var resolvedPath = LoopSupport.ResolvePath(Path!, dataContext, context, CommandName);
        if (resolvedPath == null)
        {
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: the =indirect() expression in path '{Path}' could not be resolved; nothing changed."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        var arrayNode = context.ItemsFetcher.SelectNode(resolvedPath, dataContext);
        if (arrayNode == null || !context.NodeAdapter.IsArray(arrayNode))
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: path '{Path}' did not resolve to an array — nothing changed");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: path '{Path}' did not resolve to an array; nothing changed."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        // Snapshot the element references up front: a stable list to iterate even though each
        // pass may mutate the element it is currently on. Mutating the *array itself* — inserting
        // or removing elements at Path — mid-loop is not supported; later iterations still hold
        // whatever reference they were snapshotted with.
        var elements = context.NodeAdapter.GetArrayElements(arrayNode).ToList();

        var maxIterations = MaxIterations is > 0 ? MaxIterations.Value : LoopSupport.DefaultMaxIterations;
        if (elements.Count > maxIterations)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: array at '{Path}' has {elements.Count} element(s), more than maxIterations ({maxIterations}) — stopping after {maxIterations}");
            elements = elements.Take(maxIterations).ToList();
        }

        if (Commands is not { Count: > 0 } || elements.Count == 0)
        {
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: {(elements.Count == 0 ? "array is empty" : "no nested commands")}; nothing executed."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var previousCurrentNode = context.CurrentNode;
        var result = TLioExecutionResult<TNode>.Successful(dataContext);
        var iterations = 0;
        try
        {
            foreach (var element in elements)
            {
                context.CurrentNode = element;
                result = Commands.Execute(result.Data, context);
                iterations++;
                if (!result.Success) break;
            }
        }
        finally
        {
            context.CurrentNode = previousCurrentNode;
        }

        if (!result.Success) MarkFailed();
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path ?? "", result.Success ? TraceOutcome.Success : TraceOutcome.Failure, iterations,
            $"{CommandName}: ran {iterations} of {elements.Count} planned iteration(s) over '{Path}'."));
        return new TLioExecutionResult<TNode>(result.Success, result.Data);
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"{CommandName}: Path property is required.");
        return result;
    }
}
