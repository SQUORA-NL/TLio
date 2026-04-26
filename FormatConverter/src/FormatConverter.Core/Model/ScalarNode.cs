namespace FormatConverter.Core.Model;

/// <summary>
/// Represents a typed leaf value.
/// Maps to: JSON string/number/bool/null, XML text node, YAML scalar, EDI element.
/// </summary>
public sealed class ScalarNode : IntermediateNode
{
    /// <summary>Declared type of the value.</summary>
    public ScalarType Type { get; init; }

    /// <summary>
    /// Canonical string form of the value.
    /// <see langword="null"/> only when <see cref="Type"/> is <see cref="ScalarType.Null"/>.
    /// </summary>
    public string? RawValue { get; init; }

    /// <summary>
    /// Creates a <see cref="ScalarNode"/> and validates the <paramref name="rawValue"/>/<paramref name="type"/> invariant.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="rawValue"/> is null for a non-null type.</exception>
    public ScalarNode(ScalarType type, string? rawValue)
    {
        if (type != ScalarType.Null && rawValue is null)
            throw new ArgumentException("RawValue may only be null when Type is ScalarType.Null.", nameof(rawValue));
        Type = type;
        RawValue = rawValue;
    }
}
