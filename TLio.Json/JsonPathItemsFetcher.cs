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
        if (node?.Path == null || string.IsNullOrEmpty(node.Path))
            return RootPathIndicator;
        return $"{RootPathIndicator}{PathDelimiter}{node.Path}";
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

        // Walk all segments except the last (the leaf property is created by the command itself)
        foreach (var element in constructionPath.Take(constructionPath.Count - 1))
        {
            if (currentObj.ContainsKey(element.ElementName))
            {
                // Property exists — ensure it is an object before descending
                if (currentObj[element.ElementName]?.Type != JTokenType.Object)
                    return; // can't traverse a non-object
            }
            else
            {
                // Property missing — create an empty JObject
                currentObj.Add(element.ElementName, new JObject());
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
            return (RootPathIndicator, path);

        var parentElements = split.ParentElements.ToPathString();
        var leafName = split.LastName;

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
