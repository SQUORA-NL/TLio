using Microsoft.Extensions.Logging;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands.Logic;

/// <summary>
/// Generic base class for Add, Set, and Put — the three commands that write a
/// value to a node addressed by a path expression.
///
/// Mirrors JLio's PropertyChangeCommand but all format-specific operations are
/// delegated to INodeAdapter&lt;TNode&gt; and IItemsFetcher&lt;TNode&gt;.
///
/// Supports both:
///   - Legacy syntax: path contains the full path to the property (e.g. "$.a.b")
///   - New syntax:   path selects target objects; Property names the field (e.g. path="$.items[*]", property="name")
/// </summary>
public abstract class PropertyChangeCommand<TNode> : CommandBase<TNode>
{
    public string? Path { get; set; }

    /// <summary>
    /// Optional. When set, the path selects the target objects and this property
    /// name is the field within each object to operate on (new syntax).
    /// When null, the property name is derived from the last element of Path (legacy syntax).
    /// </summary>
    public string? Property { get; set; }

    public IFunctionSupportedValue<TNode>? Value { get; set; }

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

        var logsBefore = context.GetLogEntries().Count;

        if (Property != null)
            ExecuteNewSyntax(dataContext, context);
        else
            ExecuteLegacySyntax(dataContext, context);

        if (IsSuccessful)
            context.LogInfo(CoreConstants.CommandExecution, $"{CommandName}: completed successfully on path '{Path}'");

