using System.Text.Json;
using System.Text.Json.Nodes;
using JsonCons.JsonPath;

namespace TLio.JsonPath.Benchmarks;

/// <summary>
/// The strategy <c>TLio.Json.SystemText</c> used before TLio.JsonPath, reproduced as it was so it can be
/// measured: JsonCons evaluates on an immutable <see cref="JsonElement"/>, so each selection serialized
/// the live <see cref="JsonNode"/> tree, parsed it (re-using the last parse when the text was unchanged),
/// selected on the copy, and walked the live tree by each match's normalized path to hand back live nodes.
/// This is the *best case* for that design: one fetcher, the selector cached, the document unchanged.
/// </summary>
public sealed class JsonConsBaseline : IDisposable
{
    private readonly Dictionary<string, JsonSelector> _selectors = new();
    private string? _cachedJson;
    private JsonDocument? _cachedDocument;

    public List<JsonNode?> SelectNodes(string path, JsonNode data)
    {
        var json = data.ToJsonString();
        if (json != _cachedJson)
        {
            _cachedDocument?.Dispose();
            _cachedDocument = JsonDocument.Parse(json);
            _cachedJson = json;
        }

        if (!_selectors.TryGetValue(path, out var selector))
            _selectors[path] = selector = JsonSelector.Parse(path);

        var results = new List<JsonNode?>();
        foreach (var pathNode in selector.SelectNodes(_cachedDocument!.RootElement))
        {
            var node = NavigateByPath(data, pathNode.Path);
            if (node != null) results.Add(node);
        }

        return results;
    }

    private static JsonNode? NavigateByPath(JsonNode root, NormalizedPath path)
    {
        JsonNode? current = root;
        foreach (var component in path)
        {
            if (current == null) return null;
            switch (component.ComponentKind)
            {
                case NormalizedPathNodeKind.Root:
                    current = root;
                    break;
                case NormalizedPathNodeKind.Name:
                    if (current is not JsonObject obj || !obj.ContainsKey(component.GetName())) return null;
                    current = obj[component.GetName()];
                    break;
                case NormalizedPathNodeKind.Index:
                    if (current is not JsonArray arr || component.GetIndex() >= arr.Count) return null;
                    current = arr[component.GetIndex()];
                    break;
                default:
                    return null;
            }
        }

        return current;
    }

    public void Dispose() => _cachedDocument?.Dispose();
}
