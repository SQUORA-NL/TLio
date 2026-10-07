using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json.SystemText.Internal;
using TLio.JsonPath;

namespace TLio.Json.SystemText;

/// <summary>
/// IItemsFetcher implementation for System.Text.Json. Path expressions are evaluated by
/// <see cref="TLio.JsonPath"/> — a JSONPath engine that runs directly on <see cref="JsonNode"/>,
/// so selected nodes are the live nodes of the document (parent references intact, nothing
/// serialized or copied).
///
/// Behavioural requirement: must match the Newtonsoft fetcher (<c>TLio.Json.JsonPathItemsFetcher</c>)
/// exactly. The default <see cref="JsonPathDialect.Newtonsoft"/> dialect is built to give the same
/// nodes in the same order, and the same errors, as <c>JToken.SelectTokens</c>. Pass
/// <see cref="JsonPathDialect.Rfc9535"/> or <see cref="JsonPathDialect.Extended"/> to opt in to the
/// standard instead.
/// </summary>
public class SystemTextJsonPathItemsFetcher : IItemsFetcher<JsonNode>, IDisposable
{
    private static readonly Regex IndirectPattern =
        new(@"=indirect\(([^)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // One engine per dialect, shared by every fetcher: a fetcher is created per execution, and
    // the engine is what owns the parsed-query cache, so it has to outlive the fetcher.
    private static readonly ConcurrentDictionary<JsonPathDialect, JsonPathEngine> SharedEngines = new();

    private static JsonPathEngine SharedEngine(JsonPathDialect dialect) =>
        SharedEngines.GetOrAdd(dialect, d => new JsonPathEngine(new JsonPathOptions { Dialect = d }));

    private readonly JsonPathEngine _engine;

    /// <summary>A fetcher that reads paths in the Newtonsoft dialect (identical to <c>TLio.Json</c>).</summary>
    public SystemTextJsonPathItemsFetcher() : this(JsonPathDialect.Newtonsoft)
    {
    }

    /// <summary>A fetcher that reads paths in <paramref name="dialect"/>, with default limits.</summary>
    public SystemTextJsonPathItemsFetcher(JsonPathDialect dialect) : this(SharedEngine(dialect))
    {
    }

    /// <summary>A fetcher backed by a configured engine (custom functions, regex timeout, limits, …).</summary>
    public SystemTextJsonPathItemsFetcher(JsonPathEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>The engine evaluating this fetcher's paths.</summary>
    public JsonPathEngine Engine => _engine;

    /// <summary>Nothing to release: the fetcher no longer holds a serialized snapshot of the document.</summary>
    public void Dispose()
    {
    }

    // ── Protocol constants ────────────────────────────────────────────────────

    public string RootPathIndicator => "$";
    public string PathDelimiter => ".";
    public string CurrentItemPathIndicator => "@";
    public string ParentPathIndicator => "<--";
    public string ArrayOpenChar => "[";
    public string ArrayCloseChar => "]";

    // ── Node selection ────────────────────────────────────────────────────────

    /// <summary>
    /// Select all nodes matching <paramref name="path"/> in <paramref name="data"/>, in the order the
    /// dialect defines. An invalid path throws <see cref="JsonPathException"/> — as Newtonsoft's
    /// <c>SelectTokens</c> throws — rather than quietly matching nothing.
    /// </summary>
    public SelectedNodes<JsonNode> SelectNodes(string path, JsonNode data)
    {
        if (data == null) return new SelectedNodes<JsonNode>();

        var matches = _engine.Select(path, data);
        var results = new List<JsonNode>(matches.Count);
        foreach (var match in matches)
            results.Add(ToNode(match, data));
        return new SelectedNodes<JsonNode>(results);
    }

    /// <summary>
    /// The single node <paramref name="path"/> selects, or null. Like Newtonsoft's <c>SelectToken</c>, a path that
    /// selects more than one node throws (<see cref="JsonPathErrorKind.MultipleResults"/>).
    /// </summary>
    public JsonNode? SelectNode(string path, JsonNode data)
    {
        if (data == null) return null;

        var match = _engine.SelectSingle(path, data);
        return match == null ? null : ToNode(match.Value, data);
    }

    /// <summary>
    /// A match's node. System.Text.Json stores JSON null as a C# null, so a hit on a null value has no node:
    /// hand out the placeholder that stands for the slot it occupies (see <see cref="NullSlots"/>).
    /// </summary>
    private static JsonNode ToNode(JsonPathMatch match, JsonNode root)
    {
        if (match.Node != null) return match.Node;
        if (match.Parent == null) return root; // the document itself is null: nothing to point at
        return NullSlots.CreatePlaceholder(match.Parent, match.Name, match.Index >= 0 ? match.Index : null);
    }

    // ── Path introspection ────────────────────────────────────────────────────

    /// <summary>
    /// Return the JSONPath string for <paramref name="node"/>: "$", "$.a.b", "$.items[3]", etc.
    /// — the same text <c>JsonNode.GetPath()</c> (.NET 8+) produces, computed by
    /// <see cref="FastPath"/> instead for the reason explained there.
    /// </summary>
    public string GetPath(JsonNode node)
    {
        // A null placeholder is detached — its path is the slot it stands for. Route the
        // parent's own path through GetPath (not slot.Parent.GetPath() directly) so a
        // placeholder sitting deep inside a large array gets the same fast lookup as everything
        // else — the parent itself can be exactly the kind of node FastPath exists to speed up.
        if (NullSlots.TryGetSlot(node, out var slot))
            return slot.Key != null
                ? $"{GetPath(slot.Parent)}.{slot.Key}"
                : $"{GetPath(slot.Parent)}[{slot.Index}]";

        return node == null ? RootPathIndicator : FastPath(node);
    }

    // ── Fast path computation ─────────────────────────────────────────────────
    //
    // JsonNode.GetPath() (.NET 8+) walks every array/object ancestor and, at each one, finds
    // this node's own position by scanning from the start — confirmed empirically: ~0.1us at
    // array index 0, ~21us at index 19,999 in a 20,000-element array, growing roughly linearly
    // with position. A script that reads several relative ("@.field") paths per element of a
    // large array (a decisionTable with N inputs, a resolve with several values) ends up paying
    // that scan once per read per element, which sums to O(total elements²) across the array —
    // this is the same issue TLio.Json's JsonPathItemsFetcher.GetPath had for Newtonsoft's
    // JToken.Path, and the fix here is the same shape, adapted to this format's node model.
    //
    // An index cache per JsonArray, and a key cache per JsonObject (System.Text.Json has no
    // intermediate "property" node the way Newtonsoft's JProperty gives one — a value's Parent
    // is the JsonObject directly, so finding "which key am I" needs its own reverse lookup, not
    // just Newtonsoft's O(1) JProperty.Name), each built once in a single forward pass and
    // invalidated whenever the container's Count no longer matches what the cache was built
    // from. That is checking a purely structural fact — this container's current shape — never
    // memoising a value that an unrelated write elsewhere in the document could make stale.

    // Instance-scoped, not static — see the identical fix and full rationale in
    // TLio.Json's JsonPathItemsFetcher.IndexCaches: a fresh fetcher is created per execution, so
    // an instance field already lives exactly as long as one execution needs it to, and never
    // shares a ConditionalWeakTable's internal write lock across concurrent executions.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonArray, ArrayIndexCache> ArrayIndexCaches = new();
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonObject, ObjectKeyCache> ObjectKeyCaches = new();

    private sealed class ArrayIndexCache
    {
        public int Count = -1;
        public Dictionary<JsonNode, int>? Map;
    }

    private sealed class ObjectKeyCache
    {
        public int Count = -1;
        public Dictionary<JsonNode, string>? Map;
    }

    private int IndexOfCached(JsonArray array, JsonNode child)
    {
        if (!ArrayIndexCaches.TryGetValue(array, out var cache))
        {
            cache = new ArrayIndexCache();
            ArrayIndexCaches.Add(array, cache);
        }

        if (cache.Map == null || cache.Count != array.Count || !cache.Map.TryGetValue(child, out var index))
        {
            // Built with the indexer, not foreach, so a null element (JSON null — System.Text.Json
            // has no node for it) still advances the index without needing a dictionary entry.
            var map = new Dictionary<JsonNode, int>(array.Count, ReferenceEqualityComparer.Instance);
            for (var i = 0; i < array.Count; i++)
            {
                var item = array[i];
                if (item != null) map[item] = i;
            }
            cache.Map = map;
            cache.Count = array.Count;
            map.TryGetValue(child, out index);
        }

        return index;
    }

    private string? KeyOfCached(JsonObject obj, JsonNode child)
    {
        if (!ObjectKeyCaches.TryGetValue(obj, out var cache))
        {
            cache = new ObjectKeyCache();
            ObjectKeyCaches.Add(obj, cache);
        }

        if (cache.Map == null || cache.Count != obj.Count || !cache.Map.TryGetValue(child, out var key))
        {
            var map = new Dictionary<JsonNode, string>(obj.Count, ReferenceEqualityComparer.Instance);
            foreach (var kv in obj)
                if (kv.Value != null) map[kv.Value] = kv.Key;
            cache.Map = map;
            cache.Count = obj.Count;
            map.TryGetValue(child, out key);
        }

        return key;
    }

    /// <summary>
    /// Builds the same path text as <c>JsonNode.GetPath()</c>, walking from <paramref name="node"/>
    /// to the document root, but resolving each array-index / object-key step via the caches
    /// above instead of a per-call scan. A child that is not found in its parent's cache — should
    /// not happen for a node that is genuinely still attached — falls back to <c>node.GetPath()</c>
    /// for that node entirely, so a shape this does not handle is slower, never wrong.
    /// </summary>
    private string FastPath(JsonNode node)
    {
        var segments = new List<(bool IsIndex, string Text)>();
        var current = node;
        while (current.Parent != null)
        {
            var parent = current.Parent;
            if (parent is JsonObject obj)
            {
                var key = KeyOfCached(obj, current);
                if (key == null) return node.GetPath();
                segments.Add((false, key));
                current = obj;
            }
            else if (parent is JsonArray array)
            {
                var index = IndexOfCached(array, current);
                segments.Add((true, index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                current = array;
            }
            else
            {
                return node.GetPath();
            }
        }

        var sb = new System.Text.StringBuilder("$"); // RootPathIndicator — literal here, FastPath is static
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var (isIndex, text) = segments[i];
            if (isIndex) sb.Append('[').Append(text).Append(']');
            else sb.Append('.').Append(text);
        }
        return sb.ToString();
    }

    // ── Parent navigation ─────────────────────────────────────────────────────

    public JsonNode? GetParent(JsonNode node, int levels = 1)
    {
        if (node == null || levels <= 0) return node;

        var target = node;
        for (var i = 0; i < levels && target != null; i++)
            target = NavigateToSemanticParent(target);

        return target;
    }

    private static JsonNode? NavigateToSemanticParent(JsonNode token)
    {
        // A null placeholder is detached; its semantic parent comes from the slot it stands
        // for, with the same array-skipping rule as an attached node.
        if (NullSlots.TryGetSlot(token, out var slot))
            return slot.Index != null && slot.Parent is JsonArray && slot.Parent.Parent is JsonObject po
                ? po
                : slot.Parent;

        if (token?.Parent == null) return null;
        var parent = token.Parent;

        // Element in an array that is a property value → skip the array, return the object
        if (token.Parent is JsonArray && parent.Parent is JsonObject)
            return parent.Parent;

        return parent;
    }

    // ── Relative-path resolution ──────────────────────────────────────────────

    /// <summary>
    /// Resolve a relative path expression against <paramref name="currentNode"/>.
    /// Supports @ (current node), @.prop (property), @.&lt;-- (parent navigation).
    /// Ported from JLio's JsonPathItemsFetcher.ResolveRelativePath.
    /// </summary>
    public string ResolveRelativePath(string relativePath, JsonNode currentNode, JsonNode dataContext)
    {
        if (string.IsNullOrEmpty(relativePath))
            return GetPath(currentNode);

        if (relativePath.StartsWith(CurrentItemPathIndicator))
        {
            var pathAfterCurrent = relativePath.Substring(CurrentItemPathIndicator.Length);

            if (pathAfterCurrent.StartsWith(PathDelimiter + ParentPathIndicator,
                    StringComparison.InvariantCulture))
                return HandleParentNavigation(pathAfterCurrent, currentNode);

            var currentPath = GetPath(currentNode);
            if (string.IsNullOrEmpty(pathAfterCurrent))
                return currentPath;

            if (pathAfterCurrent.StartsWith(PathDelimiter))
                pathAfterCurrent = pathAfterCurrent.Substring(PathDelimiter.Length);

            return $"{currentPath}{PathDelimiter}{pathAfterCurrent}";
        }

        return relativePath;
    }

    private string HandleParentNavigation(string pathAfterCurrent, JsonNode currentNode)
    {
        if (pathAfterCurrent.StartsWith(PathDelimiter))
            pathAfterCurrent = pathAfterCurrent.Substring(PathDelimiter.Length);

        var parentIndicatorWithDelimiter = ParentPathIndicator + PathDelimiter;

        var parentLevels = 0;
        var remainder = pathAfterCurrent;

        while (remainder.StartsWith(parentIndicatorWithDelimiter, StringComparison.InvariantCulture))
        {
            parentLevels++;
            remainder = remainder.Substring(parentIndicatorWithDelimiter.Length);
        }

        if (remainder == ParentPathIndicator)
        {
            parentLevels++;
            remainder = "";
        }

        var target = GetParent(currentNode, parentLevels);
        if (target == null)
            return GetPath(currentNode);

        var targetPath = GetPath(target);

        if (!string.IsNullOrEmpty(remainder))
            return $"{targetPath}{PathDelimiter}{remainder}";

        return targetPath;
    }

    // ── Path construction (EnsurePath) ────────────────────────────────────────

    /// <summary>
    /// Walk <paramref name="path"/> from <paramref name="root"/>, creating any missing
    /// intermediate JsonObject nodes using <paramref name="adapter"/>.
    /// Ported from JLio's JsonMethods.CheckOrCreateParentPath.
    /// </summary>
    public void EnsurePath(string path, JsonNode root, INodeAdapter<JsonNode> adapter)
    {
        var split = new JsonSplittedPath(path);
        var selectionPath = split.SelectionPath.ToPathString();
        var constructionPath = split.ConstructionPath.ToList();

        var anchorNodes = SelectNodes(selectionPath, root);
        foreach (var anchor in anchorNodes)
            EnsureConstructionPath(anchor, constructionPath, adapter);
    }

    private static void EnsureConstructionPath(
        JsonNode anchor,
        IReadOnlyList<PathElement> constructionPath,
        INodeAdapter<JsonNode> adapter)
    {
        if (anchor is not JsonObject currentObj) return;

        foreach (var element in constructionPath)
        {
            if (!currentObj.ContainsKey(element.ElementName))
            {
                currentObj.Add(element.ElementName, new JsonObject());
            }
            else if (currentObj[element.ElementName] is null)
            {
                // JSON null (a C# null node here) is an unfilled container — the same answer
                // an empty XML element gives — so a path may be built through it.
                currentObj[element.ElementName] = new JsonObject();
            }
            else if (currentObj[element.ElementName] is not JsonObject)
            {
                return;
            }

            currentObj = (JsonObject)currentObj[element.ElementName]!;
        }
    }

    // ── Path splitting ────────────────────────────────────────────────────────

    /// <summary>
    /// Split a full JsonPath into the parent path and the leaf property name.
    /// Example: "$.a.b.c" → ("$.a.b", "c").
    /// Ported from JLio's JsonSplittedPath / JsonPathMethods.SplitPath.
    /// </summary>
    public (string parentPath, string leafName) SplitParentAndLeaf(string path)
    {
        var split = new JsonSplittedPath(path);

        if (split.Elements.Count <= 1)
            return (RootPathIndicator, path);

        var parentElements = split.ParentElements.ToPathString();
        var leafName = split.LastName;

        return (parentElements, leafName);
    }

    // ── Indirect-path resolution ──────────────────────────────────────────────

    /// <summary>
    /// If <paramref name="path"/> contains =indirect(jsonpath) expressions, resolve
    /// each by reading the string value at that path and substituting into the path.
    /// Returns null when the indirect reference cannot be resolved.
    /// Ported from JLio's JsonPathItemsFetcher.ProcessIndirectPath.
    /// </summary>
    public string? ProcessIndirectPath(string path, JsonNode data)
    {
        if (!path.Contains("=indirect("))
            return path;

        var match = IndirectPattern.Match(path);
        if (!match.Success)
            return null;

        try
        {
            var indirectRef = match.Groups[1].Value.Trim();

            if ((indirectRef.StartsWith("'") && indirectRef.EndsWith("'")) ||
                (indirectRef.StartsWith("\"") && indirectRef.EndsWith("\"")))
                indirectRef = indirectRef.Substring(1, indirectRef.Length - 2);

            var indirectNode = SelectNode(indirectRef, data);
            if (indirectNode is not JsonValue v) return null;
            if (!v.TryGetValue<string>(out var actualPath) || string.IsNullOrEmpty(actualPath))
                return null;

            return IndirectPattern.Replace(path, actualPath);
        }
        catch
        {
            return null;
        }
    }

    // ── Recursive-descent leaf detection ─────────────────────────────────────

    public bool IsLeafRecursiveDescentSearch(string path)
    {
        var split = new JsonSplittedPath(path);
        return split.IsSearchingForObjectsByName;
    }

    // ── Intellisense ──────────────────────────────────────────────────────────

    public IEnumerable<string> GetIntellisense(string partialPath, JsonNode data)
    {
        var path = string.IsNullOrWhiteSpace(partialPath.TrimEnd('.'))
            ? RootPathIndicator
            : partialPath.TrimEnd('.');

        var tokens = SelectNodes(path, data).ToList();

        if (!tokens.Any())
        {
            var lastDot = path.LastIndexOf('.');
            if (lastDot >= 0)
            {
                path = path.Substring(0, lastDot);
                tokens = SelectNodes(path, data).ToList();
            }
        }

        var suggestions = GetSuggestionsFor(tokens, path);
        return suggestions.Where(s => s.StartsWith(partialPath)).Distinct().ToList();
    }

    private static IEnumerable<string> GetSuggestionsFor(IEnumerable<JsonNode> nodes, string basePath)
    {
        var result = new List<string>();
        foreach (var node in nodes)
        {
            if (node is JsonObject obj)
                result.AddRange(obj.Select(kv => $"{basePath}.{kv.Key}"));
            else if (node is JsonArray)
                result.Add($"{basePath}[*]");
        }
        return result;
    }
}
