using System.Text.Json;
using System.Text.Json.Nodes;
using TLio.Core.Contracts;

namespace TLio.Json.SystemText;

/// <summary>
/// INodeAdapter implementation for System.Text.Json's JsonNode model.
///
/// Stub — all members throw NotImplementedException until the adapter is built.
/// See specs/002-migration-from-jlio/tasks.md for the implementation backlog.
///
/// Behavioral contract: must produce identical transformation results to
/// JsonNodeAdapter (Newtonsoft) for all operations. All JLio tests ported to
/// use this adapter must pass with the same assertions.
/// </summary>
public class SystemTextJsonNodeAdapter : INodeAdapter<JsonNode>
{
    // ── Type queries ──────────────────────────────────────────────────────────

    public bool IsObject(JsonNode node) => node is JsonObject;
    public bool IsArray(JsonNode node) => node is JsonArray;
    public bool IsPrimitive(JsonNode node) => node is JsonValue;
    public bool IsNull(JsonNode node) => node == null || (node is JsonValue v && v.TryGetValue<object>(out var o) && o == null);

    // ── Object operations ─────────────────────────────────────────────────────

    public bool HasProperty(JsonNode node, string propertyName) =>
        node is JsonObject obj && obj.ContainsKey(propertyName);

    public JsonNode? GetProperty(JsonNode node, string propertyName) =>
        node is JsonObject obj ? obj[propertyName] : null;

    public void SetProperty(JsonNode node, string propertyName, JsonNode value)
    {
        if (node is JsonObject obj)
            obj[propertyName] = value;
    }

    public void RemoveProperty(JsonNode node, string propertyName)
    {
        if (node is JsonObject obj)
            obj.Remove(propertyName);
    }

    public IEnumerable<string> GetPropertyNames(JsonNode node) =>
        node is JsonObject obj ? obj.Select(kv => kv.Key) : Enumerable.Empty<string>();

    // ── Array operations ──────────────────────────────────────────────────────

    public void AppendToArray(JsonNode array, JsonNode value)
    {
        if (array is JsonArray arr) arr.Add(value);
    }

    public void InsertIntoArray(JsonNode array, int index, JsonNode value)
    {
        if (array is JsonArray arr) arr.Insert(index, value);
    }

    public void RemoveFromArray(JsonNode array, int index)
    {
        if (array is JsonArray arr) arr.RemoveAt(index);
    }

    public int GetArrayLength(JsonNode array) => array is JsonArray arr ? arr.Count : 0;

    public JsonNode GetArrayElement(JsonNode array, int index) =>
        array is JsonArray arr ? arr[index]! : JsonValue.Create<object?>(null)!;

    public IEnumerable<JsonNode> GetArrayElements(JsonNode array) =>
        array is JsonArray arr ? arr.Select(n => n ?? JsonValue.Create<object?>(null)!) : Enumerable.Empty<JsonNode>();

    // ── Node creation ─────────────────────────────────────────────────────────

    public JsonNode CreateNull() => JsonValue.Create<object?>(null)!;
    public JsonNode CreateObject() => new JsonObject();
    public JsonNode CreateArray() => new JsonArray();
    public JsonNode CreateString(string value) => JsonValue.Create(value)!;
    public JsonNode CreateNumber(double value) => JsonValue.Create(value)!;
    public JsonNode CreateBoolean(bool value) => JsonValue.Create(value)!;
    public JsonNode CreateValue(object? value) =>
        value == null ? CreateNull() : JsonValue.Create(value)!;

    // ── Value access ──────────────────────────────────────────────────────────

    public object? GetValue(JsonNode node)
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            if (v.TryGetValue<double>(out var d)) return d;
            if (v.TryGetValue<bool>(out var b)) return b;
            return null;
        }
        return null;
    }

    public T? GetValue<T>(JsonNode node) =>
        node is JsonValue v && v.TryGetValue<T>(out var result) ? result : default;

    // ── Type coercion ─────────────────────────────────────────────────────────

    public bool? TryGetBoolean(JsonNode node) =>
        node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    public double? TryGetDouble(JsonNode node) =>
        node is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

    public string? TryGetString(JsonNode node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    // ── Cloning & replacement ─────────────────────────────────────────────────

    public JsonNode DeepClone(JsonNode node) => node.DeepClone();

    public void Replace(JsonNode target, JsonNode replacement)
    {
        // TODO: System.Text.Json nodes don't have a direct in-place Replace.
        // Need parent traversal: find key in parent object/array, then swap.
        throw new NotImplementedException("System.Text.Json Replace requires parent tracking — see tasks.md");
    }

    public bool RemoveFromParent(JsonNode node)
    {
        // TODO: implement — find parent and remove self
        throw new NotImplementedException("System.Text.Json RemoveFromParent requires parent tracking — see tasks.md");
    }

    // ── Deep merge ────────────────────────────────────────────────────────────

    public void DeepMergeInto(JsonNode source, JsonNode target, ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat)
    {
        // TODO: implement recursive merge
        throw new NotImplementedException();
    }

    // ── Parent access ─────────────────────────────────────────────────────────

    public JsonNode? GetParentNode(JsonNode node) => node.Parent;

    public string? GetParentPropertyName(JsonNode node)
    {
        // TODO: System.Text.Json doesn't expose the property name easily
        throw new NotImplementedException();
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    public bool DeepEquals(JsonNode a, JsonNode b) =>
        JsonNode.DeepEquals(a, b);

    // ── Serialisation ─────────────────────────────────────────────────────────

    public JsonNode Parse(string content) => JsonNode.Parse(content)!;

    public string Serialize(JsonNode node, bool pretty = false) =>
        node.ToJsonString(new JsonSerializerOptions { WriteIndented = pretty });
}
