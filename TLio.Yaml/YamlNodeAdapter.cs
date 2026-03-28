using TLio.Core.Contracts;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

/// <summary>
/// INodeAdapter implementation for YamlDotNet's YamlNode model.
/// Stub — all members throw NotImplementedException until the YAML adapter is built.
/// See specs/001-tlio-core-architecture/tasks.md for the implementation backlog.
/// </summary>
public class YamlNodeAdapter : INodeAdapter<YamlNode>
{
    public bool IsObject(YamlNode node) => node is YamlMappingNode;
    public bool IsArray(YamlNode node) => node is YamlSequenceNode;
    public bool IsPrimitive(YamlNode node) => node is YamlScalarNode;
    public bool IsNull(YamlNode node) =>
        node is YamlScalarNode scalar && (scalar.Value == null || scalar.Value == "null" || scalar.Value == "~");

    public bool HasProperty(YamlNode node, string propertyName) =>
        node is YamlMappingNode map && map.Children.ContainsKey(new YamlScalarNode(propertyName));

    public YamlNode? GetProperty(YamlNode node, string propertyName)
    {
        if (node is YamlMappingNode map && map.Children.TryGetValue(new YamlScalarNode(propertyName), out var val))
            return val;
        return null;
    }

    public void SetProperty(YamlNode node, string propertyName, YamlNode value)
    {
        if (node is YamlMappingNode map)
            map.Children[new YamlScalarNode(propertyName)] = value;
    }

    public void RemoveProperty(YamlNode node, string propertyName)
    {
        if (node is YamlMappingNode map)
            map.Children.Remove(new YamlScalarNode(propertyName));
    }

    public IEnumerable<string> GetPropertyNames(YamlNode node)
    {
        if (node is YamlMappingNode map)
            return map.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? string.Empty);
        return Enumerable.Empty<string>();
    }

    public void AppendToArray(YamlNode array, YamlNode value)
    {
        if (array is YamlSequenceNode seq) seq.Add(value);
    }

    public void InsertIntoArray(YamlNode array, int index, YamlNode value) =>
        throw new NotImplementedException();

    public void RemoveFromArray(YamlNode array, int index) =>
        throw new NotImplementedException();

    public int GetArrayLength(YamlNode array) =>
        array is YamlSequenceNode seq ? seq.Children.Count : 0;

    public YamlNode GetArrayElement(YamlNode array, int index) =>
        array is YamlSequenceNode seq ? seq.Children[index] : new YamlScalarNode("null");

    public IEnumerable<YamlNode> GetArrayElements(YamlNode array) =>
        array is YamlSequenceNode seq ? seq.Children : Enumerable.Empty<YamlNode>();

    public YamlNode CreateNull() => new YamlScalarNode("null");
    public YamlNode CreateObject() => new YamlMappingNode();
    public YamlNode CreateArray() => new YamlSequenceNode();
    public YamlNode CreateString(string value) => new YamlScalarNode(value);
    public YamlNode CreateNumber(double value) => new YamlScalarNode(value.ToString());
    public YamlNode CreateBoolean(bool value) => new YamlScalarNode(value.ToString().ToLower());
    public YamlNode CreateValue(object? value) =>
        value == null ? CreateNull() : new YamlScalarNode(value.ToString());

    public object? GetValue(YamlNode node) =>
        node is YamlScalarNode scalar ? scalar.Value : null;

    public T? GetValue<T>(YamlNode node) => throw new NotImplementedException();

    public bool? TryGetBoolean(YamlNode node) => throw new NotImplementedException();
    public double? TryGetDouble(YamlNode node) => throw new NotImplementedException();
    public string? TryGetString(YamlNode node) => throw new NotImplementedException();

    public YamlNode DeepClone(YamlNode node) =>
        // TODO: proper deep-clone; YamlDotNet nodes don't expose a built-in clone
        throw new NotImplementedException();

    public void Replace(YamlNode target, YamlNode replacement) =>
        throw new NotImplementedException();
    public bool RemoveFromParent(YamlNode node) => throw new NotImplementedException();
    public void DeepMergeInto(YamlNode source, YamlNode target, ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat) =>
        throw new NotImplementedException();
    public YamlNode? GetParentNode(YamlNode node) => throw new NotImplementedException();
    public string? GetParentPropertyName(YamlNode node) => throw new NotImplementedException();
    public bool DeepEquals(YamlNode a, YamlNode b) => throw new NotImplementedException();

    public YamlNode Parse(string content)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(content));
        return yaml.Documents[0].RootNode;
    }

    public string Serialize(YamlNode node, bool pretty = false)
    {
        var stream = new YamlStream(new YamlDocument(node));
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }
}
