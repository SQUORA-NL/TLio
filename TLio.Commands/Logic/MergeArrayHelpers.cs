using TLio.Core.Contracts;

namespace TLio.Commands.Logic;

/// <summary>
/// Key-path matching helpers used by the <c>merge</c> command.
///
/// Ported from JLio's ArrayHelpers, but every node access goes through
/// <see cref="INodeAdapter{TNode}"/> so the logic works unchanged for JSON
/// (Newtonsoft and System.Text.Json), XML and YAML.
/// </summary>
internal static class MergeArrayHelpers
{
    /// <summary>
    /// Return every element of <paramref name="targetArray"/> whose key values all
    /// equal the corresponding key values of <paramref name="sourceItem"/>.
    /// </summary>
    internal static List<TNode> FindMatchingElements<TNode>(
        INodeAdapter<TNode> adapter,
        IItemsFetcher<TNode> fetcher,
        IEnumerable<TNode> targetArray,
        TNode sourceItem,
        List<string> keyPaths)
        => targetArray.Where(t => AllKeysMatch(adapter, fetcher, t, sourceItem, keyPaths)).ToList();

    /// <summary>
    /// Return the indexes of every element of <paramref name="targetArray"/> that
    /// matches <paramref name="sourceItem"/> on all key paths. Indexes (rather than
    /// node references) keep replacement possible for formats whose nodes cannot be
    /// located from the node itself (YAML sequence items).
    /// </summary>
    internal static List<int> FindMatchingElementIndexes<TNode>(
        INodeAdapter<TNode> adapter,
        IItemsFetcher<TNode> fetcher,
        IReadOnlyList<TNode> targetArray,
        TNode sourceItem,
        List<string> keyPaths)
    {
        var result = new List<int>();
        for (var i = 0; i < targetArray.Count; i++)
            if (AllKeysMatch(adapter, fetcher, targetArray[i], sourceItem, keyPaths))
                result.Add(i);
        return result;
    }

    /// <summary>True when all <paramref name="keyPaths"/> resolve to equal values.</summary>
    internal static bool AllKeysMatch<TNode>(
        INodeAdapter<TNode> adapter,
        IItemsFetcher<TNode> fetcher,
        TNode target,
        TNode source,
        List<string> keyPaths)
        => keyPaths.All(k => KeyValueEquals(adapter, fetcher, target, source, k));

    /// <summary>True when <paramref name="item"/> is deep-equal to any array element.</summary>
    internal static bool IsItemInArray<TNode>(
        INodeAdapter<TNode> adapter, IEnumerable<TNode> array, TNode item)
        => array.Any(t => NodeEquals(adapter, t, item));

    // ── Internals ─────────────────────────────────────────────────────────────

    private static bool KeyValueEquals<TNode>(
        INodeAdapter<TNode> adapter, IItemsFetcher<TNode> fetcher, TNode a, TNode b, string keyPath)
    {
        var path = NormalizeKeyPath(keyPath, fetcher);

        var found = TryNavigate(adapter, fetcher, a, path, out var aVal);
        var otherFound = TryNavigate(adapter, fetcher, b, path, out var bVal);

        // Mirrors JLio: a key that is missing on both sides counts as equal,
        // a key present on only one side never matches.
        if (!found && !otherFound) return true;
        if (!found || !otherFound) return false;
        return NodeEquals(adapter, aVal!, bVal!);
    }

    /// <summary>
    /// Reduce a key path to the plain sequence of field names inside the item, so it can be
    /// walked from the element itself.
    ///
    /// The markers come from the fetcher. This used to strip "@", then "$", then ".", then "/"
    /// in turn — support for three path languages wired in side by side, which meant a key path
    /// in a fourth was mangled rather than refused, and a field whose name legitimately began
    /// with one of those characters lost it.
    /// </summary>
    internal static string NormalizeKeyPath<TNode>(string keyPath, IItemsFetcher<TNode> fetcher)
    {
        var path = (keyPath ?? string.Empty).Trim();

        path = StripPrefix(path, fetcher.CurrentItemPathIndicator);
        path = StripPrefix(path, fetcher.RootPathIndicator);
        path = StripPrefix(path, fetcher.PathDelimiter);

        return path;
    }

    private static string StripPrefix(string value, string prefix) =>
        prefix.Length > 0 && value.StartsWith(prefix, StringComparison.Ordinal)
            ? value[prefix.Length..]
            : value;

    /// <summary>
    /// Walk a field path from <paramref name="node"/>, splitting on the language's own
    /// delimiter. Returns false when any segment is missing.
    /// </summary>
    private static bool TryNavigate<TNode>(
        INodeAdapter<TNode> adapter, IItemsFetcher<TNode> fetcher, TNode node, string path,
        out TNode? value)
    {
        value = node;
        if (string.IsNullOrEmpty(path))
            return true;

        var delimiter = fetcher.PathDelimiter;
        var segments = delimiter.Length > 0
            ? path.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
            : new[] { path };

        var current = node;
        foreach (var segment in segments)
        {
            if (current == null || !adapter.HasProperty(current, segment))
            {
                value = default;
                return false;
            }
            current = adapter.GetProperty(current, segment);
        }

        value = current;
        return true;
    }

    private static bool NodeEquals<TNode>(INodeAdapter<TNode> adapter, TNode a, TNode b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return adapter.DeepEquals(a, b);
    }
}