        var newLogs = context.GetLogEntries().Skip(logsBefore).ToList();
        var hasNoMatchWarning = newLogs.Any(e => e.Level == LogLevel.Warning &&
                      (e.Message.Contains("no nodes matched") ||
                       (e.Message.Contains("property '") && e.Message.Contains("' not found")) ||
                       e.Message.Contains("already exists, skipping")));
        var traceOutcome = !IsSuccessful ? TraceOutcome.Failure
                         : hasNoMatchWarning ? TraceOutcome.NoOp
                         : TraceOutcome.Success;
        var failureDetail = traceOutcome == TraceOutcome.Failure
            ? string.Join("; ", newLogs.Where(e => e.Level == LogLevel.Warning || e.Level == LogLevel.Error).Select(e => e.Message))
            : "";
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path ?? "", traceOutcome,
            traceOutcome == TraceOutcome.Success ? 1 : 0,
            traceOutcome == TraceOutcome.NoOp
                ? $"{CommandName}: path '{Path}' matched 0 nodes — field does not exist at this location. " +
                  $"Use 'set' to update an existing field or 'add' to create a new one. " +
                  $"Call tlio_analyze to see the exact paths that require changes."
                : traceOutcome == TraceOutcome.Failure
                ? $"{CommandName}: failed at '{Path}'" + (string.IsNullOrEmpty(failureDetail) ? "." : $" — {failureDetail}.")
                : $"{CommandName}: successfully applied to '{Path}'."));

        return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
    }

    // ── New syntax ────────────────────────────────────────────────────────────
    // path selects target objects; Property is the field name on each

    private void ExecuteNewSyntax(TNode dataContext, IExecutionContext<TNode> context)
    {
        var targets = context.ItemsFetcher.SelectNodes(Path!, dataContext);
        if (targets.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no nodes matched path '{Path}'");
            return;
        }

        foreach (var target in targets)
        {
            var valueResult = Value!.GetValue(target, dataContext, context);
            if (!valueResult.Success)
            {
                MarkFailed();
                continue;
            }
            var computedValue = valueResult.Data.First ?? context.NodeAdapter.CreateNull();
            ApplyValueToTarget(Property!, target, computedValue, dataContext, context);
        }
    }

    // ── Legacy syntax ─────────────────────────────────────────────────────────
    // property name is the leaf element of Path; parent is the target object

    private void ExecuteLegacySyntax(TNode dataContext, IExecutionContext<TNode> context)
    {
        // The root has no leaf to split off: SplitParentAndLeaf yields an empty name (XML "/")
        // or echoes the root indicator back (JSON "$"), and both are meaningless as property
        // names — treating them as one produced an invalid-XML-name crash or a literal "$"
        // property. The root is reachable through the 'property' field or a copy/move to root.
        if (string.IsNullOrEmpty(Path) || Path == context.ItemsFetcher.RootPathIndicator)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: path '{Path}' targets the document root, which has no property name to " +
                $"{CommandName} — name a child via the 'property' field, or use copy/move with " +
                $"toPath '{context.ItemsFetcher.RootPathIndicator}' to replace the whole document");
            return;
        }

        var (parentPath, propertyName) = context.ItemsFetcher.SplitParentAndLeaf(Path!);

        // When the leaf is reached directly via recursive descent (e.g. "$..myArray"),
        // select the leaf nodes by the full path and replace each one in-place.
        // Mirrors JLio's PropertyChangeCommand.AddToObjectItems IsSearchingForObjectsByName branch.
        if (context.ItemsFetcher.IsLeafRecursiveDescentSearch(Path!))
        {
            var targets = context.ItemsFetcher.SelectNodes(Path!, dataContext);
            foreach (var target in targets)
            {
                var valueResult = Value!.GetValue(target, dataContext, context);
                if (!valueResult.Success) { MarkFailed(); continue; }
                var computedValue = valueResult.Data.First ?? context.NodeAdapter.CreateNull();
                context.NodeAdapter.Replace(target, computedValue);
            }
            return;
        }

        // A trailing [n] addresses a position in an array, not a property of the parent object.
        // Split off as a leaf name it becomes "items[1]", which no property is called — set
        // reported it as not found, and put created a property literally named "items[1]"
        // alongside the array it was meant to edit. The element is what the path names, so
        // select it and write to it directly.
        if (context.ItemsFetcher.IsLeafArrayIndex(Path!))
        {
            var elements = context.ItemsFetcher.SelectNodes(Path!, dataContext);
            if (elements.Count == 0)
            {
                ApplyValueToMissingIndex(dataContext, context);
                return;
            }

            foreach (var element in elements)
            {
                var indexedValue = Value!.GetValue(element, dataContext, context);
                if (!indexedValue.Success)
                {
                    MarkFailed();
                    continue;
                }
                ApplyValueToNode(element,
                    indexedValue.Data.First ?? context.NodeAdapter.CreateNull(), context);
            }
            return;
        }

        // Resolve any =indirect() in parentPath. An unresolved expression must not be passed on:
        // the raw '=' is not legal in any path language and would throw out of the whole script.
        var resolvedParentPath = IndirectPath.TryResolve(parentPath, dataContext, context, CommandName);
        if (resolvedParentPath == null)
            return;

        var parents = context.ItemsFetcher.SelectNodes(resolvedParentPath, dataContext);
        if (parents.Count == 0)
        {
            if (!CreatesMissingPath)
            {
                // Set never invents structure: a path that does not exist is a no-op.
                // Warn and continue so the rest of the script still runs.
                context.LogWarning(CoreConstants.CommandExecution,
                    $"{CommandName}: no nodes matched path '{Path}' — nothing changed");
                return;
            }

            // Add uses the parent path so EnsurePath stops at the parent, leaving the leaf
            // absent — Add.ApplyValueToTarget will then create it via SetProperty.
            // Put uses the full path so EnsurePath also creates the leaf placeholder,
            // which UpsertProperty can find and Replace() with the actual value.
            var ensurePath = EnsureFullPathForLeaf ? Path! : resolvedParentPath;
            context.ItemsFetcher.EnsurePath(ensurePath, dataContext, context.NodeAdapter);
            parents = context.ItemsFetcher.SelectNodes(resolvedParentPath, dataContext);

            if (parents.Count == 0)
            {
                // Not every path can be built — one that runs through an array position which
                // does not exist yet describes a shape there is no unambiguous way to make. The
                // command used to fall through the loop below with nothing to iterate and report
                // success, so a script that changed nothing looked like it had worked.
                context.LogWarning(CoreConstants.CommandExecution,
                    $"{CommandName}: no nodes matched path '{Path}' — the path could not be created");
                return;
            }
        }

        foreach (var parent in parents)
        {
            var valueResult = Value!.GetValue(parent, dataContext, context);
            if (!valueResult.Success)
            {
                MarkFailed();
                continue;
            }
            var computedValue = valueResult.Data.First ?? context.NodeAdapter.CreateNull();
            ApplyValueToTarget(propertyName, parent, computedValue, dataContext, context);
        }
    }

    // ── Template method ───────────────────────────────────────────────────────

    /// <summary>
    /// When true (default), EnsurePath uses the full path including the leaf segment,
    /// creating a placeholder node that ReplaceProperty can then Replace().
    /// Add overrides this to false: EnsurePath stops at the parent so the leaf property
    /// remains absent and AddProperty can create it via SetProperty without skipping.
    /// </summary>
    protected virtual bool EnsureFullPathForLeaf => true;

    /// <summary>
    /// When true (default), a path whose parent matches no nodes is created via EnsurePath
    /// before the value is applied — the upsert behaviour of Add and Put.
    /// Set overrides this to false: it only ever updates nodes that already exist, so a
    /// missing path logs a warning and leaves the document untouched instead of
    /// fabricating the structure the path describes.
    /// </summary>
    protected virtual bool CreatesMissingPath => true;

    /// <summary>
    /// The array position the path names holds nothing.
    ///
    /// The commands that build what is missing — Add and Put — create the element when the path
    /// names the one position that can be created: the one just past the end, which is where a
    /// new element goes without leaving a hole behind it. When the array itself is not there,
    /// position 0 creates it, the same thing these commands already do for the objects along a
    /// path like <c>$.address.city</c>.
    ///
    /// Any position further out is refused rather than quietly appended — the element would end
    /// up at an index the path did not name. Set never builds anything, so it always warns.
    /// </summary>
    protected virtual void ApplyValueToMissingIndex(TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!CreatesMissingPath)
        {
            WarnNoIndex(context, "the array has no element at that position");
            return;
        }

        if (!context.ItemsFetcher.TrySplitArrayIndex(Path!, out var arrayPath, out var index))
            return;

        var adapter = context.NodeAdapter;
        var array = context.ItemsFetcher.SelectNode(arrayPath, dataContext);

        if (array is null)
        {
            if (index != 0)
            {
                WarnNoIndex(context, $"'{arrayPath}' does not exist, so the only position that can be added is 0");
                return;
            }

            array = CreateArrayAt(arrayPath, dataContext, context);
            if (array is null)
            {
                WarnNoIndex(context, $"'{arrayPath}' does not exist and could not be created");
                return;
            }
        }
        else
        {
            // A freshly created array is empty, and in XML an empty element is not yet
            // distinguishable from an empty object — so this only applies to an array that
            // was already in the document.
            if (!adapter.IsArray(array))
            {
                WarnNoIndex(context, $"'{arrayPath}' is not an array");
                return;
            }

            var length = adapter.GetArrayLength(array);
            if (index != length)
            {
                WarnNoIndex(context,
                    $"'{arrayPath}' holds {length} element(s), so the next position that can be added is {length}");
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
    private void WarnNoIndex(IExecutionContext<TNode> context, string detail)
        => context.LogWarning(CoreConstants.CommandExecution,
            $"{CommandName}: no nodes matched path '{Path}' — {detail}");

    /// <summary>
    /// Apply <paramref name="value"/> to a node the path addressed directly — an array element
    /// reached through a subscript — rather than to a named property of its parent.
    ///
    /// The default replaces the element's value, which is what <c>set</c> and <c>put</c> mean
    /// at a position that exists. <c>Add</c> overrides it: something is already there.
    /// </summary>
    protected virtual void ApplyValueToNode(TNode target, TNode value, IExecutionContext<TNode> context)
        => context.NodeAdapter.Replace(target, value);

    /// <summary>
    /// Apply <paramref name="value"/> to <paramref name="propertyName"/> on
    /// <paramref name="targetNode"/>. Subclasses implement the specific semantics
    /// (add-only, replace-only, or upsert).
    /// </summary>
    protected abstract void ApplyValueToTarget(
        string propertyName,
        TNode targetNode,
        TNode value,
        TNode dataContext,
        IExecutionContext<TNode> context);

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected void AddProperty(string propertyName, TNode targetNode, TNode value, IExecutionContext<TNode> context)
    {
        if (context.NodeAdapter.IsObject(targetNode))
            context.NodeAdapter.SetProperty(targetNode, propertyName, value);
        else if (context.NodeAdapter.IsArray(targetNode))
            context.NodeAdapter.AppendToArray(targetNode, value);
        else
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: cannot add property to a primitive node");
    }

    protected void ReplaceProperty(string propertyName, TNode targetNode, TNode value, IExecutionContext<TNode> context)
    {
        if (context.NodeAdapter.IsObject(targetNode))
        {
            // Use HasProperty to distinguish "property not found" from "property exists with null value".
            // In System.Text.Json, GetProperty returns C# null for JSON-null-valued properties,
            // so checking HasProperty first prevents false "not found" warnings.
            if (!context.NodeAdapter.HasProperty(targetNode, propertyName))
            {
                context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: property '{propertyName}' not found");
                return;
            }
            var existing = context.NodeAdapter.GetProperty(targetNode, propertyName);
            if (existing == null)
                context.NodeAdapter.SetProperty(targetNode, propertyName, value);
            else
                context.NodeAdapter.Replace(existing, value);
        }
        else if (context.NodeAdapter.IsArray(targetNode))
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: cannot set a named property on an array node");
        }
    }

    protected void UpsertProperty(string propertyName, TNode targetNode, TNode value, IExecutionContext<TNode> context)
    {
        if (context.NodeAdapter.IsObject(targetNode))
        {
            context.NodeAdapter.SetProperty(targetNode, propertyName, value);
        }
        else if (context.NodeAdapter.IsArray(targetNode))
        {
            // For Put on an array: replace entire array contents
            var elements = context.NodeAdapter.GetArrayElements(targetNode).ToList();
            for (int i = elements.Count - 1; i >= 0; i--)
                context.NodeAdapter.RemoveFromArray(targetNode, i);
            context.NodeAdapter.AppendToArray(targetNode, value);
        }
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"Path property for {CommandName} command is missing");
        if (Value is null)
            result.AddError($"Value property for {CommandName} command is missing");
        return result;
    }
}
