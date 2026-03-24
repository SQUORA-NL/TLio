using Newtonsoft.Json.Linq;
using TLio.Core.Contracts;

namespace TLio.Json;

/// <summary>
/// INodeAdapter implementation for Newtonsoft.Json's JToken model.
/// Provides all format-specific node manipulation so that commands remain agnostic.
/// </summary>
public class JsonNodeAdapter : INodeAdapter<JToken>
{
    // ── Type queries ──────────────────────────────────────────────────────────

    public bool IsObject(JToken node) => node is JObject;
    public bool IsArray(JToken node) => node is JArray;
    public bool IsPrimitive(JToken node) => node is JValue;
    public bool IsNull(JToken node) => node.Type == JTokenType.Null;

    // ── Object operations ─────────────────────────────────────────────────────

    public bool HasProperty(JToken node, string propertyName) =>
        node is JObject obj && obj.ContainsKey(propertyName);

    public JToken? GetProperty(JToken node, string propertyName) =>
        node is JObject obj ? obj[propertyName] : null;

    public void SetProperty(JToken node, string propertyName, JToken value)
    {
        if (node is JObject obj)
            obj[propertyName] = value;
    }

    public void RemoveProperty(JToken node, string propertyName)
    {
        if (node is JObject obj)
            obj.Remove(propertyName);
    }

    public IEnumerable<string> GetPropertyNames(JToken node) =>
        node is JObject obj ? obj.Properties().Select(p => p.Name) : Enumerable.Empty<string>();

    // ── Array operations ──────────────────────────────────────────────────────

    public void AppendToArray(JToken array, JToken value)
    {
        if (array is JArray arr) arr.Add(value);
    }

    public void InsertIntoArray(JToken array, int index, JToken value)
    {
        if (array is JArray arr) arr.Insert(index, value);
    }

    public void RemoveFromArray(JToken array, int index)
    {
        if (array is JArray arr) arr.RemoveAt(index);
    }

    public int GetArrayLength(JToken array) => array is JArray arr ? arr.Count : 0;

    public JToken GetArrayElement(JToken array, int index) =>
        array is JArray arr ? arr[index] : JValue.CreateNull();

    public IEnumerable<JToken> GetArrayElements(JToken array) =>
        array is JArray arr ? arr : Enumerable.Empty<JToken>();

    // ── Node creation ─────────────────────────────────────────────────────────

    public JToken CreateNull() => JValue.CreateNull();
    public JToken CreateObject() => new JObject();
    public JToken CreateArray() => new JArray();
    public JToken CreateString(string value) => new JValue(value);
    public JToken CreateNumber(double value) => new JValue(value);
    public JToken CreateBoolean(bool value) => new JValue(value);
    public JToken CreateValue(object? value) => value == null ? JValue.CreateNull() : new JValue(value);

    // ── Value access ──────────────────────────────────────────────────────────

    public object? GetValue(JToken node) => node is JValue v ? v.Value : null;
    public T? GetValue<T>(JToken node) => node.Value<T>();

    // ── Type coercion ─────────────────────────────────────────────────────────

    /// <summary>
    /// Coerce a JToken to bool?. Returns null if the node is not a boolean JValue or
    /// cannot be converted. Handles JValue(bool) and JValue(string "true"/"false").
    /// </summary>
    public bool? TryGetBoolean(JToken node)
    {
        if (node is not JValue v) return null;
        if (v.Type == JTokenType.Boolean) return v.Value<bool>();
        if (v.Type == JTokenType.String)
        {
            var s = v.Value<string>();
            if (bool.TryParse(s, out var b)) return b;
        }
        return null;
    }

