using TLio.Core.Contracts;

namespace TLio.Commands.Advanced;

/// <summary>
/// A single rule in a DecisionTable.
///
/// <c>Conditions</c> maps input names (from <see cref="DecisionInput.Name"/>) to
/// condition nodes.  A condition node can be:
/// <list type="bullet">
///   <item>A string primitive such as "=active", ">=50", ">=10 &amp;&amp; &lt;=100", "!=null"</item>
///   <item>An array node for membership tests — the input value must be one of the elements</item>
///   <item>A boolean or numeric primitive for direct equality</item>
/// </list>
///
/// <c>Results</c> maps output names (from <see cref="DecisionOutput.Name"/>) to the
/// value providers that produce the value to write when this rule matches.
///
/// Ported from JLio's DecisionTableRule.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public class DecisionRule<TNode>
{
    /// <summary>
    /// Condition values keyed by input name.
    /// All conditions must pass for the rule to match.
    /// </summary>
    public Dictionary<string, TNode> Conditions { get; set; } = new();

    /// <summary>
    /// Result value providers keyed by output name.
    /// Applied to the target node when the rule matches.
    /// </summary>
    public Dictionary<string, IFunctionSupportedValue<TNode>> Results { get; set; } = new();

    /// <summary>
    /// Lower numbers indicate higher priority.
    /// Used by "firstMatch" (sort order) and "bestMatch" (tie-break) and "priority" conflict resolution.
    /// </summary>
    public int Priority { get; set; } = 0;
}
