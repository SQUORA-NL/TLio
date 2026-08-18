using System.Text.Json.Serialization;

namespace TLio.Commands.Advanced.Settings;

/// <summary>
/// Full merge configuration for the <c>merge</c> command.
///
/// Ported from JLio's MergeSettings. Everything is optional — the default
/// (<see cref="CreateDefault"/>) reproduces the classic TLio merge behaviour.
/// </summary>
public class MergeSettings
{
    /// <summary>Structure and values are merged (default).</summary>
    public const string StrategyFullMerge = "fullMerge";

    /// <summary>Only missing properties are added; existing values are kept.</summary>
    public const string StrategyOnlyStructure = "onlyStructure";

    /// <summary>Only existing properties are updated; no new properties are added.</summary>
    public const string StrategyOnlyValues = "onlyValues";

    /// <summary>Per-array settings, selected by the path of the target array.</summary>
    public List<MergeArraySettings> ArraySettings { get; set; } = new();

    /// <summary>Root-level object key matching.</summary>
    public MatchSettings MatchSettings { get; set; } = new();

    /// <summary>
    /// One of <see cref="StrategyFullMerge"/>, <see cref="StrategyOnlyStructure"/>
    /// or <see cref="StrategyOnlyValues"/>. Unknown values behave as fullMerge.
    /// </summary>
    public string Strategy { get; set; } = StrategyFullMerge;

    public static MergeSettings CreateDefault() => new();

    /// <summary>True when existing values may not be overwritten (onlyStructure).</summary>
    [JsonIgnore]
    public bool IsOnlyStructure =>
        string.Equals(Strategy, StrategyOnlyStructure, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when no new properties may be added (onlyValues).</summary>
    [JsonIgnore]
    public bool IsOnlyValues =>
        string.Equals(Strategy, StrategyOnlyValues, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when no array-level or match-level settings are configured.</summary>
    [JsonIgnore]
    public bool IsDefault =>
        ArraySettings.Count == 0 && !MatchSettings.HasKeys && !IsOnlyStructure && !IsOnlyValues;
}
