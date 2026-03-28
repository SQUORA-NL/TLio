using TLio.Core.Contracts;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

/// <summary>
/// IItemsFetcher implementation for YAML using a dot-notation path language.
/// Stub — see tasks.md for the full implementation backlog.
/// Note: YAML has no standardised path language equivalent to JsonPath/XPath.
/// The first implementation will support simple dot-notation paths
/// (e.g. "$.person.address.city"); advanced querying can be added later.
/// </summary>
public class YamlPathItemsFetcher : IItemsFetcher<YamlNode>
{
    public string RootPathIndicator => "$";
    public string PathDelimiter => ".";
    public string CurrentItemPathIndicator => "@";
    public string ParentPathIndicator => "<--";
    public string ArrayCloseChar => "]";

    public SelectedNodes<YamlNode> SelectNodes(string path, YamlNode data)
    {
        // TODO: implement dot-notation path traversal
        throw new NotImplementedException();
    }

    public YamlNode? SelectNode(string path, YamlNode data)
    {
        // TODO: implement dot-notation path traversal
        throw new NotImplementedException();
    }

    public string GetPath(YamlNode node)
    {
        // TODO: build an absolute path from anchor info or node lineage
        return RootPathIndicator;
    }

    public YamlNode? GetParent(YamlNode node, int levels = 1) =>
        throw new NotImplementedException();

    public string ResolveRelativePath(string relativePath, YamlNode currentNode, YamlNode dataContext) =>
        relativePath;

    public void EnsurePath(string path, YamlNode root, INodeAdapter<YamlNode> adapter) =>
        throw new NotImplementedException();

    public (string parentPath, string leafName) SplitParentAndLeaf(string path) =>
        throw new NotImplementedException();

    public string? ProcessIndirectPath(string path, YamlNode data) =>
        path.Contains("=indirect(") ? throw new NotImplementedException() : path;

    public IEnumerable<string> GetIntellisense(string partialPath, YamlNode data) =>
        Enumerable.Empty<string>();
}
