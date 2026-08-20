using System.Globalization;
using TLio.Core.Contracts;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

public class YamlNodeAdapter : INodeAdapter<YamlNode>
{
    private readonly YamlParentTracker _tracker;

    public YamlNodeAdapter(YamlParentTracker tracker) => _tracker = tracker;

    public bool IsObject(YamlNode node) => node is YamlMappingNode;
    public bool IsArray(YamlNode node) => node is YamlSequenceNode;
    public bool IsPrimitive(YamlNode node) => node is YamlScalarNode;
    /// <summary>
    /// A plain-style empty scalar is null too: YAML reads a bare <c>key:</c> as null, and it is
    /// the YAML spelling of the empty XML element — an unfilled container add/put may write
    /// into. A quoted <c>''</c> keeps its scalar style and stays a real empty string.
    /// </summary>
    public bool IsNull(YamlNode node) =>
        node is YamlScalarNode scalar &&
        (scalar.Value == null || scalar.Value == "null" || scalar.Value == "~" ||
         (scalar.Value.Length == 0 &&
          scalar.Style is YamlDotNet.Core.ScalarStyle.Plain or YamlDotNet.Core.ScalarStyle.Any));

    public bool HasProperty(YamlNode node, string propertyName) =>
        node is YamlMappingNode map &&
        map.Children.ContainsKey(new YamlScalarNode(propertyName));

    public YamlNode? GetProperty(YamlNode node, string propertyName)
    {
        if (node is YamlMappingNode map &&
            map.Children.TryGetValue(new YamlScalarNode(propertyName), out var val))
        {
            _tracker.Track(val, map, propertyName);
            return val;
        }
        return null;
    }

    public void SetProperty(YamlNode node, string propertyName, YamlNode value)
    {
        if (node is YamlMappingNode map)
        {
            map.Children[new YamlScalarNode(propertyName)] = value;
            _tracker.Track(value, map, propertyName);
        }
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
        if (array is YamlSequenceNode seq)
        {
            _tracker.Track(value, seq, seq.Children.Count);
            seq.Add(value);
        }
    }

    public void InsertIntoArray(YamlNode array, int index, YamlNode value)
    {
        if (array is YamlSequenceNode seq)
        {
            if (index >= seq.Children.Count)
            {
                _tracker.Track(value, seq, seq.Children.Count);
                seq.Add(value);
            }
            else
            {
                seq.Children.Insert(index, value);
                for (int i = index; i < seq.Children.Count; i++)
                    _tracker.Track(seq.Children[i], seq, i);
            }
        }
    }

    public void RemoveFromArray(YamlNode array, int index)
    {
        if (array is YamlSequenceNode seq &&
            index >= 0 && index < seq.Children.Count)
        {
            seq.Children.RemoveAt(index);
            for (int i = index; i < seq.Children.Count; i++)
                _tracker.Track(seq.Children[i], seq, i);
        }
    }

    public int GetArrayLength(YamlNode array) =>
        array is YamlSequenceNode seq ? seq.Children.Count : 0;

    public YamlNode GetArrayElement(YamlNode array, int index)
    {
        if (array is not YamlSequenceNode seq) return new YamlScalarNode("null");
        var item = seq.Children[index];
        // YamlDotNet nodes carry no parent link — register it so IItemsFetcher.GetPath
        // can render "$.items[0]" for elements reached without a path traversal.
        _tracker.Track(item, seq, index);
        return item;
    }

    public IEnumerable<YamlNode> GetArrayElements(YamlNode array)
    {
        if (array is not YamlSequenceNode seq) yield break;
        for (var i = 0; i < seq.Children.Count; i++)
        {
            _tracker.Track(seq.Children[i], seq, i);
            yield return seq.Children[i];
        }
    }

    public YamlNode CreateNull() => new YamlScalarNode("null");
    public YamlNode CreateObject() => new YamlMappingNode();
    public YamlNode CreateArray() => new YamlSequenceNode();
    // An empty string is quoted so it stays a string: a plain empty scalar is how YAML
    // spells null (see IsNull), and a deliberately created "" must not be read back as one.
    public YamlNode CreateString(string value) =>
        value.Length == 0
            ? new YamlScalarNode(value) { Style = YamlDotNet.Core.ScalarStyle.SingleQuoted }
            : new YamlScalarNode(value);

