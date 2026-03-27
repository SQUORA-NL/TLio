namespace TLio.Commands.Advanced;

/// <summary>
/// Declares one input variable for a DecisionTable rule evaluation.
///
/// <c>Path</c> is resolved relative to the current target node when it starts
/// with "@" (e.g. "@.age"), or as an absolute JsonPath otherwise.
///
/// Ported from JLio's DecisionTableInput.
/// </summary>
public class DecisionInput
{
    /// <summary>Logical name used as the key in rule Conditions dictionaries.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path expression that selects the value to test.
    /// Supports relative (@.prop) and absolute ($.abs.path) syntax.
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
