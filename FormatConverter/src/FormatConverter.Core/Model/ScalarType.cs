namespace FormatConverter.Core.Model;

/// <summary>Declared type hint for a <see cref="ScalarNode"/> leaf value.</summary>
public enum ScalarType
{
    /// <summary>UTF-8 text string.</summary>
    String,

    /// <summary>Whole number (maps to JSON integer, XML/YAML untyped numeric without decimal point).</summary>
    Integer,

    /// <summary>Floating-point number (maps to JSON number with decimal, YAML float).</summary>
    Decimal,

    /// <summary>Boolean true or false.</summary>
    Boolean,

    /// <summary>Explicit null / absence of value. <see cref="ScalarNode.RawValue"/> is <see langword="null"/> for this type.</summary>
    Null,
}
