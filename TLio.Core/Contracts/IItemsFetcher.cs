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
    /// Character that opens an array/index subscript. Declared so the shared helpers do not
    /// have to assume one — a language that brackets a position differently says so here.
    /// </summary>
    string ArrayOpenChar => "[";

    /// <summary>
    /// True when <paramref name="text"/> reads as a path expression in this language rather than
    /// as a plain value. Functions use it to decide whether an argument such as
    /// <c>=fetch(…)</c>'s should be resolved against the document or returned as-is.
    ///
    /// The default answers from the tokens this fetcher already declares — a path either starts
    /// at the root, or is written relative to the current node — so it holds for any language
    /// without naming one. A language where that is not the rule overrides it.
    ///
    /// A lone indicator is not a useful path, so at least one more character is required — this
    /// matters more than it looks: this same method re-checks values that are *already
    /// resolved* (an argument's computed result, not just script text someone typed), including
    /// ones that were quoted literals. XML's current-item indicator is <c>.</c>, an ordinary
    /// character in real data (a decimal point, a padding character), so accepting it bare here
    /// would reinterpret a literal <c>'.'</c> passed to e.g. <c>padLeft(...,'.')</c> as "the
    /// current node" — confirmed by <c>TLio.Parity.Tests</c>' sweep. Bare <c>@</c>/<c>$</c> as
    /// "the current node"/"root" is real (see <see cref="ResolveRelativePath"/>), but only when
    /// a script author writes it directly as a command's own <c>path</c> — that goes through
    /// <see cref="ResolveRelativePath"/> directly and never touches this method.
    /// </summary>
    bool IsPathExpression(string text) =>
        text.Length > 1 &&
        (text.StartsWith(RootPathIndicator, StringComparison.Ordinal) ||
         text.StartsWith(CurrentItemPathIndicator + PathDelimiter, StringComparison.Ordinal));

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
    /// True when the path's last segment is an array subscript — <c>$.items[1]</c>,
    /// <c>/order/items/item[2]</c> — so it addresses a position rather than naming a property.
    ///
    /// Splitting such a path into a parent and a leaf name yields a name no property has, which
    /// is why a write through a subscript used to miss: <c>set</c> reported the property as not
    /// found and <c>put</c> created one literally called <c>items[1]</c> beside the array it
    /// meant to edit. Commands that write a value check this and address the element itself.
    ///
    /// Only an integer subscript counts. A predicate (<c>item[@id='1']</c>), a wildcard
    /// (<c>items[*]</c>) and a bracket-quoted key (<c>$['a.b']</c>) all name something other
    /// than a position, and each format's own selector already handles them.
    /// </summary>
    bool IsLeafArrayIndex(string path) =>
        PathSubscript.TrySplit(path, ArrayOpenChar, ArrayCloseChar, out _, out _);

    /// <summary>
    /// True when the path's last segment carries a <b>selector</b> rather than a name or a
    /// position — a predicate (<c>coverage[coverageCode='OPSTAL']</c>, <c>item[@id='1']</c>,
    /// <c>lines[?(@.sku=='X')]</c>) or a wildcard (<c>items[*]</c>). Such a path addresses the
    /// nodes it matches, so a command that writes a value writes to each match.
    ///
    /// Without this the leaf was split off as a property name no property has, and the parent
    /// it left behind was the collection itself — so <c>put</c> took the array branch of its
    /// upsert and replaced every sibling with the one value. A four-line
    /// <c>&lt;coverages&gt;</c> came back holding a single renamed element.
    ///
    /// An integer subscript is excluded because it names a position, which
    /// <see cref="IsLeafArrayIndex"/> already routes; a bracket-quoted key
    /// (<c>$['a.b']</c>) is excluded because it names a property.
    /// </summary>
    bool IsLeafNodeSelector(string path)
    {
        if (string.IsNullOrEmpty(path) || IsLeafArrayIndex(path)) return false;

        var (_, leaf) = SplitParentAndLeaf(path);
        if (!leaf.EndsWith(ArrayCloseChar, StringComparison.Ordinal)) return false;

        var open = leaf.IndexOf(ArrayOpenChar, StringComparison.Ordinal);
        if (open < 0) return false;

        var start = open + ArrayOpenChar.Length;
        var inner = leaf.AsSpan(start, leaf.Length - start - ArrayCloseChar.Length);

        // "[]" selects nothing, and "['a.b']" is a property name wearing brackets.
        return inner.Length > 0 && inner[0] != '\'' && inner[0] != '"';
    }

    /// <summary>
    /// Splits a path whose last segment is an array subscript into the path of the array itself
    /// and the <b>zero-based</b> position it names.
    ///
    /// The formats disagree on both halves: JSONPath and the YAML dot-notation put the
    /// subscript on the array (<c>$.items[1]</c>) and count from zero, while XPath puts it on
    /// the item step (<c>/order/items/item[2]</c>) and counts from one. Each fetcher normalises
    /// its own; callers get one answer.
    ///
    /// Returns false when the path does not end in an integer subscript.
    /// </summary>
    bool TrySplitArrayIndex(string path, out string arrayPath, out int index)
    {
        index = -1;
        if (!PathSubscript.TrySplit(path, ArrayOpenChar, ArrayCloseChar, out arrayPath, out index))
            return false;

        return arrayPath.Length > 0;
    }

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
