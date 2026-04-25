using FormatConverter.Core;
using FormatConverter.Core.Model;

namespace FormatConverter.Tests.Stubs;

/// <summary>
/// Minimal proof-of-concept adapter: ToIM wraps the raw string in a ScalarNode;
/// FromIM returns the scalar's RawValue. Accepts but ignores all settings.
/// </summary>
public sealed class EchoFormatAdapter : IFormatAdapter
{
    /// <inheritdoc/>
    public string FormatId => "echo";

    /// <inheritdoc/>
    public IntermediateNode ToIM(string source, ConversionSettings settings) =>
        new ScalarNode(ScalarType.String, source);

    /// <inheritdoc/>
    public string FromIM(IntermediateNode root, ConversionSettings settings) =>
        root is ScalarNode s ? s.RawValue ?? string.Empty : root.ToString() ?? string.Empty;
}
