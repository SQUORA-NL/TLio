using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands.Advanced;

/// <summary>
/// Deep-merges the node(s) at Path into the node(s) at TargetPath.
///
/// Ported from JLio's Merge command. Uses INodeAdapter.DeepMergeInto for all
/// merge logic — no format-specific code in this command.
///
/// Array merge behaviour is controlled by the ArrayMergeMode on each call.
/// Default is Concat (appends array elements).
/// </summary>
public class Merge<TNode> : CommandBase<TNode>
{
    public override string CommandName => "merge";

    public string? Path { get; set; }
    public string? TargetPath { get; set; }
    public ArrayMergeMode ArrayMergeMode { get; set; } = ArrayMergeMode.Concat;

    // JLio-compatible aliases (FR-003/FR-004)
    public string? FromPath { set => Path = value; }
    public string? ToPath   { set => TargetPath = value; }

    public Merge() { }

    public Merge(string path, string targetPath, ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat)
    {
        Path = path;
        TargetPath = targetPath;
        ArrayMergeMode = arrayMergeMode;
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

        var sources = context.ItemsFetcher.SelectNodes(Path!, dataContext);
        if (sources.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no nodes at source path '{Path}'");
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var targets = context.ItemsFetcher.SelectNodes(TargetPath!, dataContext);
        if (targets.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no nodes at target path '{TargetPath}'");
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        foreach (var source in sources)
            foreach (var target in targets)
                context.NodeAdapter.DeepMergeInto(source, target, ArrayMergeMode);

        context.LogInfo(CoreConstants.CommandExecution,
            $"{CommandName}: merged {sources.Count} source(s) into {targets.Count} target(s)");
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path)) result.AddError($"{CommandName}: Path is required.");
        if (string.IsNullOrWhiteSpace(TargetPath)) result.AddError($"{CommandName}: TargetPath is required.");
        return result;
    }
}
