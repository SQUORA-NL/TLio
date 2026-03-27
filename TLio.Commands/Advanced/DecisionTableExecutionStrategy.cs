namespace TLio.Commands.Advanced;

/// <summary>
/// Controls how the DecisionTable selects and applies matching rules.
///
/// Ported from JLio's ExecutionStrategy / DecisionTableStrategy.
/// </summary>
public class DecisionTableExecutionStrategy
{
    /// <summary>
    /// Rule selection mode.
    /// <list type="bullet">
    ///   <item><term>firstMatch</term><description>Sort rules by Priority ascending; stop at the first rule whose conditions all pass.</description></item>
    ///   <item><term>bestMatch</term><description>Score every matching rule as (conditionsMatched × 100 + Priority); apply the rule with the highest score.</description></item>
    ///   <item><term>allMatches</term><description>Apply every rule whose conditions all pass; use ConflictResolution when multiple rules write to the same output.</description></item>
    /// </list>
    /// Defaults to "firstMatch".
    /// </summary>
    public string Mode { get; set; } = "firstMatch";

    /// <summary>
    /// How to resolve conflicts when allMatches produces multiple results for the same output.
    /// <list type="bullet">
    ///   <item><term>priority</term><description>Use the result from the rule with the lowest Priority number.</description></item>
    ///   <item><term>lastWins</term><description>Use the result from the last matching rule (highest index).</description></item>
    ///   <item><term>merge</term><description>For arrays: concatenate. For numbers: take the maximum. For others: overwrite (same as lastWins).</description></item>
    /// </list>
    /// Defaults to "priority".
    /// </summary>
    public string ConflictResolution { get; set; } = "priority";
}
