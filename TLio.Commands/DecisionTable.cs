using TLio.Commands.Advanced;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Applies a decision table to each node matched by <see cref="Path"/>.
///
/// For each target node:
/// 1. Resolve all declared inputs by evaluating their path expressions.
/// 2. Evaluate each rule's conditions against the resolved input values.
/// 3. Select the matching rule(s) according to the execution strategy.
/// 4. Write result values to the declared output paths.
/// 5. If no rule matches, write DefaultResults (if any).
///
/// Condition syntax (applied to string condition values):
///   - Operators: =, !=, &gt;, &lt;, &gt;=, &lt;=
///   - AND (higher precedence): &amp;&amp;  — e.g. "&gt;=10 &amp;&amp; &lt;=100"
///   - OR  (lower  precedence): ||  — e.g. "=active || =pending"
///   - Array conditions: if the condition node is an array, the input value must be one of the elements.
///
/// Execution strategies: firstMatch (default), bestMatch, allMatches.
/// Conflict resolution (allMatches): priority (default), lastWins, merge.
///
/// Ported from JLio's DecisionTable command.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public class DecisionTable<TNode> : CommandBase<TNode>
{
    public override string CommandName => "decisionTable";

    /// <summary>JsonPath that selects the target node(s) to which the decision applies.</summary>
    public string? Path { get; set; }

    /// <summary>Full decision-table configuration.</summary>
    public DecisionTableConfig<TNode>? Config { get; set; }

    public DecisionTable() { }

    public DecisionTable(string path, DecisionTableConfig<TNode> config)
    {
        Path = path;
        Config = config;
    }

    // ── Execute ──────────────────────────────────────────────────────────────

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.Failure, 0,
                $"{CommandName}: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var targetNodes = context.ItemsFetcher.SelectNodes(Path!, dataContext);
        if (targetNodes.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no nodes matched path '{Path}'");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: path '{Path}' matched 0 nodes; decision table not applied."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var appliedDetails = new List<string>();
        foreach (var targetNode in targetNodes)
            ApplyDecision(targetNode, dataContext, context, appliedDetails);

        context.LogInfo(CoreConstants.CommandExecution,
            $"{CommandName}: processed {targetNodes.Count} node(s) at '{Path}'");
        var rulesSummary = appliedDetails.Count > 0
            ? $" {string.Join("; ", appliedDetails.Distinct())}."
            : "";
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path ?? "", TraceOutcome.Success, targetNodes.Count,
            $"{CommandName}: applied decision table to {targetNodes.Count} node(s) at '{Path}'.{rulesSummary}"));
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    // ── Decision application ─────────────────────────────────────────────────

    private void ApplyDecision(TNode targetNode, TNode dataContext, IExecutionContext<TNode> context, List<string> appliedDetails)
    {
        var cfg = Config!;
        var strategy = cfg.Strategy ?? new DecisionTableExecutionStrategy();

        var inputValues = ResolveInputValues(targetNode, dataContext, context);
        var matchingRules = EvaluateRules(cfg.Rules, inputValues, context);

        if (matchingRules.Count == 0)
        {
            if (cfg.DefaultResults != null)
            {
                appliedDetails.Add($"no rule matched → defaults applied ({string.Join(", ", cfg.DefaultResults.Keys)} set)");
                ApplyResults(cfg.DefaultResults, targetNode, dataContext, context);
            }
            else
            {
                appliedDetails.Add("no rule matched, no defaults");
            }
            return;
        }

        var resultsToApply = SelectResults(matchingRules, strategy, context);
        var outputKeys = string.Join(", ", resultsToApply.Keys);
        var matchDesc = strategy.Mode.ToLowerInvariant() switch
        {
            "allmatches" => $"{matchingRules.Count} rules matched",
            "bestmatch"  => $"best-match rule[priority={matchingRules.OrderByDescending(m => m.ConditionsMatched * 100 - m.Rule.Priority).First().Rule.Priority}]",
            _            => $"rule[priority={matchingRules.OrderBy(m => m.Rule.Priority).First().Rule.Priority}]"
        };
        appliedDetails.Add($"{matchDesc} → {outputKeys} set");
        ApplyResults(resultsToApply, targetNode, dataContext, context);
    }

    // ── Input resolution ─────────────────────────────────────────────────────

    private Dictionary<string, TNode?> ResolveInputValues(
        TNode targetNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var result = new Dictionary<string, TNode?>();
        foreach (var input in Config!.Inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Path))
            {
                result[input.Name] = default;
                continue;
            }

            var absolutePath = input.Path.StartsWith(context.ItemsFetcher.CurrentItemPathIndicator)
                ? context.ItemsFetcher.ResolveRelativePath(input.Path, targetNode, dataContext)
                : input.Path;

            result[input.Name] = context.ItemsFetcher.SelectNode(absolutePath, dataContext);
        }
        return result;
    }

    // ── Rule evaluation ──────────────────────────────────────────────────────

    private List<(DecisionRule<TNode> Rule, int ConditionsMatched)> EvaluateRules(
        List<DecisionRule<TNode>> rules,
        Dictionary<string, TNode?> inputValues,
        IExecutionContext<TNode> context)
    {
        var matching = new List<(DecisionRule<TNode>, int)>();
        foreach (var rule in rules)
        {
            var conditionsMatched = 0;
            var allPass = true;
            foreach (var (inputName, conditionNode) in rule.Conditions)
            {
                if (!inputValues.TryGetValue(inputName, out var inputValue) || inputValue == null)
                {
                    allPass = false;
                    break;
                }
                if (!EvaluateCondition(inputValue, conditionNode, context))
                {
                    allPass = false;
                    break;
                }
                conditionsMatched++;
            }
            if (allPass) matching.Add((rule, conditionsMatched));
        }
        return matching;
    }

    // ── Strategy selection ───────────────────────────────────────────────────

    private Dictionary<string, IFunctionSupportedValue<TNode>> SelectResults(
        List<(DecisionRule<TNode> Rule, int ConditionsMatched)> matchingRules,
        DecisionTableExecutionStrategy strategy,
        IExecutionContext<TNode> context)
    {
        switch (strategy.Mode.ToLowerInvariant())
        {
            case "firstmatch":
            {
                // Sort by Priority ascending (lower = higher priority), pick first
                var sorted = matchingRules.OrderBy(m => m.Rule.Priority).ToList();
                return new Dictionary<string, IFunctionSupportedValue<TNode>>(sorted[0].Rule.Results);
            }

            case "bestmatch":
            {
                // Score = conditionsMatched × 100 + (MaxPriority - priority) so higher score = better
                // (JLio: lower Priority number = higher priority; multiply conditionsMatched × 100 then subtract priority)
                var best = matchingRules
                    .OrderByDescending(m => m.ConditionsMatched * 100 - m.Rule.Priority)
                    .First();
                return new Dictionary<string, IFunctionSupportedValue<TNode>>(best.Rule.Results);
            }

            case "allmatches":
            {
                return MergeAllResults(matchingRules, strategy.ConflictResolution);
            }

            default:
                goto case "firstmatch";
        }
    }

    private Dictionary<string, IFunctionSupportedValue<TNode>> MergeAllResults(
        List<(DecisionRule<TNode> Rule, int ConditionsMatched)> matchingRules,
        string conflictResolution)
    {
        switch (conflictResolution.ToLowerInvariant())
        {
            case "priority":
            {
                // Lowest Priority number wins for each output key
                var result = new Dictionary<string, (IFunctionSupportedValue<TNode> Value, int Priority)>();
                foreach (var (rule, _) in matchingRules)
                    foreach (var (key, value) in rule.Results)
                        if (!result.TryGetValue(key, out var existing) || rule.Priority < existing.Priority)
                            result[key] = (value, rule.Priority);
                return result.ToDictionary(kv => kv.Key, kv => kv.Value.Value);
            }

            case "lastwins":
            {
                var result = new Dictionary<string, IFunctionSupportedValue<TNode>>();
                foreach (var (rule, _) in matchingRules)
                    foreach (var (key, value) in rule.Results)
                        result[key] = value;
                return result;
            }

            case "merge":
            {
                // For arrays: keep a list of all values to concat later (handled at write time via a special wrapper)
                // For this implementation we track all values per key and delegate merge logic to ApplyMergedResults
                var allValues = new Dictionary<string, List<IFunctionSupportedValue<TNode>>>();
                foreach (var (rule, _) in matchingRules)
                    foreach (var (key, value) in rule.Results)
                    {
                        if (!allValues.TryGetValue(key, out var list))
                            allValues[key] = list = new List<IFunctionSupportedValue<TNode>>();
                        list.Add(value);
                    }
                // Wrap each list in a MergeResultValue so ApplyResults can handle it
                return allValues.ToDictionary(
                    kv => kv.Key,
                    kv => (IFunctionSupportedValue<TNode>)new MergeResultValue<TNode>(kv.Value));
            }

            default:
                goto case "priority";
        }
    }

    // ── Result application ───────────────────────────────────────────────────

    private void ApplyResults(
        Dictionary<string, IFunctionSupportedValue<TNode>> results,
        TNode targetNode,
        TNode dataContext,
        IExecutionContext<TNode> context)
    {
        foreach (var output in Config!.Outputs)
        {
            if (!results.TryGetValue(output.Name, out var valueProvider))
                continue;

            var absolutePath = output.Path.StartsWith(context.ItemsFetcher.CurrentItemPathIndicator)
                ? context.ItemsFetcher.ResolveRelativePath(output.Path, targetNode, dataContext)
                : output.Path;

            var (parentPath, leafName) = context.ItemsFetcher.SplitParentAndLeaf(absolutePath);
            context.ItemsFetcher.EnsurePath(absolutePath, dataContext, context.NodeAdapter);
            var parents = context.ItemsFetcher.SelectNodes(parentPath, dataContext);

            foreach (var parent in parents)
            {
                var valueResult = valueProvider.GetValue(targetNode, dataContext, context);
                if (valueResult.Success && valueResult.Data.First != null)
                {
                    // MergeResultValue handles merge conflict resolution at write time
                    if (valueProvider is MergeResultValue<TNode> mergeValue)
                    {
                        var existing = context.NodeAdapter.GetProperty(parent, leafName);
                        var merged = mergeValue.Merge(existing, context);
                        context.NodeAdapter.SetProperty(parent, leafName, merged);
                    }
                    else
                    {
                        context.NodeAdapter.SetProperty(parent, leafName,
                            context.NodeAdapter.DeepClone(valueResult.Data.First!));
                    }
                }
            }
        }
    }

    // ── Condition evaluation ─────────────────────────────────────────────────

    private bool EvaluateCondition(TNode inputValue, TNode conditionNode, IExecutionContext<TNode> context)
    {
        // Array membership: input value must be one of the array elements
        if (context.NodeAdapter.IsArray(conditionNode))
        {
            var inputStr = context.NodeAdapter.TryGetString(inputValue);
            var inputNum = context.NodeAdapter.TryGetDouble(inputValue);
            return context.NodeAdapter.GetArrayElements(conditionNode).Any(el =>
            {
                if (inputNum.HasValue)
                {
                    var elNum = context.NodeAdapter.TryGetDouble(el);
                    if (elNum.HasValue && elNum.Value == inputNum.Value) return true;
                }
                return context.NodeAdapter.TryGetString(el) == inputStr;
            });
        }

        // String condition: parse operators
        var conditionStr = context.NodeAdapter.TryGetString(conditionNode);
        if (conditionStr == null) return false;

        // OR has lowest precedence — split first
        var orParts = conditionStr.Split(new[] { "||" }, StringSplitOptions.None);
        return orParts.Any(part => EvaluateAndExpression(inputValue, part.Trim(), context));
    }

    private bool EvaluateAndExpression(TNode inputValue, string expression, IExecutionContext<TNode> context)
    {
        // AND has higher precedence
        var andParts = expression.Split(new[] { "&&" }, StringSplitOptions.None);
        return andParts.All(part => EvaluateLeafCondition(inputValue, part.Trim(), context));
    }

    private bool EvaluateLeafCondition(TNode inputValue, string condition, IExecutionContext<TNode> context)
    {
        if (condition.StartsWith(">="))
            return CompareNumericOrString(inputValue, condition.Substring(2).Trim(), context,
                (a, b) => a >= b, (a, b) => string.Compare(a, b, StringComparison.Ordinal) >= 0);
        if (condition.StartsWith("<="))
            return CompareNumericOrString(inputValue, condition.Substring(2).Trim(), context,
                (a, b) => a <= b, (a, b) => string.Compare(a, b, StringComparison.Ordinal) <= 0);
        if (condition.StartsWith("!="))
            return !EvaluateEquals(inputValue, condition.Substring(2).Trim(), context);
        if (condition.StartsWith(">"))
            return CompareNumericOrString(inputValue, condition.Substring(1).Trim(), context,
                (a, b) => a > b, (a, b) => string.Compare(a, b, StringComparison.Ordinal) > 0);
        if (condition.StartsWith("<"))
            return CompareNumericOrString(inputValue, condition.Substring(1).Trim(), context,
                (a, b) => a < b, (a, b) => string.Compare(a, b, StringComparison.Ordinal) < 0);
        if (condition.StartsWith("="))
            return EvaluateEquals(inputValue, condition.Substring(1).Trim(), context);

        // No operator — treat as equality
        return EvaluateEquals(inputValue, condition, context);
    }

    private bool EvaluateEquals(TNode inputValue, string conditionValue, IExecutionContext<TNode> context)
    {
        // Numeric equality
        if (double.TryParse(conditionValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var condNum))
        {
            var inputNum = context.NodeAdapter.TryGetDouble(inputValue);
            if (inputNum.HasValue) return inputNum.Value == condNum;
        }

        // Boolean equality
        if (bool.TryParse(conditionValue, out var condBool))
        {
            var inputBool = context.NodeAdapter.TryGetBoolean(inputValue);
            if (inputBool.HasValue) return inputBool.Value == condBool;
        }

        // String equality (null check: "null" matches a null/missing node)
        if (conditionValue.Equals("null", StringComparison.OrdinalIgnoreCase))
            return context.NodeAdapter.IsNull(inputValue);

        return string.Equals(
            context.NodeAdapter.TryGetString(inputValue),
            conditionValue,
            StringComparison.Ordinal);
    }

    private bool CompareNumericOrString(
        TNode inputValue,
        string conditionValue,
        IExecutionContext<TNode> context,
        Func<double, double, bool> numericCompare,
        Func<string, string, bool> stringCompare)
    {
        if (double.TryParse(conditionValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var condNum))
        {
            var inputNum = context.NodeAdapter.TryGetDouble(inputValue);
            if (inputNum.HasValue) return numericCompare(inputNum.Value, condNum);
        }

        var inputStr = context.NodeAdapter.TryGetString(inputValue);
        if (inputStr != null) return stringCompare(inputStr, conditionValue);

        return false;
    }

    // ── Validation ───────────────────────────────────────────────────────────

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"{CommandName}: Path property is required.");
        if (Config == null)
            result.AddError($"{CommandName}: Config property is required.");
        return result;
    }
}

