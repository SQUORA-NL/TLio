namespace TLio.Commands.Advanced.Settings;

/// <summary>
/// Per-array settings for the Compare command.
///
/// ArrayPath is matched against the absolute path of the array being compared
/// (either side — first or second), as produced by the active IItemsFetcher.
/// KeyPaths are relative paths used to match array elements across the two sides;
/// "@.id", ".id" and "id" all resolve to the same element key.
/// UniqueIndexMatching: when true, an item matched at a different index is
/// additionally reported as an IndexDifference.
/// </summary>
public class CompareArraySettings
{
    public string ArrayPath { get; set; } = string.Empty;
    public List<string> KeyPaths { get; set; } = new();
    public bool UniqueIndexMatching { get; set; }
}
