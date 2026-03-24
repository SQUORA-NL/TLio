namespace TLio.Json.Internal;

/// <summary>
/// Splits a JsonPath expression into its constituent <see cref="PathElement"/> segments,
/// respecting bracket nesting so that array subscripts like [?(@ > 0)] are kept intact.
/// Ported from JLio.Core.Extensions.JsonSplittedPath — Newtonsoft-specific path utility.
/// </summary>
internal class JsonSplittedPath
{
    private const char PathDelimiter = '.';
    public readonly List<PathElement> Elements = new();

    public JsonSplittedPath(string path)
    {
        var element = string.Empty;
        var arrayNotationLevel = 0;

        foreach (var c in path)
        {
            if (c == PathDelimiter && arrayNotationLevel == 0)
            {
                Elements.Add(new PathElement(element));
                element = string.Empty;
            }
            else
            {
                element = $"{element}{c}";
            }

            if (c == '[') arrayNotationLevel++;
            if (c == ']') arrayNotationLevel--;
        }

        if (element.Any()) Elements.Add(new PathElement(element));
    }

    public JsonSplittedPath(IEnumerable<PathElement> elements)
    {
        Elements.AddRange(elements);
    }

    // ── Derived views ─────────────────────────────────────────────────────────

    public PathElement LastElement => Elements.Last();
    public string LastName => LastElement.PathElementFullText;

    /// <summary>All elements except the last one.</summary>
    public IEnumerable<PathElement> ParentElements =>
        Elements.Any() ? Elements.Take(Elements.Count - 1) : Enumerable.Empty<PathElement>();

    /// <summary>
    /// The portion of the path up to and including the last "stable anchor" element —
    /// i.e. the last array-index or recursive-descent operator.
    /// Used to first select the existing nodes before walking construction segments.
    /// </summary>
    public IEnumerable<PathElement> SelectionPath => Elements.Take(GetSelectionPathIndex() + 1);

    /// <summary>The segments beyond the selection anchor — the ones that need to be created.</summary>
    public IEnumerable<PathElement> ConstructionPath => Elements.Skip(GetSelectionPathIndex() + 1);

    public bool HasArrayIndication => Elements.Any(e => e.HasArrayIndicator);

    public bool IsSearchingForObjectsByName =>
        Elements.Count > 1 && Elements[Elements.Count - 2].RecursiveDescentIndicator;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the index of the last "stable" element (array subscript or recursive descent).
    /// Elements after this index must be created if they don't exist.
    /// </summary>
    private int GetSelectionPathIndex()
    {
        for (var i = Elements.Count - 1; i > 0; i--)
        {
            if (Elements[i].RecursiveDescentIndicator) return i + 1;
            if (Elements[i].HasArrayIndicator) return i;
        }
        return 0;
    }

    /// <summary>
    /// Returns the index of the first element that differs between this path and
    /// <paramref name="secondPath"/>. Used for array-index alignment in CopyMove.
    /// </summary>
    public int GetSameElementsIndex(JsonSplittedPath secondPath)
    {
        var result = 0;
        while (result < Elements.Count && result < secondPath.Elements.Count)
        {
            if (string.Compare(Elements[result].PathElementFullText,
                    secondPath.Elements[result].PathElementFullText,
                    StringComparison.InvariantCulture) != 0)
                return result;
            result++;
        }
        return result;
    }
}
