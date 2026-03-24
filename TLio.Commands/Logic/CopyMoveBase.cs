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
/// - DestinationAsArray: wraps the destination value in an array before assigning
/// - Array-index alignment: when FromPath and ToPath reference the same array index,
///   operations are paired (one source → one target)
/// - Move variant removes the source node after copying
/// </summary>
public abstract class CopyMoveBase<TNode> : CommandBase<TNode>
{
    public string? FromPath { get; set; }
    public string? ToPath { get; set; }
    public bool DestinationAsArray { get; set; } = false;

    protected abstract bool IsMove { get; }

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        // Resolve indirect path expressions in ToPath
        var resolvedToPath = context.ItemsFetcher.ProcessIndirectPath(ToPath!, dataContext) ?? ToPath!;

        var sources = context.ItemsFetcher.SelectNodes(FromPath!, dataContext);
        if (sources.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no nodes found at FromPath '{FromPath}'");
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        // Special case: destination is root → deep merge all sources into root
        if (resolvedToPath == context.ItemsFetcher.RootPathIndicator)
        {
            foreach (var source in sources)
                context.NodeAdapter.DeepMergeInto(context.NodeAdapter.DeepClone(source), dataContext);

            if (IsMove)
                foreach (var source in sources)
                    context.NodeAdapter.RemoveFromParent(source);

            context.LogInfo(CoreConstants.CommandExecution, $"{CommandName}: merged {sources.Count} node(s) into root");
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

        // Determine alignment: one-to-one (aligned array indices) vs many-to-many
        bool aligned = sources.Count == destinations.Count && sources.Count > 1;

        if (aligned)
        {
            for (int i = 0; i < sources.Count; i++)
                ApplyCopy(sources[i], destinations[i], resolvedToPath, dataContext, context);
        }
        else
        {
            foreach (var source in sources)
                foreach (var destination in destinations)
                    ApplyCopy(source, destination, resolvedToPath, dataContext, context);
        }

        if (IsMove)
            foreach (var source in sources)
                context.NodeAdapter.RemoveFromParent(source);

        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    private void ApplyCopy(TNode source, TNode destination, string resolvedToPath, TNode dataContext, IExecutionContext<TNode> context)
    {
        var cloned = context.NodeAdapter.DeepClone(source);

        if (DestinationAsArray && !context.NodeAdapter.IsArray(destination))
        {
            // Wrap in array
            var arr = context.NodeAdapter.CreateArray();
            context.NodeAdapter.AppendToArray(arr, cloned);
            context.NodeAdapter.Replace(destination, arr);
        }
        else if (DestinationAsArray && context.NodeAdapter.IsArray(destination))
        {
            context.NodeAdapter.AppendToArray(destination, cloned);
        }
        else
        {
            context.NodeAdapter.Replace(destination, cloned);
        }

        context.LogInfo(CoreConstants.CommandExecution,
            $"{CommandName}: copied from '{context.ItemsFetcher.GetPath(source)}' to '{context.ItemsFetcher.GetPath(destination)}'");
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(FromPath)) result.AddError($"{CommandName}: FromPath is required.");
        if (string.IsNullOrWhiteSpace(ToPath)) result.AddError($"{CommandName}: ToPath is required.");
        return result;
    }
}
