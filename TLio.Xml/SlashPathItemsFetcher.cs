using System.Xml.Linq;
using System.Xml.XPath;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// IItemsFetcher implementation for plain slash paths — the simple-hierarchy subset of
/// <see cref="NativeXPathItemsFetcher"/>, with no predicates or axes.
///
/// It anchors exactly where XPath does, on the <b>document node</b>:
///   /                = the document node (represented by the root element)
///   /order           = the document element
///   /order/customer  = a child of the document element
///   /order/*         = all children of the document element
///
/// Paths are passed to XPath unchanged. Earlier versions stripped the leading <c>/</c> and
/// evaluated relative to the document element, which made <c>/customer</c> — a path that
/// skipped the document element — the working form. It no longer is: write the full path.
/// </summary>
public class SlashPathItemsFetcher : IItemsFetcher<XElement>
{
    public string RootPathIndicator => "/";
    public string PathDelimiter => "/";
    public string CurrentItemPathIndicator => ".";
    public string ParentPathIndicator => "..";
    public string ArrayOpenChar => "[";
    public string ArrayCloseChar => "]";


    /// <summary>
    /// XPath writes the subscript on the item step and counts from one, so
    /// <c>/order/items/item[2]</c> is position 1 of the array <c>/order/items</c> — the array is
    /// the step above, not the path with the subscript removed.
    /// </summary>
    public bool TrySplitArrayIndex(string path, out string arrayPath, out int index)
    {
        arrayPath = string.Empty;
        index = -1;

        if (!PathSubscript.TrySplit(path, ArrayOpenChar, ArrayCloseChar, out var itemPath, out var position))
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

        return new SelectedNodes<XElement>(
            NativeXPathItemsFetcher.EvaluationContext(data).XPathSelectElements(path));
    }

    public XElement? SelectNode(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path))
            return data;

        if (path == RootPathIndicator)
            return null;

        return NativeXPathItemsFetcher.EvaluationContext(data).XPathSelectElement(path);
    }

    /// <summary>
    /// The absolute path of a node, including the document element: <c>/order/customer</c>.
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
        if (string.IsNullOrEmpty(relativePath))
            return GetPath(currentNode);

        if (relativePath == CurrentItemPathIndicator)
            return GetPath(currentNode);

        if (relativePath.StartsWith(CurrentItemPathIndicator + PathDelimiter,
                StringComparison.Ordinal))
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
    /// Creates ALL segments including the leaf, so SelectNodes can find the destination
    /// afterwards (mirrors the JSON EnsureConstructionPath behaviour). The first segment
    /// names the document element, which already exists and cannot be replaced by a
    /// differently named one, so a path rooted elsewhere builds nothing.
    /// </summary>
    public void EnsurePath(string path, XElement root, INodeAdapter<XElement> adapter)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments[0] != root.Name.LocalName)
            return;

        // A segment that is not a legal element name describes something that cannot be built:
        // a position (item[1]), a wildcard, a predicate. XName.Get throws on those, and it threw
        // straight out of the engine, taking the whole script with it — a command that cannot
        // find its destination has to warn and no-op like every other missed path.
        // NativeXPathItemsFetcher has always refused these; this one used to try.
        if (segments.Skip(1).Any(seg => !IsConstructibleName(seg)))
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

    /// <summary>True when the segment can be used verbatim as an element name.</summary>
    private static bool IsConstructibleName(string segment)
    {
        try { return System.Xml.XmlConvert.EncodeLocalName(segment) == segment; }
        catch { return false; }
    }

    public (string parentPath, string leafName) SplitParentAndLeaf(string path)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return (RootPathIndicator, string.Empty);

        var idx = path.LastIndexOf('/');
        if (idx < 0)
            return (RootPathIndicator, path);

        var parent = idx == 0 ? RootPathIndicator : path[..idx];
        var leaf = path[(idx + 1)..];
        return (parent, leaf);
    }

    public bool IsLeafRecursiveDescentSearch(string path) =>
        path.StartsWith("//", StringComparison.Ordinal) &&
        !path.AsSpan(2).Contains('/');

    public string? ProcessIndirectPath(string path, XElement data) =>
        path.Contains("=indirect(") ? null : path;

    public IEnumerable<string> GetIntellisense(string partialPath, XElement data) =>
        Enumerable.Empty<string>();
}
