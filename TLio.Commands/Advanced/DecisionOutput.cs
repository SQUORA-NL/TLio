namespace TLio.Commands.Advanced;

/// <summary>
/// Declares one output variable that a DecisionTable rule can write to.
///
/// <c>Path</c> is resolved relative to the current target node when it starts
/// with "@" (e.g. "@.category"), or as an absolute JsonPath otherwise.
///
/// Ported from JLio's DecisionTableOutput.
/// </summary>
public class DecisionOutput
{
    /// <summary>Logical name used as the key in rule Results dictionaries.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path expression that identifies where to write the result value.
    /// Supports relative (@.prop) and absolute ($.abs.path) syntax.
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
