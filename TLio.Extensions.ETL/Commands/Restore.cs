using System.Globalization;
namespace TLio.Extensions.ETL.Commands;

/// <summary>
/// Reconstructs a nested node from a previously flattened one, using the
/// FlattenMetadata stored in the document to know which keys should be arrays.
///
/// Without metadata (best-effort mode), restores using the configured delimiter
/// and skips keys that start with "_".
///
/// Ported from JLio.Extensions.ETL.Commands.Restore. All data manipulation
/// uses INodeAdapter — no format-specific code.
/// </summary>
public class Restore<TNode> : CommandBase<TNode>
{
    public override string CommandName => "restore";

    public string Path { get; set; } = "$";
    public RestoreSettings RestoreSettings { get; set; } = new();

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path, TraceOutcome.Failure, 0,
                $"restore: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var restoreCount = 0;
        try
        {
            var targets = context.ItemsFetcher.SelectNodes(Path, dataContext).ToList();
            foreach (var target in targets)
            {
                if (!context.NodeAdapter.IsObject(target))
                {
                    context.LogWarning(CoreConstants.CommandExecution,
                        $"restore: can only restore objects, found non-object at path '{Path}'");
                    continue;
                }

                var metadata = GetMetadata(target, dataContext, context);

                if (metadata == null && RestoreSettings.StrictMode)
                {
                    context.LogError(CoreConstants.CommandExecution,
                        "restore: no flatten metadata found and strict mode is enabled");
                    context.TraceCollector?.Record(new TraceEntry(
                        CommandName, Path, TraceOutcome.Failure, 0,
                        "restore: no flatten metadata found and strict mode is enabled."));
                    return TLioExecutionResult<TNode>.Failed(dataContext);
                }

                TNode restored;
                if (metadata != null)
                    restored = RestoreFromFlat(target, metadata, context.NodeAdapter);
                else
                {
                    context.LogInfo(CoreConstants.CommandExecution,
                        "restore: attempting restore without metadata (best-effort)");
                    restored = RestoreWithoutMetadata(target, context.NodeAdapter);
                }

                context.NodeAdapter.Replace(target, restored);
                restoreCount++;

                if (RestoreSettings.RemoveMetadata && metadata != null)
                    RemoveMetadata(dataContext, context);
            }
        }
        catch (Exception ex)
        {
            context.LogError(CoreConstants.CommandExecution, $"restore: {ex.Message}");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path, TraceOutcome.Failure, 0,
                $"restore: error — {ex.Message}"));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path,
            restoreCount == 0 ? TraceOutcome.NoOp : TraceOutcome.Success,
            restoreCount,
            restoreCount == 0
                ? $"restore: path '{Path}' matched 0 nodes; nothing restored."
                : $"restore: restored {restoreCount} node(s) at '{Path}'."));
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    // ── Metadata retrieval ────────────────────────────────────────────────────

    private FlattenMetadata? GetMetadata(
        TNode flatObj, TNode dataContext, IExecutionContext<TNode> context)
    {
        try
        {
            TNode? metaNode;
            if (string.IsNullOrEmpty(RestoreSettings.MetadataPath))
                metaNode = context.NodeAdapter.GetProperty(flatObj, RestoreSettings.MetadataKey);
            else
            {
                var container = context.ItemsFetcher.SelectNode(RestoreSettings.MetadataPath, dataContext);
                metaNode = container != null
                    ? context.NodeAdapter.GetProperty(container, RestoreSettings.MetadataKey)
                    : default;
            }
            return metaNode != null ? Flatten<TNode>.DeserializeMetadata(metaNode, context.NodeAdapter) : null;
        }
        catch (Exception ex)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"restore: failed to read metadata: {ex.Message}");
            return null;
        }
    }

    private void RemoveMetadata(TNode dataContext, IExecutionContext<TNode> context)
    {
        try
        {
            if (string.IsNullOrEmpty(RestoreSettings.MetadataPath))
                context.NodeAdapter.RemoveProperty(dataContext, RestoreSettings.MetadataKey);
            else
            {
                var container = context.ItemsFetcher.SelectNode(RestoreSettings.MetadataPath, dataContext);
                if (container != null && context.NodeAdapter.IsObject(container))
                    context.NodeAdapter.RemoveProperty(container, RestoreSettings.MetadataKey);
            }
        }
        catch (Exception ex)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"restore: failed to remove metadata: {ex.Message}");
        }
    }

    // ── Restore with metadata ─────────────────────────────────────────────────

    private TNode RestoreFromFlat(TNode flatObj, FlattenMetadata metadata, INodeAdapter<TNode> adapter)
    {
        var result = adapter.CreateObject();
        var metaKey = metadata.MetadataKey ?? RestoreSettings.MetadataKey;
        var typeIndicator = metadata.TypeIndicator ?? "_type";
        var delimiter = metadata.Delimiter ?? ".";

        var skip = new HashSet<string>(StringComparer.Ordinal) { metaKey };
        if (metadata.PreserveTypes && !string.IsNullOrEmpty(typeIndicator))
            foreach (var k in adapter.GetPropertyNames(flatObj))
                if (k.EndsWith(typeIndicator)) skip.Add(k);

        foreach (var key in adapter.GetPropertyNames(flatObj))
        {
            if (skip.Contains(key)) continue;
            var pathParts = key.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
            SetNestedValue(result, pathParts, adapter.GetProperty(flatObj, key)!, metadata, adapter);
        }
        return result;
    }

    /// <summary>
    /// Walks the flattened key one segment at a time, creating each container as an array or an
    /// object according to the metadata, and places the value at the final segment.
    ///
    /// The final segment is the point of the whole method: when it is an array index — which is
    /// the case for every array of scalars, since a scalar has no property name after its
    /// index — the value belongs AT that position. Treating it as a property name is what turned
    /// ["a","b"] into [{"0":"a"},{"1":"b"}].
    /// </summary>
    private static void SetNestedValue(
        TNode root, string[] path, TNode value,
        FlattenMetadata? metadata, INodeAdapter<TNode> adapter)
    {
        var delimiter = metadata?.Delimiter ?? ".";
        var current = root;
        var currentIsArray = false;   // the restored root is always an object

        for (int i = 0; i < path.Length; i++)
        {
            var segment = path[i];

            if (i == path.Length - 1)
            {
                PlaceChild(current, currentIsArray, segment, value, adapter);
                return;
            }

            var childPath = string.Join(delimiter, path.Take(i + 1));
            var childIsArray = metadata?.OriginalStructure != null
                            && metadata.OriginalStructure.TryGetValue(childPath, out var kind)
                            && kind.StartsWith("array", StringComparison.Ordinal);

            current = DescendOrCreate(current, currentIsArray, segment, childIsArray, adapter);
            currentIsArray = childIsArray;
        }
    }

    // ── Container navigation ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the child of <paramref name="container"/> at <paramref name="segment"/>, creating
    /// it as an array or object first when it is not there yet. The segment addresses an array
    /// position or an object property depending on what the container is.
    /// </summary>
    private static TNode DescendOrCreate(
        TNode container, bool containerIsArray, string segment,
        bool childIsArray, INodeAdapter<TNode> adapter)
    {
        var existing = GetChild(container, containerIsArray, segment, adapter);
        if (existing != null) return existing;

        var child = childIsArray ? adapter.CreateArray() : adapter.CreateObject();
        PlaceChild(container, containerIsArray, segment, child, adapter);

        // Re-read rather than reusing the local: an adapter may attach a copy.
        return GetChild(container, containerIsArray, segment, adapter)!;
    }

    private static TNode? GetChild(
        TNode container, bool containerIsArray, string segment, INodeAdapter<TNode> adapter)
    {
        if (containerIsArray)
        {
            if (!int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                return default;
            return index < adapter.GetArrayLength(container)
                ? adapter.GetArrayElement(container, index)
                : default;
        }

        return adapter.HasProperty(container, segment) ? adapter.GetProperty(container, segment) : default;
    }

    /// <summary>
    /// Writes <paramref name="value"/> into <paramref name="container"/> under
    /// <paramref name="segment"/> — as an array element when the container is an array, and as a
    /// named property otherwise.
    /// </summary>
    private static void PlaceChild(
        TNode container, bool containerIsArray, string segment, TNode value, INodeAdapter<TNode> adapter)
    {
        if (!containerIsArray)
        {
            adapter.SetProperty(container, segment, value);
            return;
        }

        if (!int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            // A non-numeric segment under an array key — the metadata and the keys disagree.
            // Appending keeps the value rather than dropping it on the floor.
            adapter.AppendToArray(container, value);
            return;
        }

        while (adapter.GetArrayLength(container) <= index)
            adapter.AppendToArray(container, adapter.CreateNull());

        adapter.RemoveFromArray(container, index);
        adapter.InsertIntoArray(container, index, value);
    }

    // ── Restore without metadata (best-effort) ────────────────────────────────

    private TNode RestoreWithoutMetadata(TNode flatObj, INodeAdapter<TNode> adapter)
    {
        var result = adapter.CreateObject();
        var delimiter = RestoreSettings.Delimiter;

        foreach (var key in adapter.GetPropertyNames(flatObj))
        {
            if (key.StartsWith("_")) continue; // skip metadata keys
            var pathParts = key.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
            SetNestedValueBasic(result, pathParts, adapter.GetProperty(flatObj, key)!, adapter);
        }
        return result;
    }

    /// <summary>
    /// The same walk without metadata to consult, so array-ness is inferred from the keys: a
    /// container is an array when the next segment is a number. Types are lost in this mode,
    /// but the shape — including arrays of scalars — is not.
    /// </summary>
    private static void SetNestedValueBasic(
        TNode root, string[] path, TNode value, INodeAdapter<TNode> adapter)
    {
        var current = root;
        var currentIsArray = false;

        for (int i = 0; i < path.Length; i++)
        {
            var segment = path[i];

            if (i == path.Length - 1)
            {
                PlaceChild(current, currentIsArray, segment, value, adapter);
                return;
            }

            var childIsArray = int.TryParse(
                path[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

            current = DescendOrCreate(current, currentIsArray, segment, childIsArray, adapter);
            currentIsArray = childIsArray;
        }
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.ValidationMessages.Add("Path property is required for restore command");
        if (RestoreSettings == null)
            result.ValidationMessages.Add("RestoreSettings property is required for restore command");
        else
        {
            if (string.IsNullOrEmpty(RestoreSettings.Delimiter))
                result.ValidationMessages.Add("Delimiter cannot be empty in RestoreSettings");
            if (RestoreSettings.UseJsonPathColumn && string.IsNullOrEmpty(RestoreSettings.JsonPathColumn))
                result.ValidationMessages.Add("JsonPathColumn cannot be empty when UseJsonPathColumn is true");
        }
        return result;
    }
}
