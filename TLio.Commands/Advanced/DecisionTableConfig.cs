using TLio.Core.Contracts;

namespace TLio.Commands.Advanced;

/// <summary>
/// Full configuration for a DecisionTable command.
///
/// Declares the input variables, output variables, evaluation rules, optional
/// default outputs (applied when no rule matches), and the execution strategy.
///
/// Ported from JLio's DecisionTableConfig.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public class DecisionTableConfig<TNode>
{
    /// <summary>Input variable declarations.</summary>
    public List<DecisionInput> Inputs { get; set; } = new();

    /// <summary>Output variable declarations.</summary>
    public List<DecisionOutput> Outputs { get; set; } = new();

    /// <summary>Ordered rule list.</summary>
    public List<DecisionRule<TNode>> Rules { get; set; } = new();

    /// <summary>
    /// Values to write when no rule matches.
    /// Keyed by output name (same keys as <see cref="DecisionOutput.Name"/>).
    /// </summary>
    public Dictionary<string, IFunctionSupportedValue<TNode>>? DefaultResults { get; set; }

    /// <summary>
    /// Controls rule selection and conflict resolution.
    /// Defaults to firstMatch / priority when null.
    /// </summary>
    public DecisionTableExecutionStrategy? Strategy { get; set; }

    /// <summary>
    /// Optional fallback path for an <see cref="DecisionOutput"/> whose <see cref="DecisionOutput.Path"/>
    /// is left empty: "{name}" is substituted with that output's <see cref="DecisionOutput.Name"/>,
    /// e.g. "@._new.{name}". A table where every result lands at the same predictable place under its
    /// own name can then declare its Outputs as bare names, instead of repeating "@._new." once per
    /// entry — the declaration still names and orders every output; only the redundant path text goes.
    /// </summary>
    public string? OutputPathTemplate { get; set; }
}
