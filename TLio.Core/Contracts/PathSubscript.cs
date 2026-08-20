using System.Globalization;

namespace TLio.Core.Contracts;

/// <summary>
/// A trailing <c>[n]</c> subscript on a path.
///
/// The subscript tokens are the fetcher's to declare — see
/// <see cref="IItemsFetcher{TNode}.ArrayOpenChar"/> and
/// <see cref="IItemsFetcher{TNode}.ArrayCloseChar"/>. This only reads a position out of the
/// text between them; where the counting starts is each fetcher's own business too.
/// </summary>
public static class PathSubscript
{
    /// <summary>
    /// Splits a trailing integer subscript off <paramref name="path"/>.
    ///
    /// Only an integer counts. A predicate (<c>item[@id='1']</c>), a wildcard
    /// (<c>items[*]</c>) and a bracket-quoted key (<c>$['a.b']</c>) all name something other
    /// than a position, and each format's selector already handles those itself.
    /// </summary>
    /// <param name="path">The path to inspect.</param>
    /// <param name="openChar">The language's subscript opener — <see cref="IItemsFetcher{TNode}.ArrayOpenChar"/>.</param>
    /// <param name="closeChar">The language's subscript terminator — <see cref="IItemsFetcher{TNode}.ArrayCloseChar"/>.</param>
    /// <param name="head">The path with the subscript removed.</param>
    /// <param name="index">The number inside the brackets, exactly as written.</param>
    public static bool TrySplit(string path, string openChar, string closeChar,
        out string head, out int index)
    {
        head = string.Empty;
        index = -1;

        if (string.IsNullOrEmpty(path) || !path.EndsWith(closeChar, StringComparison.Ordinal))
            return false;

        var open = path.LastIndexOf(openChar, StringComparison.Ordinal);
        if (open < 0) return false;

        var start = open + openChar.Length;
        var subscript = path.AsSpan(start, path.Length - start - closeChar.Length);

        // NumberStyles.None: no sign and no padding — "[-1]" and "[ 1 ]" are not positions.
        if (subscript.Length == 0 ||
            !int.TryParse(subscript, NumberStyles.None, CultureInfo.InvariantCulture, out index))
            return false;

        head = path[..open];
        return true;
    }
}
