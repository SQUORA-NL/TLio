using TLio.Core.Contracts;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

/// <summary>
/// IItemsFetcher implementation for YAML using a dot-notation path language.
///
/// Path conventions:
///   $           = root node
///   $.name      = child key "name" of root mapping
///   $.a.b.c     = nested key path from root
///   $.items[0]  = index 0 of sequence "items"
///   $.items[*]  = wildcard: all elements of sequence "items"
///
/// The fetcher requires a shared <see cref="YamlParentTracker"/> so that
/// parent relationships discovered during traversal are visible to
/// <see cref="YamlNodeAdapter"/> operations such as Replace and RemoveFromParent.
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

    // ── Node selection ────────────────────────────────────────────────────────

    public SelectedNodes<YamlNode> SelectNodes(string path, YamlNode data)
    {
        var results = Traverse(path, data);
        return new SelectedNodes<YamlNode>(results);
    }

    public YamlNode? SelectNode(string path, YamlNode data)
    {
        var results = Traverse(path, data);
        return results.Count > 0 ? results[0] : null;
    }

    // ── Path introspection ────────────────────────────────────────────────────

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

    // ── Parent navigation ─────────────────────────────────────────────────────

    public YamlNode? GetParent(YamlNode node, int levels = 1)
    {
        if (levels == 0) return node;
        var current = _tracker.GetParentNode(node);
        for (int i = 1; i < levels && current != null; i++)
            current = _tracker.GetParentNode(current);
        return current;
    }

    // ── Relative-path resolution ───────────────────────────────────────────────

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

        // "@.<--" parent navigation
        if (relativePath == CurrentItemPathIndicator + PathDelimiter + ParentPathIndicator)
        {
            var parent = GetParent(currentNode, 1);
            return parent == null ? RootPathIndicator : GetPath(parent);
        }

        return relativePath;
    }

    // ── Path construction ─────────────────────────────────────────────────────

    public void EnsurePath(string path, YamlNode root, INodeAdapter<YamlNode> adapter)
    {
        var segments = ParseSegments(path);
        var current = root;

        foreach (var seg in segments)
        {
            if (seg.IsIndex)
            {
                if (current is YamlSequenceNode seq)
                {
                    while (seq.Children.Count <= seg.Index)
                        seq.Add(new YamlMappingNode());
                    current = seq.Children[seg.Index];
                }
            }
            else
            {
                if (current is YamlMappingNode map)
                {
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
    }

    public (string parentPath, string leafName) SplitParentAndLeaf(string path)
    {
        if (string.IsNullOrEmpty(path) || path == RootPathIndicator)
            return (RootPathIndicator, string.Empty);

        var segments = ParseSegments(path);
        if (segments.Count == 0)
            return (RootPathIndicator, string.Empty);

        var leaf = segments[segments.Count - 1];
        var leafStr = leaf.IsIndex ? $"[{leaf.Index}]" : leaf.Key!;

        if (segments.Count == 1)
            return (RootPathIndicator, leafStr);

        // Rebuild parent path
        var sb = new System.Text.StringBuilder(RootPathIndicator);
        for (int i = 0; i < segments.Count - 1; i++)
        {
            var s = segments[i];
            if (s.IsIndex)
                sb.Append($"[{s.Index}]");
            else
            {
                sb.Append(PathDelimiter);
                sb.Append(s.Key);
            }
        }
        return (sb.ToString(), leafStr);
    }

    // ── Indirect resolution ───────────────────────────────────────────────────

    public string? ProcessIndirectPath(string path, YamlNode data)
        => path.Contains("=indirect(") ? null : path;

    public IEnumerable<string> GetIntellisense(string partialPath, YamlNode data)
        => Enumerable.Empty<string>();

    // ── Internal traversal ────────────────────────────────────────────────────

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

    // ── Path parsing ──────────────────────────────────────────────────────────

    private List<PathSegment> ParseSegments(string path)
    {
        // Strip leading "$" or "$."
        var s = path;
        if (s.StartsWith(RootPathIndicator + PathDelimiter))
            s = s.Substring(RootPathIndicator.Length + PathDelimiter.Length);
        else if (s.StartsWith(RootPathIndicator))
            s = s.Substring(RootPathIndicator.Length);

        if (string.IsNullOrEmpty(s))
            return new List<PathSegment>();

        var segments = new List<PathSegment>();
        var parts = s.Split('.');

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            // Check for bracket notation: name[0] or name[*]
            var bracketIdx = part.IndexOf('[');
            if (bracketIdx >= 0)
            {
                var name = part.Substring(0, bracketIdx);
                if (!string.IsNullOrEmpty(name))
                    segments.Add(new PathSegment(name));

                var bracket = part.Substring(bracketIdx + 1).TrimEnd(']');
                if (bracket == "*")
                    segments.Add(PathSegment.Wildcard());
                else if (int.TryParse(bracket, out var idx))
                    segments.Add(PathSegment.ArrayIndex(idx));
            }
            else
            {
                segments.Add(new PathSegment(part));
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

        public PathSegment(string key)
        {
            Key        = key;
            Index      = 0;
            IsIndex    = false;
            IsWildcard = false;
        }

        private PathSegment(int index, bool wildcard)
        {
            Key        = null;
            Index      = index;
            IsIndex    = !wildcard;
            IsWildcard = wildcard;
        }

        public static PathSegment ArrayIndex(int idx) => new(idx, false);
        public static PathSegment Wildcard() => new(0, true);
    }
}
