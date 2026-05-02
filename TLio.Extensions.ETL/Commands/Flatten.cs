using System.Globalization;

namespace TLio.Extensions.ETL.Commands;

/// <summary>
/// Flattens a nested node into a single-level object with dot-separated keys.
///
/// Example:  { "a": { "b": 1 } }  →  { "a.b": 1, "a.b_type": "Integer" }
///
/// Stores FlattenMetadata (OriginalStructure, delimiter info) inside the document
/// so that Restore can reconstruct the original shape.
///
/// Ported from JLio.Extensions.ETL.Commands.Flatten. All data traversal uses
/// INodeAdapter — no format-specific code.
/// </summary>
public class Flatten<TNode> : CommandBase<TNode>
{
    public override string CommandName => "flatten";

    public string Path { get; set; } = "$";
    public FlattenSettings FlattenSettings { get; set; } = new();

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path, TraceOutcome.Failure, 0,
                $"flatten: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var flattenCount = 0;
        try
        {
            var targets = context.ItemsFetcher.SelectNodes(Path, dataContext).ToList();
            flattenCount = targets.Count;
            foreach (var target in targets)
            {
                var flat = new Dictionary<string, TNode>();
                FlattenNode(target, "", flat, 0, context.NodeAdapter, FlattenSettings);

                var metadata = CreateMetadata(target, context);
                var flatObj = BuildFlatObject(flat, context.NodeAdapter);
                context.NodeAdapter.Replace(target, flatObj);
                StoreMetadata(dataContext, flatObj, metadata, context);
            }
        }
        catch (Exception ex)
        {
            context.LogError(CoreConstants.CommandExecution, $"flatten: {ex.Message}");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path, TraceOutcome.Failure, 0,
                $"flatten: error — {ex.Message}"));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path,
            flattenCount == 0 ? TraceOutcome.NoOp : TraceOutcome.Success,
            flattenCount,
            flattenCount == 0
                ? $"flatten: path '{Path}' matched 0 nodes; nothing flattened."
                : $"flatten: flattened {flattenCount} node(s) at '{Path}'."));
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    // ── Core flatten algorithm ─────────────────────────────────────────────────

    private static void FlattenNode(
        TNode node, string prefix,
        Dictionary<string, TNode> result,
        int depth,
        INodeAdapter<TNode> adapter,
        FlattenSettings settings)
    {
        if (settings.MaxDepth > 0 && depth >= settings.MaxDepth)
        {
            result[prefix] = adapter.CreateString(adapter.TryGetString(node) ?? adapter.Serialize(node));
            return;
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            if (ShouldExcludePath(prefix, settings)) return;
            if (!ShouldIncludePath(prefix, settings)) return;
        }

        if (adapter.IsObject(node))
        {
            var names = adapter.GetPropertyNames(node).ToList();
            if (names.Count == 0 && !string.IsNullOrEmpty(prefix))
            {
                result[prefix] = adapter.CreateString("");
                return;
            }
            foreach (var name in names)
            {
                var child = adapter.GetProperty(node, name)!;
                var newKey = string.IsNullOrEmpty(prefix) ? name : $"{prefix}{settings.Delimiter}{name}";
                FlattenNode(child, newKey, result, depth + 1, adapter, settings);
            }
        }
        else if (adapter.IsArray(node))
        {
            var elements = adapter.GetArrayElements(node).ToList();
            if (elements.Count == 0 && !string.IsNullOrEmpty(prefix))
            {
                result[prefix] = adapter.CreateArray();
                return;
            }
            var delim = settings.IncludeArrayIndices ? settings.ArrayDelimiter : settings.Delimiter;
            for (int i = 0; i < elements.Count; i++)
            {
                var arrayKey = $"{prefix}{delim}{i}";
                FlattenNode(elements[i], arrayKey, result, depth + 1, adapter, settings);
            }
        }
        else
        {
            result[prefix] = node;
            if (settings.PreserveTypes)
                result[$"{prefix}{settings.TypeIndicator}"] =
                    adapter.CreateString(GetTypeName(node, adapter));
        }
    }

    private static bool ShouldExcludePath(string path, FlattenSettings settings)
    {
        if (settings.ExcludePaths == null || settings.ExcludePaths.Count == 0) return false;
        foreach (var ep in settings.ExcludePaths)
            if (path.StartsWith(ep, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool ShouldIncludePath(string path, FlattenSettings settings)
    {
        if (settings.IncludePaths == null || settings.IncludePaths.Count == 0) return true;
        foreach (var ip in settings.IncludePaths)
            if (path.StartsWith(ip, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string GetTypeName(TNode node, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsNull(node)) return "Null";
        if (adapter.TryGetBoolean(node) != null) return "Boolean";
        var d = adapter.TryGetDouble(node);
        if (d != null) return (d.Value % 1 == 0) ? "Integer" : "Float";
        return "String";
    }

    private static TNode BuildFlatObject(Dictionary<string, TNode> flat, INodeAdapter<TNode> adapter)
    {
        var obj = adapter.CreateObject();
        foreach (var (key, value) in flat)
            adapter.SetProperty(obj, key, adapter.DeepClone(value));
        return obj;
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    private FlattenMetadata CreateMetadata(TNode node, IExecutionContext<TNode> context)
    {
        var structure = new Dictionary<string, string>();
        AnalyzeStructure(node, "", structure, context.NodeAdapter, FlattenSettings);
        return new FlattenMetadata
        {
            OriginalStructure = structure,
            Delimiter = FlattenSettings.Delimiter,
            ArrayDelimiter = FlattenSettings.ArrayDelimiter,
            IncludeArrayIndices = FlattenSettings.IncludeArrayIndices,
            PreserveTypes = FlattenSettings.PreserveTypes,
            TypeIndicator = FlattenSettings.TypeIndicator,
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            MetadataKey = FlattenSettings.MetadataKey,
        };
    }

    private static void AnalyzeStructure(
        TNode node, string prefix,
        Dictionary<string, string> structure,
        INodeAdapter<TNode> adapter,
        FlattenSettings settings)
    {
        if (adapter.IsObject(node))
        {
            if (!string.IsNullOrEmpty(prefix)) structure[prefix] = "object";
            foreach (var name in adapter.GetPropertyNames(node))
            {
                var newKey = string.IsNullOrEmpty(prefix) ? name : $"{prefix}{settings.Delimiter}{name}";
                AnalyzeStructure(adapter.GetProperty(node, name)!, newKey, structure, adapter, settings);
            }
        }
        else if (adapter.IsArray(node))
        {
            var len = adapter.GetArrayLength(node);
            structure[prefix] = $"array[{len}]";
            var delim = settings.IncludeArrayIndices ? settings.ArrayDelimiter : settings.Delimiter;
            var elements = adapter.GetArrayElements(node).ToList();
            for (int i = 0; i < elements.Count; i++)
                AnalyzeStructure(elements[i], $"{prefix}{delim}{i}", structure, adapter, settings);
        }
    }

    private void StoreMetadata(
        TNode dataContext, TNode flatObj,
        FlattenMetadata metadata, IExecutionContext<TNode> context)
    {
        if (string.IsNullOrEmpty(FlattenSettings.MetadataPath)) return;
        try
        {
            var target = string.IsNullOrEmpty(FlattenSettings.MetadataPath) || FlattenSettings.MetadataPath == "$"
                ? dataContext
                : context.ItemsFetcher.SelectNode(FlattenSettings.MetadataPath, dataContext) ?? dataContext;

            if (!context.NodeAdapter.IsObject(target)) return;
            context.NodeAdapter.SetProperty(target, FlattenSettings.MetadataKey,
                SerializeMetadata(metadata, context.NodeAdapter));
        }
        catch (Exception ex)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"flatten: failed to store metadata: {ex.Message}");
        }
    }

    internal static TNode SerializeMetadata(FlattenMetadata meta, INodeAdapter<TNode> adapter)
    {
        var obj = adapter.CreateObject();
        adapter.SetProperty(obj, "delimiter",           adapter.CreateString(meta.Delimiter ?? "."));
        adapter.SetProperty(obj, "arrayDelimiter",      adapter.CreateString(meta.ArrayDelimiter ?? "."));
        adapter.SetProperty(obj, "includeArrayIndices", adapter.CreateBoolean(meta.IncludeArrayIndices));
        adapter.SetProperty(obj, "preserveTypes",       adapter.CreateBoolean(meta.PreserveTypes));
        adapter.SetProperty(obj, "typeIndicator",       adapter.CreateString(meta.TypeIndicator ?? "_type"));
        adapter.SetProperty(obj, "version",             adapter.CreateString(meta.Version));
        adapter.SetProperty(obj, "metadataKey",         adapter.CreateString(meta.MetadataKey ?? ""));
        if (meta.Timestamp != null)
            adapter.SetProperty(obj, "timestamp", adapter.CreateString(meta.Timestamp));
        if (meta.RootPath != null)
            adapter.SetProperty(obj, "rootPath", adapter.CreateString(meta.RootPath));

        var structNode = adapter.CreateObject();
        foreach (var (k, v) in meta.OriginalStructure)
            adapter.SetProperty(structNode, k, adapter.CreateString(v));
        adapter.SetProperty(obj, "originalStructure", structNode);

        return obj;
    }

    internal static FlattenMetadata? DeserializeMetadata(TNode node, INodeAdapter<TNode> adapter)
    {
        if (!adapter.IsObject(node)) return null;
        var meta = new FlattenMetadata();

        string? S(string key) => adapter.GetProperty(node, key) is { } n ? adapter.TryGetString(n) : null;
        bool? B(string key) => adapter.GetProperty(node, key) is { } n ? adapter.TryGetBoolean(n) : null;

        meta.Delimiter          = S("delimiter") ?? ".";
        meta.ArrayDelimiter     = S("arrayDelimiter") ?? ".";
        meta.IncludeArrayIndices = B("includeArrayIndices") ?? true;
        meta.PreserveTypes      = B("preserveTypes") ?? true;
        meta.TypeIndicator      = S("typeIndicator") ?? "_type";
        meta.MetadataKey        = S("metadataKey");

        var structNode = adapter.GetProperty(node, "originalStructure");
        if (structNode != null && adapter.IsObject(structNode))
            foreach (var k in adapter.GetPropertyNames(structNode))
                meta.OriginalStructure[k] =
                    adapter.TryGetString(adapter.GetProperty(structNode, k)!) ?? "";

        return meta;
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.ValidationMessages.Add("Path property is required for flatten command");
        if (FlattenSettings == null)
            result.ValidationMessages.Add("FlattenSettings property is required for flatten command");
        else
        {
            if (string.IsNullOrEmpty(FlattenSettings.Delimiter))
                result.ValidationMessages.Add("Delimiter cannot be empty in FlattenSettings");
            if (FlattenSettings.MaxDepth == 0)
                result.ValidationMessages.Add("MaxDepth cannot be 0 in FlattenSettings");
        }
        return result;
    }
}
