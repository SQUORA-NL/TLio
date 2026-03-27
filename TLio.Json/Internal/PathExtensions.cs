namespace TLio.Json.Internal;

internal static class PathExtensions
{
    /// <summary>
    /// Joins a sequence of <see cref="PathElement"/> segments back into a dot-delimited path string.
    /// Ported from JLio.Core.Extensions.PathExtensions.ToPathString().
    /// </summary>
    public static string ToPathString(this IEnumerable<PathElement> elements)
    {
        return string.Join(".", elements.Select(e => e.PathElementFullText)).TrimEnd('.');
    }
}
