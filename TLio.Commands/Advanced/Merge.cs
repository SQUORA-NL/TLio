using TLio.Commands.Advanced.Settings;
using TLio.Commands.Logic;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands.Advanced;

/// <summary>
/// Deep-merges the node(s) at Path into the node(s) at TargetPath.
///
/// Ported from JLio's Merge command. All merge logic lives in this command and
/// runs exclusively through <see cref="INodeAdapter{TNode}"/> / IItemsFetcher, so
/// the exact same behaviour applies to JSON (Newtonsoft + System.Text.Json),
/// XML and YAML.
///
/// Two levels of configuration:
///   • <see cref="ArrayMergeMode"/> — coarse array behaviour (concat / replace).
///   • <see cref="Settings"/>       — per-array key matching, deduplication,
///                                    root-level match keys and merge strategy.
///
/// Without <see cref="Settings"/> the command behaves exactly as before.
/// </summary>
public class Merge<TNode> : CommandBase<TNode>
{
    public override string CommandName => "merge";

    public string? Path { get; set; }
    public string? TargetPath { get; set; }
    public ArrayMergeMode ArrayMergeMode { get; set; } = ArrayMergeMode.Concat;

    /// <summary>Fine-grained merge configuration (array keys, strategy, match keys).</summary>
    public MergeSettings Settings { get; set; } = MergeSettings.CreateDefault();

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

    public Merge(string path, string targetPath, MergeSettings settings)
    {
        Path = path;
        TargetPath = targetPath;
        Settings = settings ?? MergeSettings.CreateDefault();
    }

    public Merge(string path, string targetPath, ArrayMergeMode arrayMergeMode, MergeSettings settings)
    {
        Path = path;
        TargetPath = targetPath;
        ArrayMergeMode = arrayMergeMode;
        Settings = settings ?? MergeSettings.CreateDefault();
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

        // A path may be an =indirect() expression; the raw '=' would throw out of the fetcher.
        var resolvedPath = IndirectPath.TryResolve(Path!, dataContext, context, CommandName);
        var resolvedTargetPath = resolvedPath == null
            ? null
            : IndirectPath.TryResolve(TargetPath!, dataContext, context, CommandName, "targetPath");
        if (resolvedPath == null || resolvedTargetPath == null)
        {
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: an =indirect() path expression could not be resolved; nothing merged."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var sources = context.ItemsFetcher.SelectNodes(resolvedPath, dataContext);
        if (sources.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no nodes at source path '{Path}'");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: source path '{Path}' matched 0 nodes; nothing merged."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var targets = context.ItemsFetcher.SelectNodes(resolvedTargetPath, dataContext);
        if (targets.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no nodes at target path '{TargetPath}'");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, TargetPath ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: target path '{TargetPath}' matched 0 nodes; nothing merged."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var settings = Settings ?? MergeSettings.CreateDefault();
        foreach (var source in sources)
            foreach (var target in targets)
                MergeElements(source, target, settings, context);

        context.LogInfo(CoreConstants.CommandExecution,
            $"{CommandName}: merged {sources.Count} source(s) into {targets.Count} target(s) [strategy={settings.Strategy}]");
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path ?? "", TraceOutcome.Success, sources.Count,
            $"{CommandName}: merged {sources.Count} source(s) from '{Path}' into {targets.Count} target(s) at '{TargetPath}' [strategy={settings.Strategy}]."));
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"{CommandName}: Path is required.");
        if (string.IsNullOrWhiteSpace(TargetPath))
            result.AddError($"{CommandName}: TargetPath is required.");
        if (!string.IsNullOrWhiteSpace(Path) && !string.IsNullOrWhiteSpace(TargetPath) &&
            string.Equals(Path, TargetPath, StringComparison.OrdinalIgnoreCase))
            result.AddError($"{CommandName}: Path and TargetPath cannot be the same.");
        return result;
    }

    // ── Merge engine ──────────────────────────────────────────────────────────

    private void MergeElements(TNode source, TNode target,
        MergeSettings settings, IExecutionContext<TNode> context)
    {
        var adapter = context.NodeAdapter;

        if (source == null || target == null)
            return;

        if (ShouldMergeAsArray(source, target, settings, context))
            MergeArrays(source, target, settings, context);
        else if (adapter.IsObject(source) && adapter.IsObject(target))
            MergeObjects(source, target, settings, context);
        else
            adapter.Replace(target, adapter.DeepClone(source));
    }

