using System.Text.Json;
using System.Text.Json.Nodes;
using TLio.Core.Contracts;

namespace TLio.Json.SystemText;

/// <summary>
/// INodeAdapter implementation for System.Text.Json's JsonNode model.
///
/// Behavioral contract: must produce identical transformation results to
/// JsonNodeAdapter (Newtonsoft) for all operations.
/// </summary>
public class SystemTextJsonNodeAdapter : INodeAdapter<JsonNode>
{
    // ── Type queries ──────────────────────────────────────────────────────────

    public bool IsObject(JsonNode node) => node is JsonObject;
    public bool IsArray(JsonNode node) => node is JsonArray;
    public bool IsPrimitive(JsonNode node) => node is JsonValue;
    public bool IsNull(JsonNode node) => node == null || (node is JsonValue v && v.TryGetValue<object>(out var o) && o == null);

    /// <summary>JSON carries its own types, so report the value kind rather than guessing.</summary>
    public NodeKind GetNodeKind(JsonNode node)
    {
        if (IsNull(node)) return NodeKind.Null;
        if (node is JsonObject) return NodeKind.Object;
        if (node is JsonArray) return NodeKind.Array;
        if (node is not JsonValue value) return NodeKind.Null;

        // A JsonValue is backed either by a JsonElement (parsed document) or a CLR value
        // (constructed node); both have to be inspected without coercion.
        if (value.TryGetValue<JsonElement>(out var element))
            return element.ValueKind switch
            {
                JsonValueKind.Object                          => NodeKind.Object,
                JsonValueKind.Array                           => NodeKind.Array,
                JsonValueKind.Number                          => NodeKind.Number,
                JsonValueKind.True or JsonValueKind.False      => NodeKind.Boolean,
                JsonValueKind.Null or JsonValueKind.Undefined  => NodeKind.Null,
                _                                             => NodeKind.String
            };

        if (value.TryGetValue<string>(out _)) return NodeKind.String;
        if (value.TryGetValue<bool>(out _)) return NodeKind.Boolean;
        if (value.TryGetValue<double>(out _) || value.TryGetValue<long>(out _) ||
            value.TryGetValue<decimal>(out _)) return NodeKind.Number;
        return NodeKind.String;
    }

    // ── Object operations ─────────────────────────────────────────────────────

    public bool HasProperty(JsonNode node, string propertyName) =>
        node is JsonObject obj && obj.ContainsKey(propertyName);

    public JsonNode? GetProperty(JsonNode node, string propertyName) =>
        node is JsonObject obj ? obj[propertyName] : null;

    public void SetProperty(JsonNode node, string propertyName, JsonNode value)
    {
        if (node is JsonObject obj)
        {
            // Clone if value already has a parent — JsonNode can only live in one parent at a time.
            obj[propertyName] = value?.Parent != null ? value.DeepClone() : value;
        }
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
        if (array is JsonArray arr)
            arr.Add(value?.Parent != null ? value.DeepClone() : value);
    }

