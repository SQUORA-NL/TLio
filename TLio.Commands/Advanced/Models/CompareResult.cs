namespace TLio.Commands.Advanced.Models;

/// <summary>
/// A single difference entry produced by the Compare command.
/// Written as an element of the result array at ResultPath.
///
/// The command is format-agnostic: FirstPath / SecondPath are produced by the
/// active IItemsFetcher, so they use JsonPath ("$.a.b"), slash-paths ("/a/b") or
/// YamlPath depending on the adapter in play.
/// </summary>
public class CompareResult
{
    /// <summary>True when this entry records an actual difference.</summary>
    public bool FoundDifference { get; set; }

    public DifferenceType DifferenceType { get; set; } = DifferenceType.NotSet;

    public DifferenceSubType DifferenceSubType { get; set; } = DifferenceSubType.NotSet;

    /// <summary>Path of the compared node on the first (source) side.</summary>
    public string? FirstPath { get; set; }

    /// <summary>Path of the compared node on the second (target) side.</summary>
    public string? SecondPath { get; set; }

    /// <summary>Human readable explanation of the entry.</summary>
    public string Description { get; set; } = string.Empty;

    // ── Factory helpers ──────────────────────────────────────────────────────
    // Kept static so the Compare command reads as a description of the diff
    // rather than of object construction.

    public static CompareResult Equal(string firstPath, string secondPath, string description) => new()
    {
        FoundDifference   = false,
        DifferenceType    = DifferenceType.NoDifference,
        DifferenceSubType = DifferenceSubType.Equals,
        FirstPath         = firstPath,
        SecondPath        = secondPath,
        Description       = description,
    };

    public static CompareResult ValueDifference(
        string firstPath, string secondPath, DifferenceSubType subType, string description) => new()
    {
        FoundDifference   = true,
        DifferenceType    = DifferenceType.ValueDifference,
        DifferenceSubType = subType,
        FirstPath         = firstPath,
        SecondPath        = secondPath,
        Description       = description,
    };

    public static CompareResult TypeDifference(string firstPath, string secondPath, string description) => new()
    {
        FoundDifference   = true,
        DifferenceType    = DifferenceType.TypeDifference,
        DifferenceSubType = DifferenceSubType.NotEquals,
        FirstPath         = firstPath,
        SecondPath        = secondPath,
        Description       = description,
    };

    public static CompareResult StructureDifference(string firstPath, string secondPath, string description) => new()
    {
        FoundDifference   = true,
        DifferenceType    = DifferenceType.StructureDifference,
        DifferenceSubType = DifferenceSubType.NotEquals,
        FirstPath         = firstPath,
        SecondPath        = secondPath,
        Description       = description,
    };

    public static CompareResult ArrayDifference(
        string firstPath, string secondPath, DifferenceSubType subType, bool foundDifference, string description) => new()
    {
        FoundDifference   = foundDifference,
        DifferenceType    = DifferenceType.ArrayDifference,
        DifferenceSubType = subType,
        FirstPath         = firstPath,
        SecondPath        = secondPath,
        Description       = description,
    };
}
