using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

/// <summary>
/// Tracks parent-child relationships for YamlNode trees.
/// YamlDotNet nodes have no built-in Parent property, so we maintain a
/// dictionary keyed by child node (reference equality) that stores the
/// parent container and the key or index under which the child lives.
/// </summary>
public class YamlParentTracker
{
    private readonly Dictionary<YamlNode, ParentInfo> _parents =
        new(ReferenceEqualityComparer.Instance);

    public void Track(YamlNode child, YamlMappingNode parent, string key)
        => _parents[child] = new ParentInfo(parent, key, null);

    public void Track(YamlNode child, YamlSequenceNode parent, int index)
        => _parents[child] = new ParentInfo(parent, null, index);

    public bool TryGetParent(YamlNode node, out ParentInfo info)
        => _parents.TryGetValue(node, out info!);

    public YamlNode? GetParentNode(YamlNode node)
        => _parents.TryGetValue(node, out var info) ? info.Parent : null;

    public string? GetParentKey(YamlNode node)
        => _parents.TryGetValue(node, out var info) ? info.Key : null;

    public int? GetParentIndex(YamlNode node)
        => _parents.TryGetValue(node, out var info) ? info.Index : null;

    public void TrackChildren(YamlMappingNode mapping)
    {
        foreach (var (key, value) in mapping.Children)
        {
            var keyStr = (key as YamlScalarNode)?.Value ?? key.ToString();
            Track(value, mapping, keyStr!);
        }
    }

    public void TrackChildren(YamlSequenceNode sequence)
    {
        int i = 0;
        foreach (var child in sequence.Children)
            Track(child, sequence, i++);
    }

    public readonly struct ParentInfo
    {
        public readonly YamlNode Parent;
        public readonly string? Key;
        public readonly int? Index;

        public ParentInfo(YamlNode parent, string? key, int? index)
        {
            Parent = parent;
            Key    = key;
            Index  = index;
        }
    }
}
