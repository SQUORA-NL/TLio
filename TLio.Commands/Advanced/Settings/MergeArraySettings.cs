using System.Text.Json.Serialization;

namespace TLio.Commands.Advanced.Settings;

/// <summary>
/// Per-array merge configuration. Selected by matching <see cref="ArrayPath"/>
/// against the path of the <b>target</b> array being merged into.
///
/// Ported from JLio's MergeArraySettings — format-agnostic: the path is compared
/// with the path produced by the active IItemsFetcher, so "$.items" (JSON/YAML)
/// and "/items" (XML) both work. Comparison ignores the root indicator, so
/// "$.items", "items" and "/items" all select the same array.
/// </summary>
public class MergeArraySettings
{
    /// <summary>Path of the target array this setting applies to (e.g. "$.items").</summary>
    public string ArrayPath { get; set; } = string.Empty;

    /// <summary>
    /// Dot-notation field paths used to match source↔target array elements.
    /// Supports plain ("key.id") and @ notation ("@.key.id").
    /// When empty, <see cref="UniqueItemsWithoutKeys"/> controls deduplication.
    /// </summary>
    public List<string> KeyPaths { get; set; } = new();

    /// <summary>
    /// When true and <see cref="KeyPaths"/> is empty, source items that already
    /// exist (deep-equal) in the target array are not appended again.
    /// </summary>
    public bool UniqueItemsWithoutKeys { get; set; }

    /// <summary>True when this setting drives key-based element matching.</summary>
    [JsonIgnore]
    public bool HasKeys => KeyPaths.Count > 0;
}
