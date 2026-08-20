using TLio.Core.Contracts;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

/// <summary>
/// IItemsFetcher for YAML using dot-notation paths:
///   $           = root
///   $.name      = child key "name"
///   $.a.b.c     = nested path
///   $.items[0]  = index into sequence
///   $.items[*]  = all elements of sequence
///   $..name     = key "name" at any depth (recursive descent)
///   $.a..name   = key "name" at any depth below $.a
/// </summary>
public class YamlPathItemsFetcher : IItemsFetcher<YamlNode>
{
    private readonly YamlParentTracker _tracker;

    public YamlPathItemsFetcher(YamlParentTracker tracker) => _tracker = tracker;

    public string RootPathIndicator => "$";
    public string PathDelimiter => ".";
    public string CurrentItemPathIndicator => "@";
    public string ParentPathIndicator => "<--";
    public string ArrayCloseChar => "]";

    public SelectedNodes<YamlNode> SelectNodes(string path, YamlNode data)
        => new SelectedNodes<YamlNode>(Traverse(path, data));

    public YamlNode? SelectNode(string path, YamlNode data)
    {
        var results = Traverse(path, data);
        return results.Count > 0 ? results[0] : null;
    }

    public string GetPath(YamlNode node)
    {
        var segments = new List<string>();
        var current = node;

        while (_tracker.TryGetParent(current, out var info))
        {
            if (info.Key != null)
                segments.Insert(0, info.Key);
            else if (info.Index.HasValue)
                segments.Insert(0, $"[{info.Index.Value}]");
            current = info.Parent;
        }

        if (segments.Count == 0)
            return RootPathIndicator;

        var sb = new System.Text.StringBuilder(RootPathIndicator);
        foreach (var seg in segments)
        {
            if (seg.StartsWith("["))
                sb.Append(seg);
            else
            {
                sb.Append(PathDelimiter);
                sb.Append(seg);
            }
        }
        return sb.ToString();
    }

    public YamlNode? GetParent(YamlNode node, int levels = 1)
    {
        if (levels == 0) return node;
        var current = _tracker.GetParentNode(node);
        for (int i = 1; i < levels && current != null; i++)
            current = _tracker.GetParentNode(current);
        return current;
    }

    public string ResolveRelativePath(string relativePath, YamlNode currentNode, YamlNode dataContext)
    {
        if (string.IsNullOrEmpty(relativePath))
            return GetPath(currentNode);

        if (relativePath == CurrentItemPathIndicator)
            return GetPath(currentNode);

        if (relativePath.StartsWith(CurrentItemPathIndicator + PathDelimiter, StringComparison.Ordinal))
        {
            var after = relativePath.Substring(CurrentItemPathIndicator.Length + PathDelimiter.Length);
            var currentPath = GetPath(currentNode);
            return currentPath == RootPathIndicator
                ? RootPathIndicator + PathDelimiter + after
                : currentPath + PathDelimiter + after;
        }

        if (relativePath == CurrentItemPathIndicator + PathDelimiter + ParentPathIndicator)
        {
            var parent = GetParent(currentNode, 1);
            return parent == null ? RootPathIndicator : GetPath(parent);
        }

        return relativePath;
    }

    /// <summary>
    /// True when the leaf is reached by a recursive descent ("$..name"). The command layer
    /// then selects the leaf nodes by the whole path and replaces each in place, because there
    /// is no single parent to write a property on.
    /// </summary>
    public bool IsLeafRecursiveDescentSearch(string path)
    {
        var segments = ParseSegments(path);
        return segments.Count > 0 && segments[^1].IsDescendant;
    }

    public void EnsurePath(string path, YamlNode root, INodeAdapter<YamlNode> adapter)
    {
        var segments = ParseSegments(path);

        // A wildcard or a recursive descent describes nodes that already exist rather than a
        // single place to build, so there is nothing to construct along such a path.
        if (segments.Any(s => s.IsWildcard || s.IsDescendant))
            return;

        // Decided before anything is written. A path that runs through a position inside
        // something that is not a sequence cannot be built, and building the part above it
        // anyway left a wrong shape behind — "$.rows[0].id" produced "rows: {id: {}}", the
        // position silently dropped, where JSON and XML build nothing at all.
        if (!CanBuild(segments, root))
            return;

        var current = root;

        foreach (var seg in segments)
        {
            if (seg.IsIndex)
            {
                var seq = (YamlSequenceNode)current;
                while (seq.Children.Count <= seg.Index)
                    seq.Add(new YamlMappingNode());
                current = seq.Children[seg.Index];
            }
            else
            {
                var map = (YamlMappingNode)current;
                var key = new YamlScalarNode(seg.Key);
                if (!map.Children.ContainsKey(key))
                {
                    var child = new YamlMappingNode();
                    map.Children[key] = child;
                    _tracker.Track(child, map, seg.Key!);
                }
                current = map.Children[key];
            }
        }
    }

