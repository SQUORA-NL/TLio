using TLio.Commands.Logic;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Adds a new property to an object node, or appends an element to an array node.
/// If the property already exists on an object, a warning is logged and the add is skipped.
/// Mirrors JLio's Add command exactly.
/// </summary>
public class Add<TNode> : PropertyChangeCommand<TNode>
{
    public Add() { }

    public Add(string path, IFunctionSupportedValue<TNode> value)
    {
        Path = path;
        Value = value;
    }

    public Add(string path, string property, IFunctionSupportedValue<TNode> value)
    {
        Path = path;
        Property = property;
        Value = value;
    }

    protected override bool EnsureFullPathForLeaf => false;

    protected override void ApplyValueToTarget(
        string propertyName, TNode targetNode, TNode value,
        TNode dataContext, IExecutionContext<TNode> context)
    {
        if (context.NodeAdapter.IsObject(targetNode) &&
            context.NodeAdapter.HasProperty(targetNode, propertyName))
        {
            // JLio Add logs a warning and skips when property already exists
            context.LogWarning(TLio.Core.CoreConstants.CommandExecution,
                $"{CommandName}: property '{propertyName}' already exists, skipping");
            return;
        }

        AddProperty(propertyName, targetNode, value, context);
    }

    /// <summary>
    /// A path that resolved to an array position found an element already sitting there, and
    /// add never overwrites what exists — the same rule it applies to a property that is
    /// already present. Shifting the later elements aside is what an insert command would mean.
    /// </summary>
    protected override void ApplyValueToNode(TNode target, TNode value, IExecutionContext<TNode> context)
        => context.LogWarning(TLio.Core.CoreConstants.CommandExecution,
            $"{CommandName}: array element at '{Path}' already exists, skipping");

    /// <summary>
    /// Nothing is at that position yet.
    ///
    /// In an array the one position a new element can take without leaving a hole behind it is
    /// the one just past the end, so that is the only missing index add will create — filling
    /// an array in order works one command at a time, and <c>add $.tags[0]</c> on an empty
    /// array appends. A position further out is refused rather than quietly appended: the
    /// element would end up at an index the path did not name.
    ///
    /// When the array itself is not there, position 0 creates it — the same thing add already
    /// does for the objects along a path like <c>$.address.city</c>.
    /// </summary>
    protected override void ApplyValueToMissingIndex(TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!context.ItemsFetcher.TrySplitArrayIndex(Path!, out var arrayPath, out var index))
            return;

        var adapter = context.NodeAdapter;
        var array = context.ItemsFetcher.SelectNode(arrayPath, dataContext);

        if (array is null)
        {
            if (index != 0)
            {
                Warn(context, $"'{arrayPath}' does not exist, so the only position that can be added is 0");
                return;
            }

            array = CreateArrayAt(arrayPath, dataContext, context);
            if (array is null)
            {
                Warn(context, $"'{arrayPath}' does not exist and could not be created");
                return;
            }
        }
        else
        {
            // A freshly created array is empty, and in XML an empty element is not yet
            // distinguishable from an empty object — so this check only applies to an array
            // that was already in the document.
            if (!adapter.IsArray(array))
            {
                Warn(context, $"'{arrayPath}' is not an array");
                return;
            }

            var length = adapter.GetArrayLength(array);
            if (index != length)
            {
                Warn(context, $"'{arrayPath}' holds {length} element(s), so the next position that can be added is {length}");
                return;
            }
        }

        var valueResult = Value!.GetValue(array, dataContext, context);
        if (!valueResult.Success)
        {
            MarkFailed();
            return;
        }

        adapter.AppendToArray(array, valueResult.Data.First ?? adapter.CreateNull());
    }

    /// <summary>
    /// Builds an empty array at <paramref name="arrayPath"/>, creating the objects above it,
    /// and returns it. Null when the containing path cannot be built or does not hold properties.
    /// </summary>
    private TNode? CreateArrayAt(string arrayPath, TNode dataContext, IExecutionContext<TNode> context)
    {
        var (parentPath, leafName) = context.ItemsFetcher.SplitParentAndLeaf(arrayPath);
        if (string.IsNullOrEmpty(leafName)) return default;

        context.ItemsFetcher.EnsurePath(parentPath, dataContext, context.NodeAdapter);

        var parent = context.ItemsFetcher.SelectNode(parentPath, dataContext);
        if (parent is null || !context.NodeAdapter.IsObject(parent)) return default;

        context.NodeAdapter.SetProperty(parent, leafName, context.NodeAdapter.CreateArray());
        return context.ItemsFetcher.SelectNode(arrayPath, dataContext);
    }

    /// <summary>
    /// Reported as "no nodes matched" so the trace records a no-op, the same outcome every
    /// other path that finds nothing produces.
    /// </summary>
    private void Warn(IExecutionContext<TNode> context, string detail)
        => context.LogWarning(TLio.Core.CoreConstants.CommandExecution,
            $"{CommandName}: no nodes matched path '{Path}' — {detail}");
}
