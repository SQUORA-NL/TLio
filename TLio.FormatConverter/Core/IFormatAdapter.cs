using TLio.FormatConverter.Core.Model;

namespace TLio.FormatConverter.Core;

/// <summary>
/// Contract for a bidirectional format adapter that converts between a native format string
/// and the Intermediate Model (IM).
/// </summary>
/// <remarks>
/// Implementations must:
/// <list type="bullet">
///   <item>Expose a unique, case-insensitive <see cref="FormatId"/>.</item>
///   <item>Throw <see cref="Exceptions.FormatParseException"/> on malformed input.</item>
///   <item>Silently ignore <see cref="ConversionSettings"/> properties not relevant to their format.</item>
/// </list>
/// </remarks>
public interface IFormatAdapter
{
    /// <summary>Unique, case-insensitive identifier for this format (e.g. <c>"json"</c>, <c>"xml"</c>).</summary>
    string FormatId { get; }

    /// <summary>
    /// Whether a document in this format must have exactly one root element that is part of the
    /// document rather than of the value inside it — <see langword="true"/> for XML,
    /// <see langword="false"/> for JSON and YAML, where a bare object or array is a document.
    /// <c>convertValue</c> with <c>valueOnly</c> uses it to add a root when a value is written to
    /// such a format, and to take it off again when one is read from it.
    /// </summary>
    bool HasRootElement => false;

    /// <summary>Parse a source string into an IM tree.</summary>
    /// <param name="source">The raw format string to parse.</param>
    /// <param name="settings">Per-boundary adapter options; never <see langword="null"/>.</param>
    /// <returns>Root <see cref="IntermediateNode"/> of the parsed tree.</returns>
    /// <exception cref="Exceptions.FormatParseException">When <paramref name="source"/> is malformed.</exception>
    IntermediateNode ToIM(string source, ConversionSettings settings);

    /// <summary>Serialise an IM tree into a format string.</summary>
    /// <param name="root">Root node of the IM tree.</param>
    /// <param name="settings">Per-boundary adapter options; never <see langword="null"/>.</param>
    /// <returns>The serialised format string.</returns>
    /// <exception cref="Exceptions.FormatParseException">When the IM contains constructs unrepresentable without a defined fallback.</exception>
    string FromIM(IntermediateNode root, ConversionSettings settings);
}
