using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json.Internal;

namespace TLio.Json;

/// <summary>
/// IItemsFetcher implementation using Newtonsoft.Json's built-in JsonPath support.
/// Ported from JLio.Core.Extensions.JsonPathItemsFetcher and JsonPathMethods.
/// </summary>
public class JsonPathItemsFetcher : IItemsFetcher<JToken>
{
    // ── Indirect-path regex (compiled once) ───────────────────────────────────
    private static readonly Regex IndirectPattern =
        new(@"=indirect\(([^)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Protocol constants ────────────────────────────────────────────────────

    public string RootPathIndicator => "$";
    public string PathDelimiter => ".";
    public string CurrentItemPathIndicator => "@";
    public string ParentPathIndicator => "<--";
    public string ArrayOpenChar => "[";
    public string ArrayCloseChar => "]";

    // ── Node selection ────────────────────────────────────────────────────────

    public SelectedNodes<JToken> SelectNodes(string path, JToken data)
    {
        if (data == null) return new SelectedNodes<JToken>();
        return new SelectedNodes<JToken>(data.SelectTokens(path));
    }

    public JToken? SelectNode(string path, JToken data) => data?.SelectToken(path);

    // ── Path introspection ────────────────────────────────────────────────────

    public string GetPath(JToken node)
    {
        if (node == null) return RootPathIndicator;
        var path = FastPath(node);
        return string.IsNullOrEmpty(path) ? RootPathIndicator : $"{RootPathIndicator}{PathDelimiter}{path}";
    }

    // ── Fast path computation ─────────────────────────────────────────────────
    //
    // Newtonsoft's own JToken.Path walks every array ancestor and, at each one, finds this
    // node's own index by scanning the array's children from the start — O(index). A script
    // that reads several relative ("@.field") paths per element of a large array (a decisionTable
    // with N inputs, a resolve with several values) ends up paying that scan once per read per
    // element, which sums to O(total elements²) across the array, not O(elements). Prototyped
    // and measured against TLio.Sample/AFD conversion scripts: at 20,000 array elements this was
    // the dominant cost, ~150s of a ~150s run.
    //
    // The fix targets exactly that scan and nothing else: an index cache per JArray, built once
    // in a single forward pass and invalidated whenever the array's Count no longer matches what
    // the cache was built from — cheap to check on every call, and correct by construction: it
    // caches a purely structural fact (this array's current child order), invalidated the moment
    // the array's own shape changes, which is the only thing that can make it wrong. It is not
    // memoising a *value* that an unrelated write elsewhere in the document could make stale —
    // that would be unsafe, since a later step in the same activity must see an earlier step's
    // change, and nothing here weakens that.
    // Known gap: an in-place reorder that leaves Count unchanged (swapping two elements without
    // adding/removing any) would go undetected. No built-in TLio function does that today —
    // `sort`/`sortBy` build and return a new array rather than reordering the source in place —
    // but a future one that does would need to invalidate this cache explicitly (see
    // InvalidateIndexCache below) rather than mutate an array's element order silently.

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JArray, ArrayIndexCache> IndexCaches = new();

    private sealed class ArrayIndexCache
    {
        public int Count = -1;
        public Dictionary<JToken, int>? Map;
    }

    /// <summary>
    /// Escape hatch for a future array-mutating-in-place function/command: drop this array's
    /// cached index map so the next GetPath call rebuilds it. Not called anywhere today because
    /// nothing needs it today (see the remarks on GetPath) — here so that guarantee has a place
    /// to be enforced from if it ever stops holding.
    /// </summary>
    internal static void InvalidateIndexCache(JArray array) => IndexCaches.Remove(array);

    private static int IndexOfCached(JArray array, JToken child)
    {
        if (!IndexCaches.TryGetValue(array, out var cache))
        {
            cache = new ArrayIndexCache();
            IndexCaches.Add(array, cache);
        }

        if (cache.Map == null || cache.Count != array.Count || !cache.Map.TryGetValue(child, out var index))
        {
            var map = new Dictionary<JToken, int>(array.Count, ReferenceEqualityComparer.Instance);
            var i = 0;
            foreach (var item in array)
                map[item] = i++;
            cache.Map = map;
            cache.Count = array.Count;
            map.TryGetValue(child, out index);
        }

        return index;
    }

    /// <summary>
    /// Builds the same path text as <c>JToken.Path</c>, walking from <paramref name="node"/> to
    /// the document root, but resolving each array-element step via <see cref="IndexOfCached"/>
    /// instead of Newtonsoft's per-call scan. Any container shape this does not specifically
    /// know how to walk (only <see cref="JProperty"/> and <see cref="JArray"/> parents are
    /// handled — a plain JSON document never has anything else) falls back to <c>node.Path</c>
    /// itself, so a shape this misses is slower, never wrong.
    /// </summary>
    private static string FastPath(JToken node)
    {
        var segments = new List<(bool IsIndex, string Text)>();
        var current = node;
        while (current.Parent != null)
        {
            var parent = current.Parent;
            if (parent is JProperty property)
            {
                // A property whose own Parent is null has been removed from its owning object —
                // e.g. a still-nested match under a node an earlier step in the same recursive
                // walk already detached (RemoveTests.CanRemoveRecursiveValues hits exactly this).
                // Newtonsoft's own node.Path handles that case; match it exactly rather than
                // guess, since it is neither this node's array-index step nor the shape this
                // fast path exists to speed up.
                if (property.Parent == null)
                    return node.Path;
                segments.Add((false, property.Name));
                current = property.Parent;
            }
            else if (parent is JArray array)
            {
                var index = IndexOfCached(array, current);
                segments.Add((true, index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                current = array;
            }
            else
            {
                return node.Path;
            }
        }

        segments.Reverse();
        var sb = new System.Text.StringBuilder();
        foreach (var (isIndex, text) in segments)
        {
            if (isIndex)
                sb.Append('[').Append(text).Append(']');
            else
            {
                if (sb.Length > 0) sb.Append('.');
                sb.Append(text);
            }
        }
        return sb.ToString();
    }

    // ── Parent navigation ─────────────────────────────────────────────────────

    /// <summary>
    /// Navigate up the semantic-parent chain by <paramref name="levels"/> steps,
    /// skipping Newtonsoft JProperty / containing-JArray wrappers at each level.
    /// Mirrors JLio's JsonPathItemsFetcher.NavigateToParent / NavigateToSemanticParent.
    /// </summary>
    public JToken? GetParent(JToken node, int levels = 1)
    {
        if (node == null || levels <= 0) return node;

        var target = node;
        for (var i = 0; i < levels && target != null; i++)
            target = NavigateToSemanticParent(target);

        return target;
    }

    private static JToken? NavigateToSemanticParent(JToken token)
    {
        if (token?.Parent == null)
            return null;

        var parent = token.Parent;

        // Node is an array element
        if (token.Parent is JArray)
        {
            // If the array itself is a property value, skip both JArray and JProperty
            if (parent.Parent is JProperty)
                return parent.Parent.Parent;  // owning JObject

            return parent; // root-level array
        }

        // Node is the value of a named property
        if (token.Parent is JProperty property)
            return property.Parent;

        return parent;
    }

    // ── Relative-path resolution ───────────────────────────────────────────────

    /// <summary>
    /// Resolve a relative path expression against <paramref name="currentNode"/>
    /// and return an absolute path string.
    ///
    /// Supports:
    ///   @           → path of the current node
    ///   @.prop      → current node path + ".prop"
    ///   @.&lt;--   → one level up
    ///   @.&lt;--.&lt;-- → two levels up, etc.
    ///
    /// Ported from JLio's JsonPathItemsFetcher.ResolveRelativePath / HandleParentNavigation.
    /// </summary>
    public string ResolveRelativePath(string relativePath, JToken currentNode, JToken dataContext)
    {
        if (string.IsNullOrEmpty(relativePath))
            return GetPath(currentNode);

        if (relativePath.StartsWith(CurrentItemPathIndicator))
        {
            var pathAfterCurrent = relativePath.Substring(CurrentItemPathIndicator.Length);

            // Parent navigation: @.<--, @.<--.<--, etc.
            if (pathAfterCurrent.StartsWith(PathDelimiter + ParentPathIndicator,
                    StringComparison.InvariantCulture))
                return HandleParentNavigation(pathAfterCurrent, currentNode);

            // Regular relative path: @.property
            var currentPath = GetPath(currentNode);
            if (string.IsNullOrEmpty(pathAfterCurrent))
                return currentPath;

            if (pathAfterCurrent.StartsWith(PathDelimiter))
                pathAfterCurrent = pathAfterCurrent.Substring(PathDelimiter.Length);

            return $"{currentPath}{PathDelimiter}{pathAfterCurrent}";
        }

        // Absolute path — return as-is
        return relativePath;
    }

    private string HandleParentNavigation(string pathAfterCurrent, JToken currentNode)
    {
        // Strip leading delimiter
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

        // Trailing <-- with no following delimiter
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
    /// intermediate JObject nodes using <paramref name="adapter"/>.
    ///
    /// Ported from JLio's JsonMethods.CheckOrCreateParentPath +
    /// CheckOrCreateConstructionPath.
    /// </summary>
    public void EnsurePath(string path, JToken root, INodeAdapter<JToken> adapter)
    {
        var split = new JsonSplittedPath(path);
        var selectionPath = split.SelectionPath.ToPathString();
        var constructionPath = split.ConstructionPath.ToList();

        // Select existing anchor nodes, then walk the construction path from each
        var anchorTokens = root.SelectTokens(selectionPath);
        foreach (var anchor in anchorTokens)
            EnsureConstructionPath(anchor, constructionPath, adapter);
    }

    private static void EnsureConstructionPath(
        JToken anchor,
        IReadOnlyList<PathElement> constructionPath,
        INodeAdapter<JToken> adapter)
    {
        if (anchor is not JObject currentObj) return;

        // Walk all segments including the leaf so that SelectNodes can find the destination.
        // Commands that write to new paths rely on finding an existing node to Replace().
        foreach (var element in constructionPath)
        {
            if (!currentObj.ContainsKey(element.ElementName))
            {
                // Property missing — create an empty JObject placeholder
                currentObj.Add(element.ElementName, new JObject());
            }
            else if (currentObj[element.ElementName]?.Type == JTokenType.Null)
            {
                // A null value is an unfilled container — the same answer an empty XML
                // element gives — so a path may be built through it. Without this,
                // {"a":null} + add $.a.b.c refused in JSON what XML happily created.
                currentObj[element.ElementName] = new JObject();
            }
            else if (currentObj[element.ElementName]?.Type != JTokenType.Object)
            {
                // Property exists but is not an object — leaf already has a value;
                // SelectNodes will find it and Replace() will overwrite it.
                return;
            }

            currentObj = (JObject)currentObj[element.ElementName]!;
        }
    }

    // ── Path splitting ────────────────────────────────────────────────────────

    /// <summary>
    /// Split a full JsonPath into the parent path and the leaf property name.
    /// Example: "$.a.b.c" → ("$.a.b", "c").
    ///
    /// Handles bracket-nesting so that filter expressions are not broken.
    /// Ported from JLio's JsonSplittedPath / JsonPathMethods.SplitPath.
    /// </summary>
    public (string parentPath, string leafName) SplitParentAndLeaf(string path)
    {
        var split = new JsonSplittedPath(path);

        if (split.Elements.Count <= 1)
        {
            // $['key'] or $["key"] — single-element bracket-notation root path
            var singleLeaf = path;
            if ((singleLeaf.StartsWith("$['") && singleLeaf.EndsWith("']")) ||
                (singleLeaf.StartsWith("$[\"") && singleLeaf.EndsWith("\"]")))
                singleLeaf = singleLeaf.Substring(3, singleLeaf.Length - 5);
            return (RootPathIndicator, singleLeaf);
        }

        var parentElements = split.ParentElements.ToPathString();
        var leafName = split.LastName;

        // Strip bracket-quoted notation so the leaf is a plain property name:
        // ['version.major'] → version.major,  ["version.major"] → version.major
        if ((leafName.StartsWith("['") && leafName.EndsWith("']")) ||
            (leafName.StartsWith("[\"") && leafName.EndsWith("\"]")))
            leafName = leafName.Substring(2, leafName.Length - 4);

        return (parentElements, leafName);
    }

    // ── Indirect-path resolution ──────────────────────────────────────────────

    /// <summary>
    /// If <paramref name="path"/> contains <c>=indirect(jsonpath)</c> expressions,
    /// resolve each one by reading the string value at that path from <paramref name="data"/>
    /// and substituting it into the path string.
    ///
    /// Returns null when the indirect reference cannot be resolved (missing token,
    /// non-string value, or malformed expression).
    ///
    /// Ported from JLio's JsonPathItemsFetcher.ProcessIndirectPath.
    /// </summary>
    public string? ProcessIndirectPath(string path, JToken data)
    {
        if (!path.Contains("=indirect("))
            return path;

        var match = IndirectPattern.Match(path);
        if (!match.Success)
            return null;

        try
        {
            var indirectRef = match.Groups[1].Value.Trim();

            // Strip surrounding quotes: '$.path' or "$.path"
            if ((indirectRef.StartsWith("'") && indirectRef.EndsWith("'")) ||
                (indirectRef.StartsWith("\"") && indirectRef.EndsWith("\"")))
                indirectRef = indirectRef.Substring(1, indirectRef.Length - 2);

            var indirectToken = data.SelectToken(indirectRef);
            if (indirectToken == null || indirectToken.Type != JTokenType.String)
                return null;

            var actualPath = indirectToken.Value<string>();
            if (string.IsNullOrEmpty(actualPath))
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

    /// <summary>
    /// Return property-path suggestions for the given partial path expression.
    /// Objects: return child property paths. Arrays: return the wildcard path.
    ///
    /// Ported from JLio's JsonPathMethods.GetIntellisense.
    /// </summary>
    public IEnumerable<string> GetIntellisense(string partialPath, JToken data)
    {
        var path = string.IsNullOrWhiteSpace(partialPath.TrimEnd('.'))
            ? RootPathIndicator
            : partialPath.TrimEnd('.');

        var tokens = data.SelectTokens(path).ToList();

        if (!tokens.Any())
        {
            var lastDot = path.LastIndexOf('.');
            if (lastDot >= 0)
            {
                path = path.Substring(0, lastDot);
                tokens = data.SelectTokens(path).ToList();
            }
        }

        var suggestions = GetSuggestionsFor(tokens, path);
        var filtered = FilterStartsWith(partialPath, suggestions).Distinct().ToList();
        return filtered;
    }

    private static IEnumerable<string> GetSuggestionsFor(IEnumerable<JToken> tokens, string basePath)
    {
        var result = new List<string>();
        foreach (var token in tokens)
        {
            switch (token)
            {
                case JObject obj:
                    result.AddRange(obj.Properties().Select(p => $"{basePath}.{p.Name}"));
                    break;
                case JArray:
                    result.Add($"{basePath}[*]");
                    break;
            }
        }
        return result;
    }

    private static List<string> FilterStartsWith(string path, IEnumerable<string> items)
    {
        var list = items.ToList();
        var matching = list.Where(i => i.StartsWith(path)).ToList();
        return matching.Any() ? matching : list;
    }
}
