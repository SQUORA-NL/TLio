using System.Text.RegularExpressions;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands.Logic;

/// <summary>
/// Generic base class for Copy and Move commands.
///
/// Mirrors JLio's CopyMove logic but all format-specific operations are delegated
/// to INodeAdapter&lt;TNode&gt; and IItemsFetcher&lt;TNode&gt;.
///
/// Key behaviours ported from JLio:
/// - =indirect() expressions in ToPath are resolved via IItemsFetcher.ProcessIndirectPath
/// - Root path ($) as destination triggers a DeepMerge instead of property assignment
/// - DestinationAsArray: when destination is not yet an array, wraps it with the source
///   into a new [old, new] array; when destination is already an array, just appends
/// - Array destination (without flag): always appends rather than replaces
/// - Array-index alignment: groups sources and destinations by the first array index
///   found in their path, then processes each group independently (mirrors JLio's
///   GetInnerArrayIndex logic so that e.g. firstArray[0].subs all go to firstArray[0].target)
/// - Move variant removes the source node after copying
/// </summary>
public abstract class CopyMoveBase<TNode> : CommandBase<TNode>
{
    public string? FromPath { get; set; }
    public string? ToPath { get; set; }
    public bool DestinationAsArray { get; set; } = false;

    protected abstract bool IsMove { get; }

    private static readonly Regex ArrayIndexPattern = new(@"\[(\d+)\]", RegexOptions.Compiled);

    /// <summary>
    /// Returns the first numeric array index found in <paramref name="nodePath"/>,
    /// or -1 when the path contains no array subscript.
    /// Mirrors JLio's GetInnerArrayIndex.
    /// </summary>
    private static int GetFirstArrayIndex(string nodePath)
    {
        var match = ArrayIndexPattern.Match(nodePath);
        return match.Success && int.TryParse(match.Groups[1].Value, out var idx) ? idx : -1;
    }

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, $"{FromPath} → {ToPath}", TraceOutcome.Failure, 0,
                $"{CommandName}: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        // Resolve indirect path expressions in ToPath
        var resolvedToPath = context.ItemsFetcher.ProcessIndirectPath(ToPath!, dataContext) ?? ToPath!;

        var sources = context.ItemsFetcher.SelectNodes(FromPath!, dataContext);
        if (sources.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no nodes found at FromPath '{FromPath}'");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, $"{FromPath} → {ToPath}", TraceOutcome.NoOp, 0,
                $"{CommandName}: FromPath '{FromPath}' matched 0 nodes; nothing copied."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        // Special case: destination is root
        if (resolvedToPath == context.ItemsFetcher.RootPathIndicator)
        {
            if (IsMove)
            {
                // Move-to-root: replace the entire root content with the source node's content.
                // Clear all existing root properties first, then merge the clone in.
                foreach (var source in sources)
                {
                    var cloned = context.NodeAdapter.DeepClone(source);
                    foreach (var propName in context.NodeAdapter.GetPropertyNames(dataContext).ToList())
                        context.NodeAdapter.RemoveProperty(dataContext, propName);
                    context.NodeAdapter.DeepMergeInto(cloned, dataContext);
                }
            }
            else
            {
                // Copy-to-root: merge source content into root (additive, keeps existing properties).
                foreach (var source in sources)
                    context.NodeAdapter.DeepMergeInto(context.NodeAdapter.DeepClone(source), dataContext);
            }

            context.LogInfo(CoreConstants.CommandExecution, $"{CommandName}: merged {sources.Count} node(s) into root");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, $"{FromPath} → $", TraceOutcome.Success, sources.Count,
                $"{CommandName}: merged {sources.Count} node(s) from '{FromPath}' into root."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        // Ensure destination paths exist
        context.ItemsFetcher.EnsurePath(resolvedToPath, dataContext, context.NodeAdapter);
        var destinations = context.ItemsFetcher.SelectNodes(resolvedToPath, dataContext);

        if (destinations.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: destination path '{resolvedToPath}' not found after EnsurePath");
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        // Group sources and destinations by their first array index (JLio GetInnerArrayIndex logic).
        // When both sides have the same set of indices, process each index-group independently
        // so that array-structured data is aligned correctly (e.g. sources in firstArray[0] go
        // only to destinations also in firstArray[0]).
        var sourceGroups = sources
            .GroupBy(s => GetFirstArrayIndex(context.ItemsFetcher.GetPath(s)))
            .ToDictionary(g => g.Key, g => g.ToList());
        var destGroups = destinations
            .GroupBy(d => GetFirstArrayIndex(context.ItemsFetcher.GetPath(d)))
            .ToDictionary(g => g.Key, g => g.ToList());

        if (sourceGroups.Keys.OrderBy(k => k).SequenceEqual(destGroups.Keys.OrderBy(k => k)))
        {
            // Index-aligned: process each group independently
            foreach (var idx in sourceGroups.Keys)
                foreach (var src in sourceGroups[idx])
                    foreach (var dst in destGroups[idx])
                        ApplyCopy(src, dst, context);
        }
        else
        {
            // Fall back to many-to-many
            foreach (var src in sources)
                foreach (var dst in destinations)
                    ApplyCopy(src, dst, context);
        }

        if (IsMove)
            foreach (var source in sources)
                context.NodeAdapter.RemoveFromParent(source);

        context.TraceCollector?.Record(new TraceEntry(
            CommandName, $"{FromPath} → {ToPath}", TraceOutcome.Success, sources.Count,
            $"{CommandName}: copied {sources.Count} node(s) from '{FromPath}' to '{ToPath}'."));

        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    private void ApplyCopy(TNode source, TNode destination, IExecutionContext<TNode> context)
    {
        var srcPath = context.ItemsFetcher.GetPath(source);
        var dstPath = context.ItemsFetcher.GetPath(destination);
        var cloned = context.NodeAdapter.DeepClone(source);

        if (context.NodeAdapter.IsArray(destination))
        {
            // Always append to an array destination (with or without DestinationAsArray flag)
            context.NodeAdapter.AppendToArray(destination, cloned);
        }
        else if (DestinationAsArray)
        {
            // Promote scalar/object destination to [old_value, new_value]
            var arr = context.NodeAdapter.CreateArray();
            context.NodeAdapter.AppendToArray(arr, context.NodeAdapter.DeepClone(destination));
            context.NodeAdapter.AppendToArray(arr, cloned);
            context.NodeAdapter.Replace(destination, arr);
        }
        else
        {
            // Normal scalar/object destination: replace with clone of source
            context.NodeAdapter.Replace(destination, cloned);
        }

        context.LogInfo(CoreConstants.CommandExecution,
            $"{CommandName}: copied from '{srcPath}' to '{dstPath}'");
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(FromPath)) result.AddError($"FromPath property for {CommandName} command is missing");
        if (string.IsNullOrWhiteSpace(ToPath)) result.AddError($"ToPath property for {CommandName} command is missing");
        return result;
    }
}
