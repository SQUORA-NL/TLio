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

        // Resolve any =indirect() in parentPath
        var resolvedParentPath = context.ItemsFetcher.ProcessIndirectPath(parentPath, dataContext) ?? parentPath;

        var parents = context.ItemsFetcher.SelectNodes(resolvedParentPath, dataContext);
        if (parents.Count == 0)
        {
            // Add uses the parent path so EnsurePath stops at the parent, leaving the leaf
            // absent — Add.ApplyValueToTarget will then create it via SetProperty.
            // Set/Put use the full path so EnsurePath also creates the leaf placeholder,
            // which ReplaceProperty can find and Replace() with the actual value.
            var ensurePath = EnsureFullPathForLeaf ? Path! : resolvedParentPath;
            context.ItemsFetcher.EnsurePath(ensurePath, dataContext, context.NodeAdapter);
            parents = context.ItemsFetcher.SelectNodes(resolvedParentPath, dataContext);
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
