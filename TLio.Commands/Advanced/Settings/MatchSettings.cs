using System.Text.Json.Serialization;

namespace TLio.Commands.Advanced.Settings;

/// <summary>
/// Root-level object matching: objects are only merged when all specified key
/// fields hold equal values on both source and target.
///
/// Ported from JLio's MatchSettings.
/// </summary>
public class MatchSettings
{
    /// <summary>
    /// Dot-notation field paths that must be equal on source and target before
    /// the objects are merged. Supports plain ("id") and @ notation ("@.id").
    /// </summary>
    public List<string> KeyPaths { get; set; } = new();

    /// <summary>True when at least one key path is configured.</summary>
    [JsonIgnore]
    public bool HasKeys => KeyPaths.Count > 0;
}
