using TLio.Core.Models;

namespace TLio.Commands.Advanced;

/// <summary>
/// Fluent extension methods for building DecisionTable commands inside a TLioScript.
///
/// Ported from JLio's DecisionTableExtensions / builder API.
/// </summary>
public static class DecisionTableBuilders
{
    /// <summary>
    /// Append a DecisionTable command to the script and return the script for chaining.
    /// </summary>
    public static TLioScript<TNode> AddDecisionTable<TNode>(
        this TLioScript<TNode> script,
        string path,
        DecisionTableConfig<TNode> config)
    {
        script.Add(new TLio.Commands.DecisionTable<TNode>(path, config));
        return script;
    }

    /// <summary>
    /// Create a new DecisionTableConfig builder for constructing a config inline.
    /// </summary>
    public static DecisionTableConfigBuilder<TNode> BuildConfig<TNode>() =>
        new DecisionTableConfigBuilder<TNode>();
}

/// <summary>
/// Fluent builder for <see cref="DecisionTableConfig{TNode}"/>.
/// </summary>
public class DecisionTableConfigBuilder<TNode>
{
    private readonly DecisionTableConfig<TNode> _config = new();

    public DecisionTableConfigBuilder<TNode> WithInput(string name, string path)
    {
        _config.Inputs.Add(new DecisionInput { Name = name, Path = path });
        return this;
    }

    public DecisionTableConfigBuilder<TNode> WithOutput(string name, string path)
    {
        _config.Outputs.Add(new DecisionOutput { Name = name, Path = path });
        return this;
    }

    public DecisionTableConfigBuilder<TNode> WithRule(DecisionRule<TNode> rule)
    {
        _config.Rules.Add(rule);
        return this;
    }

    public DecisionTableConfigBuilder<TNode> WithStrategy(
        string mode = "firstMatch",
        string conflictResolution = "priority")
    {
        _config.Strategy = new DecisionTableExecutionStrategy
        {
            Mode = mode,
            ConflictResolution = conflictResolution
        };
        return this;
    }

    public DecisionTableConfigBuilder<TNode> WithDefaultResults(
        Dictionary<string, TLio.Core.Contracts.IFunctionSupportedValue<TNode>> defaults)
    {
        _config.DefaultResults = defaults;
        return this;
    }

    public DecisionTableConfig<TNode> Build() => _config;
}
