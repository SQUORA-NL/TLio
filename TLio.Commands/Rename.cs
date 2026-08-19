using TLio.Commands.Logic;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Renames the nodes matched by the path, keeping their value, children and position.
///
/// This is the operation copy+remove cannot express: it preserves child order and XML
/// attributes, and it reaches the document element, which has no parent to copy out of.
///
///   {"command": "rename", "path": "/order",          "name": "opdracht"}
///   {"command": "rename", "path": "/order/customer", "name": "client"}
///   {"command": "rename", "path": "//item",          "name": "line"}
///
/// Where the name lives is format-specific — see <see cref="INodeAdapter{TNode}.RenameNode"/>.
/// In JSON and YAML the name belongs to the parent that holds the node, so the root has no
/// name and renaming it warns instead of failing. XML elements carry their own name, so even
/// the document element renames cleanly.
///
/// Follows the same rule as the other commands: a path that matches nothing, or a node that
/// has no name, logs a warning and lets the rest of the script run.
/// </summary>
public class Rename<TNode> : CommandBase<TNode>
{
    public string? Path { get; set; }

    /// <summary>The new name for each matched node.</summary>
    public string? Name { get; set; }

    public Rename() { }

    public Rename(string path, string name)
    {
        Path = path;
        Name = name;
    }

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
                $"{CommandName}: the =indirect() expression in path '{Path}' could not be resolved; nothing renamed."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        var targets = context.ItemsFetcher.SelectNodes(resolvedPath, dataContext).ToList();
        if (targets.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: no nodes matched path '{Path}' — nothing renamed");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: path '{Path}' matched 0 nodes; nothing renamed. Verify the path is correct."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        var renamed = 0;
        foreach (var target in targets)
        {
            var oldPath = context.ItemsFetcher.GetPath(target);
            if (context.NodeAdapter.RenameNode(target, Name!))
            {
                renamed++;
                context.LogInfo(CoreConstants.CommandExecution,
                    $"{CommandName}: renamed '{oldPath}' to '{Name}'");
            }
            else
            {
                context.LogWarning(CoreConstants.CommandExecution,
                    $"{CommandName}: node at '{oldPath}' has no name to change, or '{Name}' is not a usable name");
            }
        }

        var outcome = renamed == 0 ? TraceOutcome.NoOp : TraceOutcome.Success;
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path ?? "", outcome, renamed,
            outcome == TraceOutcome.NoOp
                ? $"{CommandName}: matched {targets.Count} node(s) at '{Path}' but none could be renamed — " +
                  $"in JSON and YAML the root has no name of its own."
                : $"{CommandName}: renamed {renamed} node(s) at '{Path}' to '{Name}'."));

        return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"Path property for {CommandName} command is missing");
        if (string.IsNullOrWhiteSpace(Name))
            result.AddError($"Name property for {CommandName} command is missing");
        return result;
    }
}
