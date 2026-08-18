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
        IEnumerable<TNode> targetArray,
        TNode sourceItem,
        List<string> keyPaths)
        => targetArray.Where(t => AllKeysMatch(adapter, t, sourceItem, keyPaths)).ToList();

    /// <summary>
    /// Return the indexes of every element of <paramref name="targetArray"/> that
    /// matches <paramref name="sourceItem"/> on all key paths. Indexes (rather than
    /// node references) keep replacement possible for formats whose nodes cannot be
    /// located from the node itself (YAML sequence items).
    /// </summary>
    internal static List<int> FindMatchingElementIndexes<TNode>(
        INodeAdapter<TNode> adapter,
        IReadOnlyList<TNode> targetArray,
        TNode sourceItem,
        List<string> keyPaths)
    {
        var result = new List<int>();
        for (var i = 0; i < targetArray.Count; i++)
            if (AllKeysMatch(adapter, targetArray[i], sourceItem, keyPaths))
                result.Add(i);
        return result;
    }

    /// <summary>True when all <paramref name="keyPaths"/> resolve to equal values.</summary>
    internal static bool AllKeysMatch<TNode>(
        INodeAdapter<TNode> adapter,
        TNode target,
        TNode source,
        List<string> keyPaths)
        => keyPaths.All(k => KeyValueEquals(adapter, target, source, k));

    /// <summary>True when <paramref name="item"/> is deep-equal to any array element.</summary>
    internal static bool IsItemInArray<TNode>(
        INodeAdapter<TNode> adapter, IEnumerable<TNode> array, TNode item)
        => array.Any(t => NodeEquals(adapter, t, item));

    // ── Internals ─────────────────────────────────────────────────────────────

    private static bool KeyValueEquals<TNode>(
        INodeAdapter<TNode> adapter, TNode a, TNode b, string keyPath)
    {
        var path = NormalizeKeyPath(keyPath);

        var found = TryNavigateDotPath(adapter, a, path, out var aVal);
        var otherFound = TryNavigateDotPath(adapter, b, path, out var bVal);

        // Mirrors JLio: a key that is missing on both sides counts as equal,
        // a key present on only one side never matches.
        if (!found && !otherFound) return true;
        if (!found || !otherFound) return false;
        return NodeEquals(adapter, aVal!, bVal!);
    }

    /// <summary>
    /// Strip the current-item indicator so "@.key.id", "@key.id", ".key.id" and
    /// "key.id" all resolve identically. "$." (root indicator) is stripped too so
    /// key paths can be copied from full path expressions.
    /// </summary>
    internal static string NormalizeKeyPath(string keyPath)
    {
        var path = (keyPath ?? string.Empty).Trim();
        if (path.StartsWith("@", StringComparison.Ordinal)) path = path[1..];
        else if (path.StartsWith("$", StringComparison.Ordinal)) path = path[1..];
        if (path.StartsWith(".", StringComparison.Ordinal)) path = path[1..];
        if (path.StartsWith("/", StringComparison.Ordinal)) path = path[1..];
        return path;
    }

    /// <summary>
    /// Walk a dot-separated field path from <paramref name="node"/>.
    /// Returns false when any segment is missing.
    /// </summary>
    private static bool TryNavigateDotPath<TNode>(
        INodeAdapter<TNode> adapter, TNode node, string path, out TNode? value)
    {
        value = node;
        if (string.IsNullOrEmpty(path))
            return true;

        var current = node;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current == null || !adapter.IsObject(current))
            {
                value = default;
                return false;
            }

            if (!adapter.HasProperty(current, segment))
            {
                value = default;
                return false;
            }

            current = adapter.GetProperty(current, segment);
        }

        value = current;
        return true;
    }

    /// <summary>Null-tolerant deep equality (System.Text.Json exposes JSON null as C# null).</summary>
    private static bool NodeEquals<TNode>(INodeAdapter<TNode> adapter, TNode a, TNode b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return adapter.DeepEquals(a, b);
    }
}
