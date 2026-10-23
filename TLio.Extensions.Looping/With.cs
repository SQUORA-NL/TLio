using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Extensions.Looping;

/// <summary>
/// Evaluates <c>path</c> once and runs a nested script with <c>@</c> addressing the node it
/// selected — <c>forEach</c> for exactly one node, with no array required.
///
/// It is for the script that reaches into the same related record for many fields:
/// instead of repeating a long lookup in every value, select the record once and write
/// <c>@.firstName</c>, <c>=fetch(@.address.city)</c> inside. The nested commands run on the
/// same <see cref="IExecutionContext{TNode}.CurrentNode"/> mechanism <c>forEach</c> uses, so
/// everything <c>@</c> does there it does here, and the previous current node is restored
/// afterwards (so <c>with</c> nests inside <c>forEach</c>, and the other way round).
///
/// No match: a warning, the block is skipped and the script continues. More than one match: the
/// first is used and a warning says so — iterating over several nodes is <c>forEach</c>'s job.
///
/// <c>{"command":"with","path":"$.parties[0]","commands":[
///   {"command":"add","path":"$.out.name","value":"@.firstName"}
/// ]}</c>
/// </summary>
public class With<TNode> : CommandBase<TNode>
{
    public override string CommandName => "with";

    /// <summary>Path selecting the node <c>@</c> refers to inside <see cref="Commands"/>.</summary>
    public string? Path { get; set; }

    /// <summary>Script executed once, with <c>@</c> addressing the selected node.</summary>
    public TLioScript<TNode>? Commands { get; set; }

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

        var matches = context.ItemsFetcher.SelectNodes(resolvedPath, dataContext);
        if (matches.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: path '{Path}' matched nothing — block skipped");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: path '{Path}' matched nothing; block skipped."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        if (matches.Count > 1)
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: path '{Path}' matched {matches.Count} nodes — using the first (use forEach to visit each)");

        if (Commands is not { Count: > 0 })
        {
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: no nested commands; nothing executed."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var previousCurrentNode = context.CurrentNode;
        TLioExecutionResult<TNode> result;
        try
        {
            context.CurrentNode = matches.First;
            result = Commands.Execute(dataContext, context);
        }
        finally
        {
            context.CurrentNode = previousCurrentNode;
        }

        if (!result.Success) MarkFailed();
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path ?? "", result.Success ? TraceOutcome.Success : TraceOutcome.Failure, 1,
            $"{CommandName}: ran {Commands.Count} nested command(s) on '{Path}'."));
        return new TLioExecutionResult<TNode>(result.Success, result.Data);
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"{CommandName}: Path property is required.");
        return result;
    }

    // Commands is a nested TLioScript, not read-only configuration — see ForEach<TNode>.Clone().
    public override ICommand<TNode> Clone()
    {
        var clone = (With<TNode>)base.Clone();
        clone.Commands = Commands?.Clone();
        return clone;
    }
}
