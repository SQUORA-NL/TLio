using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using JsonCons.JsonPath;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json.SystemText.Internal;

namespace TLio.Json.SystemText;

/// <summary>
/// IItemsFetcher implementation for System.Text.Json using JsonCons.JsonPath
/// for path-expression evaluation.
///
/// Behavioral requirement: must match Newtonsoft JsonPathItemsFetcher exactly.
/// All path expressions that work in JLio must produce the same node selection here.
///
/// Architecture:
///   JsonCons.JsonPath operates on immutable JsonElement (System.Text.Json).
///   To return mutable JsonNode results with intact parent references, we:
///   1. Serialize the JsonNode tree to a JSON string and parse as JsonDocument.
///   2. Use JsonSelector.SelectNodes to get NormalizedPath for each match.
///   3. Navigate the ORIGINAL JsonNode tree using the same path components.
///   This preserves parent relationships required by Replace/RemoveFromParent.
/// </summary>
public class SystemTextJsonPathItemsFetcher : IItemsFetcher<JsonNode>
{
    private static readonly Regex IndirectPattern =
        new(@"=indirect\(([^)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Protocol constants ────────────────────────────────────────────────────

    public string RootPathIndicator => "$";
    public string PathDelimiter => ".";
    public string CurrentItemPathIndicator => "@";
    public string ParentPathIndicator => "<--";
    public string ArrayCloseChar => "]";

    // ── Node selection ────────────────────────────────────────────────────────

    /// <summary>
    /// Select all nodes matching <paramref name="path"/> in <paramref name="data"/>.
    /// Uses JsonCons.JsonPath on a serialized snapshot to get path components,
    /// then navigates the original JsonNode tree to return live (mutable) nodes.
    /// </summary>
    public SelectedNodes<JsonNode> SelectNodes(string path, JsonNode data)
    {
        if (data == null) return new SelectedNodes<JsonNode>();

        try
        {
            // Serialize to a JsonDocument for JsonCons evaluation
            var json = data.ToJsonString();
            using var doc = JsonDocument.Parse(json);

            var selector = JsonSelector.Parse(path);
            var pathNodes = selector.SelectNodes(doc.RootElement);

            var results = new List<JsonNode>();
            foreach (var pathNode in pathNodes)
            {
                var node = NavigateByPath(data, pathNode.Path);
                if (node != null)
                    results.Add(node);
            }

            return new SelectedNodes<JsonNode>(results);
        }
        catch
        {
            return new SelectedNodes<JsonNode>();
        }
    }

    public JsonNode? SelectNode(string path, JsonNode data)
    {
        if (data == null) return null;
        try
        {
            var json = data.ToJsonString();
            using var doc = JsonDocument.Parse(json);
            var selector = JsonSelector.Parse(path);
            var pathNodes = selector.SelectNodes(doc.RootElement);
            if (!pathNodes.Any()) return null;
            return NavigateByPath(data, pathNodes[0].Path);
        }
        catch
        {
            return null;
        }
    }

    // ── Path introspection ────────────────────────────────────────────────────

    /// <summary>
    /// Return the JSONPath string for <paramref name="node"/>.
    /// Uses JsonNode.GetPath() (available from .NET 8) which returns "$", "$.a.b", etc.
    /// </summary>
    public string GetPath(JsonNode node) => node?.GetPath() ?? RootPathIndicator;

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

    // ── Private: JsonNode navigation by NormalizedPath ────────────────────────

    /// <summary>
    /// Navigate the original <paramref name="root"/> JsonNode tree by following
    /// the components of <paramref name="path"/> (from JsonCons.JsonPath).
    /// This preserves parent references on the returned JsonNode.
    /// </summary>
    private static JsonNode? NavigateByPath(JsonNode root, NormalizedPath path)
    {
        JsonNode? current = root;
        foreach (var component in path)
        {
            if (current == null) return null;
            current = component.ComponentKind switch
            {
                NormalizedPathNodeKind.Root  => root,
                NormalizedPathNodeKind.Name  => current is JsonObject obj ? obj[component.GetName()] : null,
                NormalizedPathNodeKind.Index => current is JsonArray arr ? arr[component.GetIndex()] : null,
                _ => null
            };
        }
        return current;
    }
}