    /// <summary>
    /// Merge the properties of <paramref name="source"/> into <paramref name="target"/>.
    /// Honours <see cref="MergeSettings.MatchSettings"/> and the merge strategy.
    /// </summary>
    private void MergeObjects(TNode source, TNode target,
        MergeSettings settings, IExecutionContext<TNode> context)
    {
        var adapter = context.NodeAdapter;

        // Root-level match keys: objects are only merged when all key fields match.
        if (settings.MatchSettings.HasKeys &&
            !MergeArrayHelpers.AllKeysMatch(adapter, context.ItemsFetcher, target, source, settings.MatchSettings.KeyPaths))
            return;

        foreach (var propertyName in adapter.GetPropertyNames(source).Distinct().ToList())
        {
            var sourceValue = adapter.GetProperty(source, propertyName);

            if (!adapter.HasProperty(target, propertyName))
            {
                // Property is new on the target — "onlyValues" never adds structure.
                if (!settings.IsOnlyValues)
                    adapter.SetProperty(target, propertyName, Clone(adapter, sourceValue));
                continue;
            }

            var targetValue = adapter.GetProperty(target, propertyName);

            if (sourceValue != null && targetValue != null &&
                ShouldMergeAsArray(sourceValue, targetValue, settings, context))
            {
                MergeArrays(sourceValue, targetValue, settings, context);
            }
            else if (sourceValue != null && targetValue != null &&
                     adapter.IsObject(sourceValue) && adapter.IsObject(targetValue))
            {
                MergeObjects(sourceValue, targetValue, settings, context);
            }
            else if (!settings.IsOnlyStructure)
            {
                // Existing value is overwritten — "onlyStructure" keeps the target value.
                adapter.SetProperty(target, propertyName, Clone(adapter, sourceValue));
            }
        }
    }

    /// <summary>
    /// Merge two arrays. Key-based matching (per-array settings) takes precedence
    /// over <see cref="ArrayMergeMode"/>; without keys the mode decides between
    /// replace and append, with optional deduplication.
    /// </summary>
    private void MergeArrays(TNode source, TNode target,
        MergeSettings settings, IExecutionContext<TNode> context)
    {
        var adapter = context.NodeAdapter;
        var arraySettings = FindArraySettings(target, settings, context) ?? new MergeArraySettings();
        var sourceItems = adapter.GetArrayElements(source).ToList();

        if (arraySettings.HasKeys)
        {
            foreach (var sourceItem in sourceItems)
                MergeArrayItemByKey(sourceItem, target, arraySettings, settings, context);
            return;
        }

        if (ArrayMergeMode == ArrayMergeMode.Replace)
        {
            for (var i = adapter.GetArrayLength(target) - 1; i >= 0; i--)
                adapter.RemoveFromArray(target, i);

            foreach (var sourceItem in sourceItems)
                adapter.AppendToArray(target, Clone(adapter, sourceItem));
            return;
        }

        // MergeByKey without configured key paths falls back to whole-item matching:
        // items already present in the target are not appended a second time.
        var deduplicate = arraySettings.UniqueItemsWithoutKeys ||
                          ArrayMergeMode == ArrayMergeMode.MergeByKey;

        foreach (var sourceItem in sourceItems)
        {
            if (deduplicate &&
                MergeArrayHelpers.IsItemInArray(adapter, adapter.GetArrayElements(target).ToList(), sourceItem))
                continue;

            adapter.AppendToArray(target, Clone(adapter, sourceItem));
        }
    }