    public YamlNode CreateNumber(double value) =>
        new YamlScalarNode(value.ToString(CultureInfo.InvariantCulture));

    public YamlNode CreateBoolean(bool value) =>
        new YamlScalarNode(value.ToString().ToLowerInvariant());

    public YamlNode CreateValue(object? value) =>
        value == null ? CreateNull() : CreateString(value.ToString() ?? string.Empty);

    public object? GetValue(YamlNode node) =>
        node is YamlScalarNode scalar ? scalar.Value : null;

    public T? GetValue<T>(YamlNode node)
    {
        try
        {
            if (node is YamlScalarNode scalar && scalar.Value != null)
                return (T)Convert.ChangeType(scalar.Value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch { }
        return default;
    }

    public bool? TryGetBoolean(YamlNode node)
    {
        if (IsNull(node)) return null;
        if (node is YamlScalarNode scalar)
        {
            if (bool.TryParse(scalar.Value, out var b)) return b;
            if (scalar.Value == "yes" || scalar.Value == "on") return true;
            if (scalar.Value == "no" || scalar.Value == "off") return false;
        }
        return null;
    }

    public double? TryGetDouble(YamlNode node)
    {
        if (IsNull(node)) return null;
        if (node is YamlScalarNode scalar &&
            double.TryParse(scalar.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    public string? TryGetString(YamlNode node)
    {
        if (IsNull(node)) return null;
        return node is YamlScalarNode scalar ? scalar.Value : null;
    }

    public YamlNode DeepClone(YamlNode node) => CloneNode(node);

    public void Replace(YamlNode target, YamlNode replacement)
    {
        if (!_tracker.TryGetParent(target, out var info))
            return;

        if (info.Parent is YamlMappingNode map && info.Key != null)
        {
            map.Children[new YamlScalarNode(info.Key)] = replacement;
            _tracker.Track(replacement, map, info.Key);
        }
        else if (info.Parent is YamlSequenceNode seq && info.Index.HasValue)
        {
            seq.Children[info.Index.Value] = replacement;
            _tracker.Track(replacement, seq, info.Index.Value);
        }
    }

    public bool RemoveFromParent(YamlNode node)
    {
        if (!_tracker.TryGetParent(node, out var info))
            return false;

        if (info.Parent is YamlMappingNode map && info.Key != null)
        {
            map.Children.Remove(new YamlScalarNode(info.Key));
            return true;
        }
        if (info.Parent is YamlSequenceNode seq && info.Index.HasValue)
        {
            seq.Children.RemoveAt(info.Index.Value);
            for (int i = info.Index.Value; i < seq.Children.Count; i++)
                _tracker.Track(seq.Children[i], seq, i);
            return true;
        }
        return false;
    }

    /// <summary>
    /// A YAML node is named by the mapping key that holds it, so renaming rewrites that key.
    /// The mapping is rebuilt in its original order because YamlDotNet appends new keys, and
    /// the values are re-attached rather than cloned so existing node references stay valid.
    /// A sequence element or the document root has no key to rename.
    /// </summary>
    public bool RenameNode(YamlNode node, string newName)
    {
        if (string.IsNullOrEmpty(newName)) return false;
        if (!_tracker.TryGetParent(node, out var info)) return false;
        if (info.Parent is not YamlMappingNode map || info.Key == null) return false;
        if (info.Key == newName) return true;

        var entries = map.Children.ToList();
        map.Children.Clear();
        foreach (var (key, value) in entries)
        {
            var keyText = (key as YamlScalarNode)?.Value ?? key.ToString();
            var newKey = keyText == info.Key ? newName : keyText!;
            map.Children[new YamlScalarNode(newKey)] = value;
            _tracker.Track(value, map, newKey);
        }

        return true;
    }

    public void DeepMergeInto(YamlNode source, YamlNode target,
        ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat)
    {
        if (source is YamlScalarNode scalar)
        {
            if (target is YamlScalarNode targetScalar)
                targetScalar.Value = scalar.Value;
            return;
        }

        if (source is YamlMappingNode sourceMap && target is YamlMappingNode targetMap)
        {
            foreach (var (key, value) in sourceMap.Children)
            {
                if (targetMap.Children.TryGetValue(key, out var existing))
                {
                    if (IsArray(existing) && IsArray(value))
                    {
                        var srcSeq = (YamlSequenceNode)value;
                        var tgtSeq = (YamlSequenceNode)existing;
                        if (arrayMergeMode == ArrayMergeMode.Replace)
                        {
                            tgtSeq.Children.Clear();
                            foreach (var item in srcSeq.Children)
                                tgtSeq.Add(CloneNode(item));
                        }
                        else
                        {
                            foreach (var item in srcSeq.Children)
                                tgtSeq.Add(CloneNode(item));
                        }
                    }
                    else
                    {
                        DeepMergeInto(value, existing, arrayMergeMode);
                    }
                }
                else
                {
                    var cloned = CloneNode(value);
                    var keyStr = (key as YamlScalarNode)?.Value ?? key.ToString();
                    targetMap.Children[key] = cloned;
                    _tracker.Track(cloned, targetMap, keyStr!);
                }
            }
        }
        else if (source is YamlSequenceNode srcSeq2 && target is YamlSequenceNode tgtSeq2)
        {
            if (arrayMergeMode == ArrayMergeMode.Replace)
            {
                tgtSeq2.Children.Clear();
                foreach (var item in srcSeq2.Children)
                    tgtSeq2.Add(CloneNode(item));
            }
            else
            {
                foreach (var item in srcSeq2.Children)
                    tgtSeq2.Add(CloneNode(item));
            }
        }
    }

    public YamlNode? GetParentNode(YamlNode node) => _tracker.GetParentNode(node);

    public string? GetParentPropertyName(YamlNode node) => _tracker.GetParentKey(node);

    public bool DeepEquals(YamlNode a, YamlNode b)
    {
        if (a is YamlScalarNode sa && b is YamlScalarNode sb)
            return sa.Value == sb.Value;

        if (a is YamlMappingNode ma && b is YamlMappingNode mb)
        {
            if (ma.Children.Count != mb.Children.Count) return false;
            foreach (var (key, val) in ma.Children)
            {
                if (!mb.Children.TryGetValue(key, out var bVal)) return false;
                if (!DeepEquals(val, bVal)) return false;
            }
            return true;
        }

        if (a is YamlSequenceNode qa && b is YamlSequenceNode qb)
        {
            if (qa.Children.Count != qb.Children.Count) return false;
            for (int i = 0; i < qa.Children.Count; i++)
                if (!DeepEquals(qa.Children[i], qb.Children[i])) return false;
            return true;
        }

        return false;
    }

    public YamlNode Parse(string content)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(content));
        if (yaml.Documents.Count == 0)
            return new YamlSequenceNode();
        if (yaml.Documents.Count == 1)
            return yaml.Documents[0].RootNode;
        var sequence = new YamlSequenceNode();
        foreach (var doc in yaml.Documents)
            sequence.Add(doc.RootNode);
        return sequence;
    }

    public string Serialize(YamlNode node, bool pretty = false)
    {
        var stream = new YamlStream(new YamlDocument(node));
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    private static YamlNode CloneNode(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => new YamlScalarNode(scalar.Value),
            YamlMappingNode mapping => CloneMapping(mapping),
            YamlSequenceNode sequence => CloneSequence(sequence),
            _ => new YamlScalarNode(node.ToString())
        };
    }

    private static YamlMappingNode CloneMapping(YamlMappingNode mapping)
    {
        var newMap = new YamlMappingNode();
        foreach (var (k, v) in mapping.Children)
            newMap.Children[CloneNode(k)] = CloneNode(v);
        return newMap;
    }

    private static YamlSequenceNode CloneSequence(YamlSequenceNode sequence)
    {
        var newSeq = new YamlSequenceNode();
        foreach (var item in sequence.Children)
            newSeq.Add(CloneNode(item));
        return newSeq;
    }
}
