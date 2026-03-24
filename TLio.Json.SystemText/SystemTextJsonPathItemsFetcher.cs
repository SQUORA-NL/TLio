using System.Text.Json.Nodes;
using JsonCons.JsonPath;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Json.SystemText;

/// <summary>
/// IItemsFetcher implementation for System.Text.Json using JsonCons.JsonPath
/// for path-expression evaluation.
///
/// Stub — SelectNodes/ProcessIndirectPath/EnsurePath need full implementation.
/// See specs/002-migration-from-jlio/tasks.md for backlog.
///
/// Behavioral requirement: must match Newtonsoft JsonPathItemsFetcher exactly.
/// All path expressions that work in JLio must produce the same node selection here.
/// </summary>
public class SystemTextJsonPathItemsFetcher : IItemsFetcher<JsonNode>
{
    public string RootPathIndicator => "$";
    public string PathDelimiter => ".";
    public string CurrentItemPathIndicator => "@";
    public string ParentPathIndicator => "<--";
    public string ArrayCloseChar => "]";

    public SelectedNodes<JsonNode> SelectNodes(string path, JsonNode data)
    {
        // TODO: process indirect expressions first, then use JsonCons.JsonPath
        var selector = JsonSelector.Parse(path);
        var results = selector.Select(data);
        return new SelectedNodes<JsonNode>(results);
    }

    public JsonNode? SelectNode(string path, JsonNode data)
    {
        var selector = JsonSelector.Parse(path);
        return selector.Select(data).FirstOrDefault();
    }

    public string GetPath(JsonNode node)
    {
        // TODO: System.Text.Json doesn't maintain path on nodes; need to track manually
        throw new NotImplementedException("Path tracking not yet implemented — see tasks.md");
    }

    public JsonNode? GetParent(JsonNode node, int levels = 1)
    {
        var current = node.Parent;
        for (int i = 1; i < levels && current != null; i++)
            current = current.Parent;
        return current;
    }

    public string ResolveRelativePath(string relativePath, JsonNode currentNode, JsonNode dataContext)
    {
        // TODO: implement @ and <-- resolution
        throw new NotImplementedException();
    }

    public void EnsurePath(string path, JsonNode root, INodeAdapter<JsonNode> adapter)
    {
        // TODO: implement path creation logic
        throw new NotImplementedException();
    }

    public (string parentPath, string leafName) SplitParentAndLeaf(string path)
    {
        // TODO: port JsonSplittedPath logic
        throw new NotImplementedException();
    }

    public string? ProcessIndirectPath(string path, JsonNode data)
    {
        // TODO: port indirect expression handling from JLio
        if (!path.Contains("=indirect(")) return path;
        throw new NotImplementedException();
    }

    public IEnumerable<string> GetIntellisense(string partialPath, JsonNode data)
    {
        // TODO: implement property suggestions
        return Enumerable.Empty<string>();
    }
}
