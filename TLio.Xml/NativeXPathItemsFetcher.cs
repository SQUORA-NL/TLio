using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// IItemsFetcher implementation for XElement using genuine XPath expressions.
///
/// Path conventions:
///   .             = root element (context node / self)
///   name          = direct child element named "name"
///   address/city  = nested path
///   //name        = any descendant named "name" (recursive descent)
///   items/item[1] = first item child of items
///   *             = any child element
///
/// Paths are relative XPath expressions — nothing is stripped or rewritten.
/// The root indicator is "." (current node), not "/".
/// </summary>
public class NativeXPathItemsFetcher : IItemsFetcher<XElement>
{
    // Matches a / that is NOT inside square brackets (for SplitParentAndLeaf)
    private static readonly Regex SplitPattern =
        new(@"/(?![^\[]*\])", RegexOptions.Compiled | RegexOptions.RightToLeft);

    public string RootPathIndicator => ".";
    public string PathDelimiter => "/";
    public string CurrentItemPathIndicator => ".";
    public string ParentPathIndicator => "..";
    public string ArrayCloseChar => "]";

    public SelectedNodes<XElement> SelectNodes(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return new SelectedNodes<XElement>(new[] { data });

        return new SelectedNodes<XElement>(data.XPathSelectElements(path));
    }

    public XElement? SelectNode(string path, XElement data)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return data;

        return data.XPathSelectElement(path);
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
        return string.Join("/", segments);
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
                ? after
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
    /// Creates missing elements along a simple <c>a/b/c</c> path.
    /// Paths containing XPath predicates or axes are skipped (read-only).
    /// </summary>
    public void EnsurePath(string path, XElement root, INodeAdapter<XElement> adapter)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return;

        // Predicate or axis path — cannot construct; leave the command to handle the miss
        if (path.Contains('[') || path.Contains("::") || path.Contains("//"))
            return;

        var segments = path.Split('/');
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
