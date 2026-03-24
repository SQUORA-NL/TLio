namespace TLio.Json.Internal;

/// <summary>
/// Represents one segment of a dot-split JsonPath expression.
/// Ported from JLio.Core.Models.Path.PathElement — Newtonsoft-specific path utility.
/// </summary>
internal class PathElement
{
    public PathElement(string pathElementFullText)
    {
        PathElementFullText = pathElementFullText;
    }

    public string PathElementFullText { get; }

    /// <summary>True when this element represents a ".." recursive-descent token.</summary>
    public bool RecursiveDescentIndicator => PathElementFullText == string.Empty;

    /// <summary>The name portion before any array brackets, e.g. "items" from "items[0]".</summary>
    public string ElementName => !string.IsNullOrEmpty(PathElementFullText)
        ? PathElementFullText.Contains('[')
            ? PathElementFullText.Substring(0, PathElementFullText.IndexOf('['))
            : PathElementFullText
        : PathElementFullText;

    /// <summary>True when this element contains array subscript notation.</summary>
    public bool HasArrayIndicator => GetHasArrayIndicator();

    /// <summary>The bracket portion, e.g. "[0]" or "[*]".</summary>
    public string ArrayNotation => GetArrayNotation();

    public string ArrayNotationInnerText => ArrayNotation.TrimStart('[').TrimEnd(']');

    private bool GetHasArrayIndicator()
    {
        var cleaned = GetCleanedElement();
        return cleaned.Contains('[') && cleaned.EndsWith("]");
    }

    private string GetArrayNotation()
    {
        if (!HasArrayIndicator) return string.Empty;
        var cleaned = GetCleanedElement();
        var start = cleaned.Substring(cleaned.IndexOf('['));
        return start.Substring(0, start.IndexOf(']') + 1);
    }

    private string GetCleanedElement() =>
        PathElementFullText.Replace("['", "").Replace("']", "");
}
