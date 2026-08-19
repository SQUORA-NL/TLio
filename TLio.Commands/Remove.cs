using TLio.Commands.Logic;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Removes all nodes matched by the path expression.
/// Mirrors JLio's Remove command exactly.
/// </summary>
public class Remove<TNode> : CommandBase<TNode>
{
    public string? Path { get; set; }

    public Remove() { }
    public Remove(string path) => Path = path;

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

        var resolvedPath = IndirectPath.TryResolve(Path!, dataContext, context, CommandName);
        if (resolvedPath == null)
        {
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: the =indirect() expression in path '{Path}' could not be resolved; nothing removed."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        var targets = context.ItemsFetcher.SelectNodes(resolvedPath, dataContext);
        // Enumerate to a list first so removal doesn't invalidate the iterator
        var targetList = targets.ToList();

        if (targetList.Count == 0)
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: no nodes matched path '{Path}' — nothing removed");

        foreach (var target in targetList)
        {
            var path = context.ItemsFetcher.GetPath(target);
            var removed = context.NodeAdapter.RemoveFromParent(target);
            if (removed)
                context.LogInfo(CoreConstants.CommandExecution, $"{CommandName}: removed node at '{path}'");
            else
                context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: could not remove node at '{path}' (root or unsupported type)");
        }

        var removeOutcome = targetList.Count == 0 ? TraceOutcome.NoOp : TraceOutcome.Success;
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path ?? "", removeOutcome, targetList.Count,
            removeOutcome == TraceOutcome.NoOp
                ? $"{CommandName}: path '{Path}' matched 0 nodes — field does not exist; nothing removed. Verify the path is correct."
                : $"{CommandName}: removed {targetList.Count} node(s) at '{Path}'."));

        return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"Path property for {CommandName} command is missing");
        return result;
    }
}
