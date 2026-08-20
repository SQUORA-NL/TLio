using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// IItemsFetcher implementation for XElement using genuine XPath expressions, anchored
/// where XPath anchors them: on the <b>document node</b>, not on the document element.
///
/// Path conventions:
///   /                  = the document node (represented by the root element, since the
///                        node model is element-based)
///   /order             = the document element
///   /order/customer    = a child of the document element
///   //customer         = a customer element at any depth (recursive descent)
///   /order/item[1]     = the first item child
///   /order/*           = every child of the document element
///
/// A bare relative step such as <c>customer</c> is <c>child::customer</c> of the document
/// node, which matches nothing — the document node's only element child is the document
/// element. Nothing here skips a level; write the full path or use <c>//</c>.
/// </summary>
public class NativeXPathItemsFetcher : IItemsFetcher<XElement>
{
    // Matches a / that is NOT inside square brackets (for SplitParentAndLeaf)
    private static readonly Regex SplitPattern =
        new(@"/(?![^\[]*\])", RegexOptions.Compiled | RegexOptions.RightToLeft);

    public string RootPathIndicator => "/";
    public string PathDelimiter => "/";
    public string CurrentItemPathIndicator => ".";
    public string ParentPathIndicator => "..";
    public string ArrayCloseChar => "]";

    /// <summary>
    /// XPath evaluates against the document node when the element is attached, which is
    /// what makes <c>/order/customer</c> resolve. A detached element (one built by hand
    /// rather than parsed) has no document, so it stands in as its own context.
    /// </summary>
    internal static XNode EvaluationContext(XElement data) => (XNode?)data.Document ?? data;

    /// <summary>
    /// An XPath path expression starts at the document node (<c>/order</c>), searches from it
    /// (<c>//city</c>), or is written relative to the current node (<c>./city</c>). Bare
    /// relative steps (<c>customer</c>) are legal XPath but indistinguishable from ordinary
    /// text, so they are not auto-detected: a function argument meant as a path is written
    /// anchored, and a bare word stays a value.
    /// </summary>
    public bool IsPathExpression(string text) =>
        text.Length > 1 && (text[0] == '/' || text.StartsWith("./", StringComparison.Ordinal));

    /// <summary>
    /// XPath writes the subscript on the item step and counts from one, so
    /// <c>/order/items/item[2]</c> is position 1 of the array <c>/order/items</c> — the array is
    /// the step above, not the path with the subscript removed.
    /// </summary>
    public bool TrySplitArrayIndex(string path, out string arrayPath, out int index)
    {
        arrayPath = string.Empty;
        index = -1;

        if (!PathSubscript.TrySplit(path, ArrayCloseChar, out var itemPath, out var position))
            return false;

        var lastStep = itemPath.LastIndexOf('/');
        if (lastStep <= 0 || position < 1) return false;

        arrayPath = itemPath[..lastStep];
        index = position - 1;
        return true;
    }

    public SelectedNodes<XElement> SelectNodes(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path))
            return new SelectedNodes<XElement>(new[] { data });

        // "/" is the document node. It is not an element, so it selects nothing: commands
        // that write properties must not see it as an object whose children are addressable
        // without naming the document element. Copy/move to root are handled by path string.
        if (path == RootPathIndicator)
            return new SelectedNodes<XElement>(Array.Empty<XElement>());

        return new SelectedNodes<XElement>(EvaluationContext(data).XPathSelectElements(path));
    }

    public XElement? SelectNode(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path))
            return data;

        if (path == RootPathIndicator)
            return null;

        return EvaluationContext(data).XPathSelectElement(path);
    }

    /// <summary>
    /// The absolute path of a node, including the document element: <c>/order/customer</c>.
    /// The document element itself is <c>/order</c>; only the document node is <c>/</c>.
    /// </summary>
    public string GetPath(XElement node)
    {
        var segments = new List<string>();
        XElement? current = node;
        while (current != null)
        {
            segments.Insert(0, current.Name.LocalName);
            current = current.Parent;
        }
        return segments.Count == 0 ? RootPathIndicator : "/" + string.Join("/", segments);
    }

    public XElement? GetParent(XElement node, int levels = 1)
    {
        var current = node.Parent;
        for (int i = 1; i < levels && current != null; i++)
            current = current.Parent;
        return current;
    }

    public string ResolveRelativePath(string relativePath, XElement currentNode, XElement dataContext)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath == CurrentItemPathIndicator)
            return GetPath(currentNode);

        if (relativePath.StartsWith(CurrentItemPathIndicator + PathDelimiter, StringComparison.Ordinal))
        {
            var after = relativePath[(CurrentItemPathIndicator.Length + PathDelimiter.Length)..];
            var currentPath = GetPath(currentNode);
            return currentPath == RootPathIndicator
                ? "/" + after
                : currentPath + "/" + after;
        }

        if (relativePath == ParentPathIndicator ||
            relativePath == CurrentItemPathIndicator + PathDelimiter + ParentPathIndicator)
        {
            var parent = GetParent(currentNode, 1);
            return parent == null ? RootPathIndicator : GetPath(parent);
        }

        return relativePath;
    }

    /// <summary>
    /// Creates missing elements along a simple <c>/order/a/b</c> path.
    /// The first segment names the document element, which already exists and cannot be
    /// swapped for a differently named one — a path that starts elsewhere builds nothing.
    /// Paths containing XPath predicates or axes are skipped (read-only).
    /// </summary>
    public void EnsurePath(string path, XElement root, INodeAdapter<XElement> adapter)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return;

        // Predicate or axis path — cannot construct; leave the command to handle the miss
        if (path.Contains('[') || path.Contains("::") || path.Contains("//"))
            return;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return;

        // XML has exactly one document element; only paths rooted at it can be created.
        if (segments[0] != root.Name.LocalName)
            return;

        var current = root;
        foreach (var seg in segments.Skip(1))
        {
            var child = current.Element(seg);
            if (child == null)
            {
                child = new XElement(seg);
                current.Add(child);
            }
            current = child;
        }
    }

    public (string parentPath, string leafName) SplitParentAndLeaf(string path)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return (RootPathIndicator, string.Empty);

        var match = SplitPattern.Match(path);
        if (!match.Success)
            return (RootPathIndicator, path);

        var parent = path[..match.Index];
        var leaf   = path[(match.Index + 1)..];
        return (string.IsNullOrEmpty(parent) ? RootPathIndicator : parent, leaf);
    }

    /// <summary>
    /// Returns true for pure recursive-descent leaf paths like <c>//city</c>,
    /// where the XPath expression itself identifies the leaf node (nothing follows
    /// the recursive-descent axis). The command layer will select matching nodes
    /// directly and replace each one in-place rather than splitting parent/leaf.
    /// </summary>
    public bool IsLeafRecursiveDescentSearch(string path) =>
        path.StartsWith("//", StringComparison.Ordinal) &&
        !path.AsSpan(2).Contains('/');

    public string? ProcessIndirectPath(string path, XElement data) =>
        path.Contains("=indirect(") ? null : path;

    public IEnumerable<string> GetIntellisense(string partialPath, XElement data) =>
        Enumerable.Empty<string>();
}
