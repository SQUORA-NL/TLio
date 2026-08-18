namespace TLio.Commands.Advanced.Models;

/// <summary>
/// The kind of difference reported by a single <see cref="CompareResult"/>.
///
/// Ported from JLio's DifferenceType. <see cref="NoDifference"/> is used for
/// entries that record a match (FoundDifference = false) so that
/// CompareSettings.ResultTypes can filter equal results away.
/// </summary>
public enum DifferenceType
{
    /// <summary>No difference type assigned (default).</summary>
    NotSet,

    /// <summary>The two nodes are equivalent — informational entry only.</summary>
    NoDifference,

    /// <summary>Both nodes exist and are of the same kind, but hold different values.</summary>
    ValueDifference,

    /// <summary>A property exists on one side only.</summary>
    StructureDifference,

    /// <summary>An array-level difference: item count, membership or index.</summary>
    ArrayDifference,

    /// <summary>The nodes are of a different kind (object / array / primitive / null).</summary>
    TypeDifference,
}

/// <summary>
/// Refines a <see cref="DifferenceType"/> with the direction or nature of the difference.
/// </summary>
public enum DifferenceSubType
{
    /// <summary>No sub type assigned (default).</summary>
    NotSet,

    /// <summary>The values are equivalent.</summary>
    Equals,

    /// <summary>The values are not equivalent and cannot be ordered.</summary>
    NotEquals,

    /// <summary>The first value is numerically smaller than the second.</summary>
    LessThan,

    /// <summary>The first value is numerically greater than the second.</summary>
    GreaterThan,

    /// <summary>The same array item exists on both sides but at a different index.</summary>
    IndexDifference,
}
