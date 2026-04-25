using FormatConverter.Core.Model;

namespace FormatConverter.Core;

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