    /// <summary>
    /// Append <paramref name="sourceItem"/> when no target element matches on the
    /// configured key paths, otherwise merge it into every matching element.
    /// </summary>
    private void MergeArrayItemByKey(TNode sourceItem, TNode target,
        MergeArraySettings arraySettings, MergeSettings settings, IExecutionContext<TNode> context)
    {
        var adapter = context.NodeAdapter;
        // Re-read on every item so elements appended by earlier items participate
        // in matching (matches JLio, which matches against the live target array).
        var targetItems = adapter.GetArrayElements(target).ToList();
        var matches = MergeArrayHelpers.FindMatchingElementIndexes(
            adapter, context.ItemsFetcher, targetItems, sourceItem, arraySettings.KeyPaths);

        if (matches.Count == 0)
        {
            adapter.AppendToArray(target, Clone(adapter, sourceItem));
            return;
        }

        foreach (var index in matches)
        {
            var targetItem = targetItems[index];

            // Complex elements are merged recursively — MergeElements decides between
            // object and array semantics for the pair.
            if (sourceItem != null && targetItem != null &&
                ((adapter.IsObject(sourceItem) && adapter.IsObject(targetItem)) ||
                 (adapter.IsArray(sourceItem) && adapter.IsArray(targetItem))))
            {
                MergeElements(sourceItem, targetItem, settings, context);
                continue;
            }

            // Primitive (or type-mismatched) element: replace by index. Adapter.Replace
            // cannot always locate an array element (YAML tracks parents separately),
            // so the element is swapped positionally instead.
            if (!settings.IsOnlyStructure)
            {
                adapter.RemoveFromArray(target, index);
                adapter.InsertIntoArray(target, index, Clone(adapter, sourceItem));
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Decide whether the pair should be merged with array semantics.
    ///
    /// JSON and YAML distinguish arrays from objects natively. XML does not — an
    /// element holding one repeated child name looks like both. For that ambiguous
    /// case array semantics are only used when the script explicitly declared array
    /// settings for the target path, which keeps existing XML merges unchanged.
    /// </summary>
    private bool ShouldMergeAsArray(TNode source, TNode target,
        MergeSettings settings, IExecutionContext<TNode> context)
    {
        var adapter = context.NodeAdapter;
        if (source == null || target == null) return false;
        if (!adapter.IsArray(source) || !adapter.IsArray(target)) return false;
        if (!adapter.IsObject(source) || !adapter.IsObject(target)) return true;
        return FindArraySettings(target, settings, context) != null;
    }

    /// <summary>Find the per-array settings whose ArrayPath points at the target array.</summary>
    private static MergeArraySettings? FindArraySettings(TNode targetArray,
        MergeSettings settings, IExecutionContext<TNode> context)
    {
        if (settings.ArraySettings.Count == 0) return null;

        string actualPath;
        try { actualPath = context.ItemsFetcher.GetPath(targetArray); }
        catch { return null; }

        return settings.ArraySettings.FirstOrDefault(
            a => PathsMatch(a.ArrayPath, actualPath, context.ItemsFetcher));
    }

    /// <summary>
    /// Compare a configured array path with an actual path, ignoring the root
    /// indicator so "$.items", "items" and "/items" are equivalent across formats.
    /// </summary>
    internal static bool PathsMatch(string configuredPath, string actualPath,
        IItemsFetcher<TNode> fetcher)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return false;
        if (string.Equals(configuredPath, actualPath, StringComparison.Ordinal)) return true;
        return string.Equals(Normalize(configuredPath, fetcher), Normalize(actualPath, fetcher),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Reduces a path to the part that names nodes, so a configured array path and the path the
    /// fetcher reports for the array compare equal even when one carries a root marker and the
    /// other does not.
    ///
    /// Both sides go through this, so it is a canonical form rather than a conversion between
    /// languages. The tokens come from the fetcher — it used to strip a literal "$" and rewrite
    /// "/" to ".", which is one language's root marker and another's delimiter.
    /// </summary>
    private static string Normalize(string path, IItemsFetcher<TNode> fetcher)
    {
        var value = (path ?? string.Empty).Trim();

        var root = fetcher.RootPathIndicator;
        if (root.Length > 0 && value.StartsWith(root, StringComparison.Ordinal))
            value = value[root.Length..];

        var delimiter = fetcher.PathDelimiter;
        if (delimiter.Length == 0) return value;

        while (value.StartsWith(delimiter, StringComparison.Ordinal))
            value = value[delimiter.Length..];

        return value;
    }

    /// <summary>Null-tolerant clone (System.Text.Json exposes JSON null as C# null).</summary>
    private static TNode Clone(INodeAdapter<TNode> adapter, TNode? node)
        => node == null ? adapter.CreateNull() : adapter.DeepClone(node);
}
