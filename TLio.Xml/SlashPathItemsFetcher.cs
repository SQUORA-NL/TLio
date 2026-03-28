using System.Xml.Linq;
using System.Xml.XPath;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// IItemsFetcher implementation using slash-path conventions (leading / stripped to relative XPath) against XElement documents.
///
/// Path conventions (mirrors XML's natural addressing):
///   /           = root element (data itself)
///   /name       = child element named "name"
///   /a/b/c      = nested path
///   /items/*    = all children of /items
///
/// Paths are stored with a leading /. When forwarded to XPathSelectElements the
/// leading / is stripped so the expression becomes relative to the root element.
/// </summary>
public class SlashPathItemsFetcher : IItemsFetcher<XElement>
{
    public string RootPathIndicator => "/";
    public string PathDelimiter => "/";
    public string CurrentItemPathIndicator => ".";
    public string ParentPathIndicator => "..";
    public string ArrayCloseChar => "]";

    public SelectedNodes<XElement> SelectNodes(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return new SelectedNodes<XElement>(new[] { data });

        var xPath = path.StartsWith("/") ? path[1..] : path;
        if (string.IsNullOrEmpty(xPath))
            return new SelectedNodes<XElement>(new[] { data });

        return new SelectedNodes<XElement>(data.XPathSelectElements(xPath));
    }

    public XElement? SelectNode(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return data;

        var xPath = path.StartsWith("/") ? path[1..] : path;
        if (string.IsNullOrEmpty(xPath))
            return data;

        return data.XPathSelectElement(xPath);
    }

    public string GetPath(XElement node)
    {
        var segments = new List<string>();
        var current = node;
        while (current.Parent != null)
        {
            segments.Insert(0, current.Name.LocalName);
            current = current.Parent;
        }
        if (segments.Count == 0)
            return RootPathIndicator;
        return "/" + string.Join("/", segments);
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

    public void EnsurePath(string path, XElement root, INodeAdapter<XElement> adapter)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return;

        // Create ALL segments including the leaf so that SelectNodes can find the
        // destination after EnsurePath (mirrors JSON EnsureConstructionPath behaviour).
        var segments = path.TrimStart('/').Split('/');
        var current = root;
        foreach (var seg in segments)
        {
            if (string.IsNullOrEmpty(seg)) continue;
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

        var idx = path.LastIndexOf('/');
        if (idx < 0)
            return (RootPathIndicator, path);

        var parent = idx == 0 ? RootPathIndicator : path[..idx];
        var leaf = path[(idx + 1)..];
        return (parent, leaf);
    }

    public string? ProcessIndirectPath(string path, XElement data) =>
        path.Contains("=indirect(") ? null : path;

    public IEnumerable<string> GetIntellisense(string partialPath, XElement data) =>
        Enumerable.Empty<string>();
}
