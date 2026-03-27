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
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

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

                if (RestoreSettings.RemoveMetadata && metadata != null)
                    RemoveMetadata(dataContext, context);
            }
        }
        catch (Exception ex)
        {
            context.LogError(CoreConstants.CommandExecution, $"restore: {ex.Message}");
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

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

    private static void SetNestedValue(
        TNode root, string[] path, TNode value,
        FlattenMetadata? metadata, INodeAdapter<TNode> adapter)
    {
        var current = root;
        var delimiter = metadata?.Delimiter ?? ".";

        for (int i = 0; i < path.Length - 1; i++)
        {
            var segment = path[i];
            var currentPath = string.Join(delimiter, path.Take(i + 1));
            var shouldBeArray = metadata?.OriginalStructure?.ContainsKey(currentPath) == true
                             && metadata.OriginalStructure[currentPath].StartsWith("array");

            if (shouldBeArray)
            {
                if (!adapter.HasProperty(current, segment))
                    adapter.SetProperty(current, segment, adapter.CreateArray());

                var array = adapter.GetProperty(current, segment)!;
                if (i + 1 < path.Length && int.TryParse(path[i + 1], out int idx))
                {
                    while (adapter.GetArrayLength(array) <= idx)
                        adapter.AppendToArray(array, adapter.CreateObject());
                    current = adapter.GetArrayElement(array, idx);
                    i++; // consumed the index segment
                }
                else
                {
                    adapter.AppendToArray(array, value);
                    return;
                }
            }
            else
            {
                if (!adapter.HasProperty(current, segment))
                    adapter.SetProperty(current, segment, adapter.CreateObject());
                current = adapter.GetProperty(current, segment)!;
            }
        }
        adapter.SetProperty(current, path[^1], value);
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

    private static void SetNestedValueBasic(
        TNode root, string[] path, TNode value, INodeAdapter<TNode> adapter)
    {
        var current = root;
        for (int i = 0; i < path.Length - 1; i++)
        {
            var segment = path[i];
            if (int.TryParse(segment, out _)) continue; // handled via parent array

            bool nextIsIndex = i + 1 < path.Length && int.TryParse(path[i + 1], out _);
            if (nextIsIndex)
            {
                if (!adapter.HasProperty(current, segment))
                    adapter.SetProperty(current, segment, adapter.CreateArray());
                // navigate into array element below
            }
            else
            {
                if (!adapter.HasProperty(current, segment))
                    adapter.SetProperty(current, segment, adapter.CreateObject());
                current = adapter.GetProperty(current, segment)!;
            }
        }
        adapter.SetProperty(current, path[^1], value);
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