    /// <summary>
    /// Walks <paramref name="segments"/> over what is already there, treating a node that is not
    /// there yet as the mapping the build would create. False as soon as a segment cannot be
    /// satisfied — a position inside anything but a sequence.
    /// </summary>
    private bool CanBuild(List<PathSegment> segments, YamlNode root)
    {
        YamlNode? node = root;

        foreach (var seg in segments)
        {
            if (seg.IsIndex)
            {
                // A node the build would create is a mapping, never a sequence.
                if (node is not YamlSequenceNode seq) return false;
                node = seg.Index < seq.Children.Count ? seq.Children[seg.Index] : null;
            }
            else
            {
                if (node is null) continue;               // a mapping that will be created
                if (node is not YamlMappingNode map) return false;
                node = map.Children.TryGetValue(new YamlScalarNode(seg.Key), out var child) ? child : null;
            }
        }

        return true;
    }

    public (string parentPath, string leafName) SplitParentAndLeaf(string path)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return (RootPathIndicator, string.Empty);

        var segments = ParseSegments(path);
        if (segments.Count == 0)
            return (RootPathIndicator, string.Empty);

        var leaf = segments[^1];
        var leafStr = leaf.IsIndex ? $"[{leaf.Index}]"
                    : leaf.IsWildcard ? "[*]"
                    : leaf.Key!;

        if (segments.Count == 1)
            return (RootPathIndicator, leafStr);

