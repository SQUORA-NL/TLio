using TLio.Commands.Advanced.Models;

namespace TLio.Commands.Advanced.Settings;

/// <summary>
/// Controls how Compare performs the diff.
///
/// ArraySettings: per-array key matching (same pattern as the Merge array settings).
/// ResultTypes:   filter which DifferenceType values are written to ResultPath.
///                Empty list = write all results.
/// </summary>
public class CompareSettings
{
    public List<CompareArraySettings> ArraySettings { get; set; } = new();
    public List<DifferenceType> ResultTypes { get; set; } = new();

    public static CompareSettings CreateDefault() => new();

    /// <summary>
    /// True when nothing has been configured — used to decide whether the
    /// backwards-compatible scalar result is written for primitive comparisons.
    /// </summary>
    public bool IsDefault =>
        (ArraySettings == null || ArraySettings.Count == 0) &&
        (ResultTypes == null || ResultTypes.Count == 0);
}
