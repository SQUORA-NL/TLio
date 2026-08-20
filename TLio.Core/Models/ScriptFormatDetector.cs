namespace TLio.Core.Models;

/// <summary>
/// Works out which notation a script is written in by looking at its first meaningful
/// character. The three notations are distinguishable there and nowhere cheaper:
/// XML opens with a tag or a prolog, a JSON script opens with the array bracket, and
/// everything else is YAML — which is the right fallback, since YAML is the only one of
/// the three whose scripts can legitimately begin with a bare word, a dash or a comment.
///
/// JSON is a subset of YAML, so a JSON script would also parse as YAML. It is claimed for
/// JSON anyway: the JSON parser is the one that understands every command property, and
/// routing there keeps a JSON script behaving identically whether or not a YAML parser is
/// registered.
/// </summary>
public static class ScriptFormatDetector
{
    /// <summary>
    /// The notation <paramref name="scriptText"/> appears to be written in.
    /// Empty or whitespace-only text is reported as <see cref="ScriptFormat.Json"/> — every
    /// parser returns an empty script for it, so the choice does not change the outcome.
    /// </summary>
    public static ScriptFormat Detect(string? scriptText)
    {
        foreach (var c in SkipLeadingNoise(scriptText))
        {
            return c switch
            {
                '<' => ScriptFormat.Xml,
                '[' or '{' => ScriptFormat.Json,
                _ => ScriptFormat.Yaml
            };
        }

        return ScriptFormat.Json;
    }

    /// <summary>
    /// The notation a file extension names, or null when the extension says nothing.
    /// Accepts the extension with or without its leading dot.
    /// </summary>
    public static ScriptFormat? FromFileExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return null;
        return extension.TrimStart('.').ToLowerInvariant() switch
        {
            "json" => ScriptFormat.Json,
            "xml" => ScriptFormat.Xml,
            "yaml" or "yml" => ScriptFormat.Yaml,
            _ => null
        };
    }

    /// <summary>
    /// The notation <paramref name="name"/> spells, for the format parameters that reach the
    /// library as text (an MCP argument, a query string, a CLI flag). Case-insensitive, and
    /// "yml" is accepted alongside "yaml".
    /// </summary>
    public static bool TryParse(string? name, out ScriptFormat format)
    {
        format = ScriptFormat.Json;
        if (string.IsNullOrWhiteSpace(name)) return false;

        switch (name.Trim().ToLowerInvariant())
        {
            case "json": format = ScriptFormat.Json; return true;
            case "xml": format = ScriptFormat.Xml; return true;
            case "yaml":
            case "yml": format = ScriptFormat.Yaml; return true;
            default: return false;
        }
    }

    /// <summary>
    /// The text from its first character that could start a script, skipping whitespace, a
    /// byte-order mark, and the two things that can precede the opening bracket or tag:
    /// a YAML comment line and a YAML document marker.
    /// </summary>
    private static IEnumerable<char> SkipLeadingNoise(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];

            if (char.IsWhiteSpace(c) || c == '﻿') { i++; continue; }

            // A comment is YAML's alone, but it may sit above a flow sequence that is really
            // JSON, so skip the line rather than deciding on the '#'.
            if (c == '#')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }

            // "---" opens a YAML document and can be followed by a flow sequence. "----" and
            // longer are not a marker, so only the exact three-dash line is skipped.
            if (c == '-' && IsDocumentMarker(text, i))
            {
                i += 3;
                continue;
            }

            yield return c;
            yield break;
        }
    }

    private static bool IsDocumentMarker(string text, int i) =>
        i + 2 < text.Length &&
        text[i + 1] == '-' && text[i + 2] == '-' &&
        (i + 3 == text.Length || text[i + 3] is ' ' or '\t' or '\r' or '\n');
}
