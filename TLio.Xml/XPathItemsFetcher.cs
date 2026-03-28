using System.Xml.Linq;
using System.Xml.XPath;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// IItemsFetcher implementation using XPath expressions against XElement documents.
///
/// Path conventions:
///   /          = the root element (the <paramref name="data"/> parameter itself)
///   /name      = child element named "name" of root
///   /a/b/c     = nested child path from root
///
/// Paths are stripped of their leading "/" before being evaluated as relative XPath
/// against the data element, since XElement.XPathSelectElements interprets absolute
/// XPath (starting "/") as document-rooted which requires XDocument wrapping.
/// </summary>
public class XPathItemsFetcher : IItemsFetcher<XElement>
{
    public string RootPathIndicator => "/";
    public string PathDelimiter => "/";
    public string CurrentItemPathIndicator => ".";
    public string ParentPathIndicator => "..";
    public string ArrayCloseChar => "]";

    // ── Node selection ────────────────────────────────────────────────────────

    public SelectedNodes<XElement> SelectNodes(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return new SelectedNodes<XElement>([data]);

        var xpath = path.TrimStart('/');
        if (string.IsNullOrEmpty(xpath))
            return new SelectedNodes<XElement>([data]);

        var results = data.XPathSelectElements(xpath);
        return new SelectedNodes<XElement>(results);
    }

    public XElement? SelectNode(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return data;

        var xpath = path.TrimStart('/');
        return string.IsNullOrEmpty(xpath) ? data : data.XPathSelectElement(xpath);
    }

    // ── Path introspection ────────────────────────────────────────────────────

    /// <summary>
    /// Build an absolute path string for <paramref name="node"/> relative to its
    /// document root (the topmost XElement ancestor with no XElement parent).
    /// Returns "/" for the root element itself.
    /// Example: root/person/name → "/person/name"
    /// </summary>
    public string GetPath(XElement node)
    {
        var segments = new List<string>();
        var current = node;

        while (current.Parent is XElement parent)
        {
            segments.Insert(0, current.Name.LocalName);
            current = parent;
        }

        return segments.Count == 0
            ? RootPathIndicator
            : RootPathIndicator + string.Join(PathDelimiter, segments);
    }

    // ── Parent navigation ─────────────────────────────────────────────────────

    public XElement? GetParent(XElement node, int levels = 1)
    {
        XElement? current = node.Parent as XElement;
        for (int i = 1; i < levels && current != null; i++)
            current = current.Parent as XElement;
        return current;
    }

    // ── Relative-path resolution ───────────────────────────────────────────────

    public string ResolveRelativePath(string relativePath, XElement currentNode, XElement dataContext)
    {
        if (string.IsNullOrEmpty(relativePath))
            return GetPath(currentNode);

        if (relativePath == CurrentItemPathIndicator)
            return GetPath(currentNode);

        if (relativePath.StartsWith(CurrentItemPathIndicator + PathDelimiter,
                StringComparison.Ordinal))
        {
            var after = relativePath.Substring(CurrentItemPathIndicator.Length + PathDelimiter.Length);
            var currentPath = GetPath(currentNode);
            return currentPath == RootPathIndicator
                ? RootPathIndicator + after
                : currentPath + PathDelimiter + after;
        }

        return relativePath;
    }

    // ── Path construction ─────────────────────────────────────────────────────

    /// <summary>
    /// Walk <paramref name="path"/> from <paramref name="root"/>, creating any missing
    /// intermediate XElement nodes. All segments including the leaf are created so that
    /// a subsequent SelectNodes can find the destination.
    /// </summary>
    public void EnsurePath(string path, XElement root, INodeAdapter<XElement> adapter)
    {
        var segments = path.TrimStart('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        var current = root;
        foreach (var seg in segments)
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

    /// <summary>
    /// Split "/a/b/c" → ("/a/b", "c").
    /// "/name" → ("/", "name").
    /// </summary>
    public (string parentPath, string leafName) SplitParentAndLeaf(string path)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return (RootPathIndicator, string.Empty);

        var lastSlash = path.LastIndexOf('/');

        // Path is "/name" — only leading slash
        if (lastSlash <= 0)
            return (RootPathIndicator, path.TrimStart('/'));

        var parentPath = path.Substring(0, lastSlash);
        var leafName = path.Substring(lastSlash + 1);
        return (string.IsNullOrEmpty(parentPath) ? RootPathIndicator : parentPath, leafName);
    }

    // ── Indirect / indirect resolution ───────────────────────────────────────

    /// <summary>
    /// =indirect() is not supported for XML paths; returns null when path contains it.
    /// </summary>
    public string? ProcessIndirectPath(string path, XElement data) =>
        path.Contains("=indirect(") ? null : path;

    public IEnumerable<string> GetIntellisense(string partialPath, XElement data) =>
        Enumerable.Empty<string>();
}