    /// <summary>
    /// Coerce a JToken to double?. Handles all numeric JValue types and numeric strings.
    /// Returns null if the node is null, not a JValue, or cannot be converted.
    /// </summary>
    public double? TryGetDouble(JToken node)
    {
        if (node is not JValue v) return null;
        if (v.Type == JTokenType.Null) return null;
        try
        {
            return v.Value<double?>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Coerce a JToken to string?. Returns null only when the node is a JSON null.
    /// For all other JValue types, returns the string representation via JToken.ToString().
    /// </summary>
    public string? TryGetString(JToken node)
    {
        if (node.Type == JTokenType.Null) return null;
        if (node is JValue v) return v.ToString();
        return null;
    }

    // ── Cloning & replacement ─────────────────────────────────────────────────

    public JToken DeepClone(JToken node) => node.DeepClone();

    public void Replace(JToken target, JToken replacement) => target.Replace(replacement);

    /// <summary>
    /// Remove <paramref name="node"/> from its parent container.
    /// Handles both property values (removes the JProperty) and array elements.
    /// Returns false if the node has no removable parent (e.g. it is the document root).
    /// Ported from JLio's JsonMethods.RemoveItemFromTarget.
    /// </summary>
    public bool RemoveFromParent(JToken node)
    {
        var parent = node.Parent;
        switch (parent?.Type)
        {
            case JTokenType.Property:
                // The node is the value of a JProperty — remove the property itself
                parent.Remove();
                return true;

            case JTokenType.Array:
                var array = (JArray)parent;
                var index = array.IndexOf(node);
                array.RemoveAt(index);
                return true;

            default:
                return false;
        }
    }

    // ── Deep merge ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recursively merges <paramref name="source"/> into <paramref name="target"/>.
    /// Objects: properties are merged recursively; missing properties are added.
    /// Arrays: behaviour is controlled by <paramref name="arrayMergeMode"/>.
    /// Primitive/type-mismatch: target is replaced by a deep clone of source.
    ///
    /// Based on the merge logic in JLio's Merge command and CopyMove root-merge path.
    /// </summary>
    public void DeepMergeInto(JToken source, JToken target,
        ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat)
    {
        if (source is JObject sourceObj && target is JObject targetObj)
        {
            foreach (var property in sourceObj.Properties())
            {
                if (targetObj.ContainsKey(property.Name))
                {
                    var targetProp = targetObj[property.Name];
                    if (property.Value is JObject && targetProp is JObject)
                    {
                        // Recurse into nested objects
                        DeepMergeInto(property.Value, targetProp, arrayMergeMode);
                    }
                    else if (property.Value is JArray sourceArr
                             && targetProp is JArray targetArr
                             && arrayMergeMode == ArrayMergeMode.Concat)
                    {
                        // Concat mode: append source elements to target array
                        foreach (var item in sourceArr)
                            targetArr.Add(item.DeepClone());
                    }
                    else
                    {
                        // Primitive, type-mismatch, or Replace/MergeByKey mode: overwrite
                        targetObj[property.Name] = property.Value.DeepClone();
                    }
                }
                else
                {
                    // Property does not exist in target — add it
                    targetObj.Add(property.Name, property.Value.DeepClone());
                }
            }
        }
        else if (source is JArray sourceArray && target is JArray targetArray)
        {
            switch (arrayMergeMode)
            {
                case ArrayMergeMode.Concat:
                    foreach (var item in sourceArray)
                        targetArray.Add(item.DeepClone());
                    break;

                case ArrayMergeMode.Replace:
                    targetArray.Clear();
                    foreach (var item in sourceArray)
                        targetArray.Add(item.DeepClone());
                    break;

                // MergeByKey is handled by the Merge command itself (requires key paths).
                // Fall back to Concat for the adapter-level merge.
                case ArrayMergeMode.MergeByKey:
                    foreach (var item in sourceArray)
                        targetArray.Add(item.DeepClone());
                    break;
            }
        }
        else
        {
            // Different types or both primitives — replace target
            target.Replace(source.DeepClone());
        }
    }

    // ── Parent access ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the logical (semantic) parent of <paramref name="node"/>, skipping
    /// intermediate Newtonsoft wrappers (JProperty, parent JArray of a property value).
    ///
    /// Rules (mirroring JLio's NavigateToSemanticParent):
    /// • Node in JArray → if array is a property value → skip JProperty → return containing JObject.
    ///   If array is root → return the array.
    /// • Node is value of JProperty → return the JObject owning that property.
    /// • Other → return direct parent.
    /// </summary>
    public JToken? GetParentNode(JToken node)
    {
        if (node?.Parent == null)
            return null;

        var parent = node.Parent;

        if (node.Parent is JArray)
        {
            // We are an element inside an array.
            if (parent.Parent is JProperty)
                return parent.Parent.Parent;  // JObject owning the property whose value is the array

            return parent;  // root-level array
        }

        if (node.Parent is JProperty property)
        {
            // We are the value of a named property — semantic parent is the owning JObject
            return property.Parent;
        }

        return parent;
    }

    /// <summary>
    /// Returns the property name under which <paramref name="node"/> lives in its parent
    /// JObject, or null when the node is an array element or has no parent.
    /// </summary>
    public string? GetParentPropertyName(JToken node)
    {
        if (node?.Parent is JProperty property)
            return property.Name;
        return null;
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    public bool DeepEquals(JToken a, JToken b) => JToken.DeepEquals(a, b);

    // ── Serialisation ─────────────────────────────────────────────────────────

    public JToken Parse(string content) => JToken.Parse(content);

    public string Serialize(JToken node, bool pretty = false) =>
        node.ToString(pretty ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
}