    public void InsertIntoArray(JsonNode array, int index, JsonNode value)
    {
        if (array is JsonArray arr)
            arr.Insert(index, value?.Parent != null ? value.DeepClone() : value);
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
            // A JsonValue created from a CLR long (e.g. an integral script literal) is not
            // convertible to double by TryGetValue — read it in its own type.
            if (v.TryGetValue<long>(out var l)) return l;
            if (v.TryGetValue<bool>(out var b)) return b;
            return null;
        }
        return null;
    }

    public T? GetValue<T>(JsonNode node) =>
        node is JsonValue v && v.TryGetValue<T>(out var result) ? result : default;

    // ── Type coercion ─────────────────────────────────────────────────────────

    /// <summary>
    /// Coerce to bool?. Handles boolean JsonValue and "true"/"false" strings.
    /// Mirrors JsonNodeAdapter (Newtonsoft) behavior.
    /// </summary>
    public bool? TryGetBoolean(JsonNode node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<bool>(out var b)) return b;
        if (v.TryGetValue<string>(out var s) && bool.TryParse(s, out var parsed)) return parsed;
        return null;
    }

    /// <summary>
    /// Coerce to double?. Handles all numeric JsonValue types and numeric strings.
    /// Returns null for null or non-numeric nodes.
    /// </summary>
    public double? TryGetDouble(JsonNode node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<decimal>(out var dec)) return (double)dec;
        if (v.TryGetValue<string>(out var s) && double.TryParse(s, out var parsed)) return parsed;
        return null;
    }

    /// <summary>
    /// Coerce to string?. Returns null only for null nodes.
    /// For all other JsonValue types returns the runtime string representation —
    /// booleans → "True"/"False" (capital T/F) to match Newtonsoft's JToken.ToString().
    /// </summary>
    public string? TryGetString(JsonNode node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<string>(out var s)) return s;
        if (v.TryGetValue<bool>(out var b)) return b.ToString();   // "True" / "False"
        if (v.TryGetValue<double>(out var d)) return d.ToString();
        if (v.TryGetValue<long>(out var l)) return l.ToString();
        if (v.TryGetValue<decimal>(out var dec)) return dec.ToString();
        // Fallback for any other boxed type
        try
        {
            var raw = v.GetValue<object>();
            return raw?.ToString();
        }
        catch
        {
            return null;
        }
    }

    // ── Cloning & replacement ─────────────────────────────────────────────────

    public JsonNode DeepClone(JsonNode node) => node.DeepClone();

    /// <summary>
    /// Replace <paramref name="target"/> in-place with <paramref name="replacement"/>.
    /// Finds the target in its parent JsonObject or JsonArray and swaps.
    /// Always uses a deep clone of the replacement to avoid "node already has a parent"
    /// exceptions when the same value node is used in multiple Replace calls
    /// (e.g. recursive-descent paths like $..score).
    /// </summary>
    public void Replace(JsonNode target, JsonNode replacement)
    {
        // Clone replacement (or null for JSON null) so the new node is always parentless.
        // JsonNode can only live in one parent at a time; CreateNull() returns C# null
        // in .NET 10+ because System.Text.Json represents JSON null as C# null.
        JsonNode? value = replacement?.DeepClone();

        var parent = target.Parent;
        if (parent is JsonObject obj)
        {
            string? key = FindKeyInObject(obj, target);
            if (key != null)
            {
                obj.Remove(key);
                obj[key] = value;
            }
        }
        else if (parent is JsonArray arr)
        {
            int idx = FindIndexInArray(arr, target);
            if (idx >= 0)
                arr[idx] = value;
        }
    }

    /// <summary>
    /// Remove <paramref name="node"/> from its parent container.
    /// Returns false when the node has no removable parent (e.g. root).
    /// Ported from JLio's JsonMethods.RemoveItemFromTarget.
    /// </summary>
    public bool RemoveFromParent(JsonNode node)
    {
        var parent = node.Parent;
        if (parent is JsonObject obj)
        {
            string? key = FindKeyInObject(obj, node);
            if (key != null) { obj.Remove(key); return true; }
        }
        else if (parent is JsonArray arr)
        {
            int idx = FindIndexInArray(arr, node);
            if (idx >= 0) { arr.RemoveAt(idx); return true; }
        }
        return false;
    }

    // ── Deep merge ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recursively merge <paramref name="source"/> into <paramref name="target"/>.
    /// Mirrors JsonNodeAdapter (Newtonsoft) DeepMergeInto behavior.
    /// </summary>
    public void DeepMergeInto(JsonNode source, JsonNode target,
        ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat)
    {
        if (source is JsonObject sourceObj && target is JsonObject targetObj)
        {
            foreach (var kvp in sourceObj.ToList())
            {
                if (targetObj.ContainsKey(kvp.Key))
                {
                    var targetProp = targetObj[kvp.Key];
                    if (kvp.Value is JsonObject && targetProp is JsonObject)
                    {
                        DeepMergeInto(kvp.Value!, targetProp!, arrayMergeMode);
                    }
                    else if (kvp.Value is JsonArray sourceArr
                             && targetProp is JsonArray targetArr
                             && arrayMergeMode == ArrayMergeMode.Concat)
                    {
                        foreach (var item in sourceArr)
                            targetArr.Add(item?.DeepClone());
                    }
                    else
                    {
                        targetObj[kvp.Key] = kvp.Value?.DeepClone();
                    }
                }
                else
                {
                    targetObj.Add(kvp.Key, kvp.Value?.DeepClone());
                }
            }
        }
        else if (source is JsonArray sourceArray && target is JsonArray targetArray)
        {
            switch (arrayMergeMode)
            {
                case ArrayMergeMode.Concat:
                case ArrayMergeMode.MergeByKey:
                    foreach (var item in sourceArray)
                        targetArray.Add(item?.DeepClone());
                    break;
                case ArrayMergeMode.Replace:
                    targetArray.Clear();
                    foreach (var item in sourceArray)
                        targetArray.Add(item?.DeepClone());
                    break;
            }
        }
        else
        {
            // Type mismatch or both primitives — replace target in parent
            Replace(target, source.DeepClone());
        }
    }

    // ── Parent access ─────────────────────────────────────────────────────────

    /// <summary>
    /// Return the semantic parent of <paramref name="node"/>.
    /// In System.Text.Json there are no JProperty wrappers, so:
    /// • Array element whose parent array is itself a property value → return the JsonObject
    /// • Otherwise → return the direct parent
    /// </summary>
    public JsonNode? GetParentNode(JsonNode node)
    {
        if (node?.Parent == null) return null;
        var parent = node.Parent;

        // Element in an array that is a property of an object → skip array, return object
        if (parent is JsonArray && parent.Parent is JsonObject)
            return parent.Parent;

        return parent;
    }

    /// <summary>
    /// Return the property name under which <paramref name="node"/> lives in its parent
    /// JsonObject, or null when the node is an array element or has no parent.
    /// </summary>
    public string? GetParentPropertyName(JsonNode node)
    {
        if (node?.Parent is JsonObject parentObj)
            return FindKeyInObject(parentObj, node);
        return null;
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    public bool DeepEquals(JsonNode a, JsonNode b) =>
        JsonNode.DeepEquals(a, b);

    // ── Serialisation ─────────────────────────────────────────────────────────

    public JsonNode Parse(string content) => JsonNode.Parse(content)!;

    public string Serialize(JsonNode node, bool pretty = false) =>
        node.ToJsonString(new JsonSerializerOptions { WriteIndented = pretty });

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string? FindKeyInObject(JsonObject obj, JsonNode target)
    {
        foreach (var kvp in obj)
        {
            if (ReferenceEquals(kvp.Value, target))
                return kvp.Key;
        }
        return null;
    }

    private static int FindIndexInArray(JsonArray arr, JsonNode target)
    {
        for (int i = 0; i < arr.Count; i++)
        {
            if (ReferenceEquals(arr[i], target))
                return i;
        }
        return -1;
    }
}
