using TLio.Core.Models;

namespace TLio.Core.Contracts;

/// <summary>
/// Path-based node selection — the equivalent of JsonPath but for any data format.
/// Swap this interface to move between JsonPath, XPath, YamlPath, etc.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface IItemsFetcher<TNode>
{
    /// <summary>E.g. "$" for JsonPath, "/" for XPath.</summary>
    string RootPathIndicator { get; }

    /// <summary>E.g. "." for JsonPath, "/" for XPath.</summary>
    string PathDelimiter { get; }

    /// <summary>Token that refers to the current node in a path expression.</summary>
    string CurrentItemPathIndicator { get; }

    /// <summary>Token that refers to the parent node in a relative path.</summary>
    string ParentPathIndicator { get; }

    /// <summary>Character that closes an array/index subscript.</summary>
    string ArrayCloseChar { get; }

    /// <summary>
    /// True when <paramref name="text"/> reads as a path expression in this format rather than
    /// as a plain value. Functions use it to decide whether an argument such as
    /// <c>=fetch(…)</c>'s should be resolved against the document or returned as-is.
    ///
    /// The default is the JSONPath rule — a leading <c>$</c> or <c>@</c> — which is also what
    /// the YAML dot-notation uses. Formats with a different path language (XPath) override it.
    /// A lone <c>$</c> or <c>@</c> is not a useful path, so at least one more character is
    /// required.
    /// </summary>
    bool IsPathExpression(string text) =>
        text.Length > 1 && (text[0] == '$' || text[0] == '@');

    /// <summary>Select zero or more nodes matching the given path expression.</summary>
    SelectedNodes<TNode> SelectNodes(string path, TNode data);

    /// <summary>Select a single node (first match) or return default.</summary>
    TNode? SelectNode(string path, TNode data);

    /// <summary>Return the absolute path string for the given node.</summary>
    string GetPath(TNode node);

    /// <summary>Navigate up the hierarchy by the specified number of levels.</summary>
    TNode? GetParent(TNode node, int levels = 1);

    /// <summary>
    /// Resolve a relative path expression against the current node and return
    /// the absolute path string.
    /// Supports @ (current item) and &lt;-- (parent) syntax used by JLio/TLio.
    /// </summary>
    string ResolveRelativePath(string relativePath, TNode currentNode, TNode dataContext);

    // ── Path construction ─────────────────────────────────────────────────────

    /// <summary>
    /// Walk <paramref name="path"/> from <paramref name="root"/>, creating any
    /// missing intermediate object nodes using <paramref name="adapter"/>.
    /// Used by commands that write to paths that may not yet exist
    /// (mirrors JLio's JsonMethods.CheckOrCreateParentPath).
    /// </summary>
    void EnsurePath(string path, TNode root, INodeAdapter<TNode> adapter);

    /// <summary>
    /// Given a full path string, return the parent path and the leaf property name.
    /// E.g. "$.a.b.c" → ("$.a.b", "c").
    /// </summary>
    (string parentPath, string leafName) SplitParentAndLeaf(string path);

    // ── Indirect / function resolution in paths ───────────────────────────────

    /// <summary>
    /// If <paramref name="path"/> contains any =indirect(...) expressions, resolve
    /// them against <paramref name="data"/> and return the resolved path.
    /// Returns null if resolution fails.
    /// Mirrors JLio's JsonPathItemsFetcher.ProcessIndirectPath logic.
    /// </summary>
    string? ProcessIndirectPath(string path, TNode data);

    /// <summary>
    /// Returns true when the leaf of the path is selected directly via a recursive
    /// descent (..) operator — e.g. "$..myArray". In this case the full path should
    /// be used for node selection and each matched node should be replaced in-place.
    /// Ported from JLio's JsonSplittedPath.IsSearchingForObjectsByName.
    /// Default implementation returns false (override in format-specific fetchers).
    /// </summary>
    bool IsLeafRecursiveDescentSearch(string path) => false;

    /// <summary>
    /// Return intellisense / autocomplete suggestions for an incomplete path
    /// expression against the given data root.
    /// Used by tooling; return empty enumerable if not supported.
    /// </summary>
    IEnumerable<string> GetIntellisense(string partialPath, TNode data);
}
