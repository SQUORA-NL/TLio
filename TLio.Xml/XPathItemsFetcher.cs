using System.Xml.Linq;
using System.Xml.XPath;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// IItemsFetcher implementation using XPath expressions against XElement documents.
/// Stub — see tasks.md for implementation backlog.
/// </summary>
public class XPathItemsFetcher : IItemsFetcher<XElement>
{
    public string RootPathIndicator => "/";
    public string PathDelimiter => "/";
    public string CurrentItemPathIndicator => ".";
    public string ParentPathIndicator => "..";
    public string ArrayCloseChar => "]";

    public SelectedNodes<XElement> SelectNodes(string path, XElement data)
    {
        var results = data.XPathSelectElements(path);
        return new SelectedNodes<XElement>(results);
    }

    public XElement? SelectNode(string path, XElement data) =>
        data.XPathSelectElement(path);

    public string GetPath(XElement node) =>
        // TODO: build a proper XPath from the element's ancestry
        $"/{node.Name.LocalName}";

    public XElement? GetParent(XElement node, int levels = 1)
    {
        var current = node.Parent;
        for (int i = 1; i < levels && current != null; i++)
            current = current.Parent;
        return current;
    }

    public string ResolveRelativePath(string relativePath, XElement currentNode, XElement dataContext)
    {
        // TODO: implement relative-path resolution
        return relativePath;
    }
}