// ── Merge helper ──────────────────────────────────────────────────────────────

/// <summary>
/// Internal value provider that holds multiple candidate values for "merge" conflict resolution.
/// When written, it merges them: arrays are concatenated, numbers take the maximum, others are overwritten.
/// </summary>
internal sealed class MergeResultValue<TNode> : IFunctionSupportedValue<TNode>
{
    private readonly List<IFunctionSupportedValue<TNode>> _candidates;

    public MergeResultValue(List<IFunctionSupportedValue<TNode>> candidates)
        => _candidates = candidates;

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        // Return the last candidate's value as the "raw" result (actual merge happens in Merge())
        var last = _candidates.Last().GetValue(currentNode, dataContext, context);
        return last;
    }

    public TNode Merge(TNode? existing, IExecutionContext<TNode> context)
    {
        // Collect all candidate values
        var dummy = default(TNode)!;
        var values = _candidates
            .Select(c => c.GetValue(dummy, dummy, context))
            .Where(r => r.Success && r.Data.First != null)
            .Select(r => r.Data.First!)
            .ToList();

        if (values.Count == 0) return context.NodeAdapter.CreateNull();

        // All arrays → concat
        if (values.All(v => context.NodeAdapter.IsArray(v)))
        {
            var merged = existing != null && context.NodeAdapter.IsArray(existing)
                ? context.NodeAdapter.DeepClone(existing)
                : context.NodeAdapter.CreateArray();
            foreach (var arr in values)
                foreach (var el in context.NodeAdapter.GetArrayElements(arr))
                    context.NodeAdapter.AppendToArray(merged, context.NodeAdapter.DeepClone(el));
            return merged;
        }

        // All numbers → max
        var nums = values.Select(v => context.NodeAdapter.TryGetDouble(v)).ToList();
        if (nums.All(n => n.HasValue))
            return context.NodeAdapter.CreateNumber(nums.Max(n => n!.Value));

        // Default: last wins
        return context.NodeAdapter.DeepClone(values.Last());
    }

    public string ToScript() => "[mergeResult]";
}
