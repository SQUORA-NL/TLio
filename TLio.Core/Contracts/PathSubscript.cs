using System.Globalization;

namespace TLio.Core.Contracts;

/// <summary>
/// A trailing <c>[n]</c> subscript on a path.
///
/// Every path language TLio speaks spells a position the same way — <c>$.items[1]</c>,
/// <c>/order/items/item[2]</c> — even though they disagree on where counting starts. This is
/// the spelling; where the counting starts is each fetcher's own business.
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
    /// <param name="closeChar">The format's subscript terminator — <see cref="IItemsFetcher{TNode}.ArrayCloseChar"/>.</param>
    /// <param name="head">The path with the subscript removed.</param>
    /// <param name="index">The number inside the brackets, exactly as written.</param>
    public static bool TrySplit(string path, string closeChar, out string head, out int index)
    {
        head = string.Empty;
        index = -1;

        if (string.IsNullOrEmpty(path) || !path.EndsWith(closeChar, StringComparison.Ordinal))
            return false;

        var open = path.LastIndexOf('[');
        if (open < 0) return false;

        var subscript = path.AsSpan(open + 1, path.Length - open - closeChar.Length - 1);

        // NumberStyles.None: no sign and no padding — "[-1]" and "[ 1 ]" are not positions.
        if (subscript.Length == 0 ||
            !int.TryParse(subscript, NumberStyles.None, CultureInfo.InvariantCulture, out index))
            return false;

        head = path[..open];
        return true;
    }
}
