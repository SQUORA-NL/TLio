namespace TLio.Json.SystemText.Internal;

internal static class PathExtensions
{
    public static string ToPathString(this IEnumerable<PathElement> elements)
    {
        return string.Join(".", elements.Select(e => e.PathElementFullText)).TrimEnd('.');
    }
}