        return (Render(segments.Take(segments.Count - 1)), leafStr);
    }

    /// <summary>
    /// Writes segments back out as a path string. Every segment kind has to round-trip here:
    /// a wildcard rendered as a plain key produced "$.items." — a path that parses back to
    /// the sequence itself, so a command aimed at "$.items[*].n" landed on the array instead
    /// of on each element.
    /// </summary>
    private string Render(IEnumerable<PathSegment> segments)
    {
        var sb = new System.Text.StringBuilder(RootPathIndicator);
        foreach (var s in segments)
        {
            if (s.IsIndex) sb.Append('[').Append(s.Index).Append(']');
            else if (s.IsWildcard) sb.Append("[*]");
            else if (s.IsDescendant) sb.Append(PathDelimiter).Append(PathDelimiter).Append(s.Key);
            else sb.Append(PathDelimiter).Append(s.Key);
        }
        return sb.ToString();
    }

    public string? ProcessIndirectPath(string path, YamlNode data)
        => path.Contains("=indirect(") ? null : path;

    public IEnumerable<string> GetIntellisense(string partialPath, YamlNode data)
        => Enumerable.Empty<string>();

    private List<YamlNode> Traverse(string path, YamlNode data)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return new List<YamlNode> { data };

        var segments = ParseSegments(path);
        var current = new List<YamlNode> { data };

        foreach (var seg in segments)
        {
            var next = new List<YamlNode>();

            foreach (var node in current)
            {
                if (seg.IsWildcard)
                {
                    if (node is YamlSequenceNode seq)
                    {
                        int idx = 0;
                        foreach (var item in seq.Children)
                        {
                            _tracker.Track(item, seq, idx++);
                            next.Add(item);
                        }
                    }
                }
                else if (seg.IsIndex)
                {
                    if (node is YamlSequenceNode seq && seg.Index < seq.Children.Count)
                    {
                        var item = seq.Children[seg.Index];
                        _tracker.Track(item, seq, seg.Index);
                        next.Add(item);
                    }
                }
                else if (seg.IsDescendant)
                {
                    CollectDescendants(node, seg.Key!, next);
                }
                else
                {
                    if (node is YamlMappingNode map)
                    {
                        var key = new YamlScalarNode(seg.Key);
                        if (map.Children.TryGetValue(key, out var child))
                        {
                            _tracker.Track(child, map, seg.Key!);
                            next.Add(child);
                        }
                    }
                }
            }

            current = next;
            if (current.Count == 0) break;
        }

        return current;
    }

    /// <summary>
    /// Depth-first walk collecting every value stored under <paramref name="key"/>, at any
    /// depth below <paramref name="node"/>. Matches are recorded in document order and each is
    /// tracked so it can later be pathed, replaced or removed.
    /// </summary>
    private void CollectDescendants(YamlNode node, string key, List<YamlNode> results)
    {
        switch (node)
        {
            case YamlMappingNode map:
                foreach (var (k, v) in map.Children)
                {
                    var keyText = (k as YamlScalarNode)?.Value;
                    if (keyText == key)
                    {
                        _tracker.Track(v, map, key);
                        results.Add(v);
                    }
                    if (keyText != null) _tracker.Track(v, map, keyText);
                    CollectDescendants(v, key, results);
                }
                break;
            case YamlSequenceNode seq:
                for (var i = 0; i < seq.Children.Count; i++)
                {
                    _tracker.Track(seq.Children[i], seq, i);
                    CollectDescendants(seq.Children[i], key, results);
                }
                break;
        }
    }

    private List<PathSegment> ParseSegments(string path)
    {
        var s = path;
        if (s.StartsWith(RootPathIndicator))
            s = s.Substring(RootPathIndicator.Length);

        if (string.IsNullOrEmpty(s))
            return new List<PathSegment>();

        var segments = new List<PathSegment>();
        var i = 0;

        while (i < s.Length)
        {
            if (s[i] == '.' && i + 1 < s.Length && s[i + 1] == '.')
            {
                // ".." marks a recursive descent; the key that follows is searched at any depth.
                i += 2;
                var end = i;
                while (end < s.Length && s[end] != '.' && s[end] != '[')
                    end++;
                var descendantKey = s.Substring(i, end - i);
                if (!string.IsNullOrEmpty(descendantKey))
                    segments.Add(PathSegment.Descendant(descendantKey));
                i = end;
            }
            else if (s[i] == '[')
            {
                i++; // skip '['
                if (i < s.Length && (s[i] == '\'' || s[i] == '"'))
                {
                    // Bracket-quoted key: ['key.with.special'] or ["key"]
                    var q = s[i];
                    i++; // skip opening quote
                    var closeSeq = $"{q}]";
                    var end = s.IndexOf(closeSeq, i, StringComparison.Ordinal);
                    if (end < 0) end = s.Length;
                    segments.Add(new PathSegment(s.Substring(i, end - i)));
                    i = end < s.Length ? end + closeSeq.Length : s.Length;
                }
                else
                {
                    // Numeric index or wildcard: [n] or [*]
                    var end = s.IndexOf(']', i);
                    if (end < 0) end = s.Length;
                    var bracket = s.Substring(i, end - i);
                    if (bracket == "*")
                        segments.Add(PathSegment.Wildcard());
                    else if (int.TryParse(bracket, out var idx))
                        segments.Add(PathSegment.ArrayIndex(idx));
                    i = end < s.Length ? end + 1 : s.Length;
                }
            }
            else if (s[i] == '.')
            {
                i++; // skip delimiter between segments
            }
            else
            {
                // Plain key segment: read until next '.' or '['
                var end = i;
                while (end < s.Length && s[end] != '.' && s[end] != '[')
                    end++;
                var key = s.Substring(i, end - i);
                if (!string.IsNullOrEmpty(key))
                    segments.Add(new PathSegment(key));
                i = end;
            }
        }

        return segments;
    }

    private readonly struct PathSegment
    {
        public readonly string? Key;
        public readonly int Index;
        public readonly bool IsIndex;
        public readonly bool IsWildcard;
        public readonly bool IsDescendant;

        public PathSegment(string key)
        {
            Key = key; Index = 0; IsIndex = false; IsWildcard = false; IsDescendant = false;
        }

        private PathSegment(int index, bool wildcard)
        {
            Key = null; Index = index; IsIndex = !wildcard; IsWildcard = wildcard; IsDescendant = false;
        }

        private PathSegment(string key, bool descendant)
        {
            Key = key; Index = 0; IsIndex = false; IsWildcard = false; IsDescendant = descendant;
        }

        public static PathSegment ArrayIndex(int idx) => new(idx, false);
        public static PathSegment Wildcard() => new(0, true);
        public static PathSegment Descendant(string key) => new(key, true);
    }
}
