using System.Diagnostics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.Performance;

/// <summary>
/// decisionTable's ApplyResults used to walk the full, statically-declared Outputs list to find
/// the handful of keys a matched rule's Results actually set — cost proportional to the
/// *declared* output count, paid again for every target node. It is now an outputs-by-name
/// lookup driven by Results itself — cost proportional to the *matched* result count. These
/// tests build tables shaped like the generated SIVI AFD conversion scripts in
/// samples/TLio.Sample.AfdApi (tens of thousands of declared outputs, a handful set per rule) to
/// prove that shape stays fast regardless of how large the declared surface grows.
/// </summary>
[TestFixture]
public class DecisionTable_PerformanceTests
{
    // Conservative thresholds, sized for CI (shared/noisy runners can run several times slower
    // than a developer machine — e.g. this file's outputPathTemplate case measures ~230 ms
    // locally but has been observed at 900-1300 ms on GitHub Actions). Each is set well above the
    // worst CI timing seen, while staying far below what the old O(declared-outputs) behavior
    // these tests guard against would take (tens of seconds at this scale).
    private const int MaxElapsedMs = 500;
    private const int MaxElapsedMsAtScale = 3_000;
    private const int WarmupIterations = 3;

    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void SetUp() => _context = JsonExecutionContext.CreateDefault();

    private static DecisionTable<JToken> BuildTable(int declaredOutputs, int matchedResultsPerRule)
    {
        var outputs = new List<DecisionOutput>(declaredOutputs);
        for (var i = 0; i < declaredOutputs; i++)
            outputs.Add(new DecisionOutput { Name = $"field{i}" }); // Path left empty -> outputPathTemplate

        // The one rule every target node matches; its Results only ever set the FIRST few
        // declared outputs, regardless of how many thousands more are declared.
        var results = new Dictionary<string, IFunctionSupportedValue<JToken>>();
        for (var i = 0; i < matchedResultsPerRule; i++)
            results[$"field{i}"] = new FixedValue<JToken>(new JValue($"value{i}"));

        return new DecisionTable<JToken>
        {
            Path = "$.items[*]",
            Config = new DecisionTableConfig<JToken>
            {
                Inputs = new List<DecisionInput> { new() { Name = "code", Path = "@.code" } },
                Outputs = outputs,
                OutputPathTemplate = "@._new.{name}",
                Rules = new List<DecisionRule<JToken>>
                {
                    new()
                    {
                        Conditions = new Dictionary<string, JToken> { ["code"] = new JValue("=A") },
                        Results = results
                    }
                }
            }
        };
    }

    private static JObject BuildTargets(int count)
    {
        var items = new JArray();
        for (var i = 0; i < count; i++)
            items.Add(new JObject { ["code"] = "A" });
        return new JObject { ["items"] = items };
    }

    [Test]
    public void ManyDeclaredOutputs_FewMatchedResults_ScalesWithMatchedNotDeclared()
    {
        // Comparable order of magnitude to afd1-to-afdshort.tlio.json's real mapping table:
        // 39,400 declared outputs, a few dozen set per matching rule. Old behavior walked all
        // 50,000 declared outputs per target node; new behavior only touches the 5 matched keys.
        const int declaredOutputs = 50_000;
        const int matchedResultsPerRule = 5;
        const int targetNodes = 300;

        var table = BuildTable(declaredOutputs, matchedResultsPerRule);

        // Warmup: builds and caches the outputs-by-name index once (shared across executions,
        // same as a compiled script reusing one DecisionTable instance across requests).
        for (var w = 0; w < WarmupIterations; w++)
            table.Execute(BuildTargets(10), _context);

        var data = BuildTargets(targetNodes);
        var sw = Stopwatch.StartNew();
        var result = table.Execute(data, _context);
        sw.Stop();

        TestContext.WriteLine(
            $"{declaredOutputs:N0} declared outputs, {matchedResultsPerRule} matched/rule, " +
            $"{targetNodes} target nodes: {sw.ElapsedMilliseconds} ms (threshold: {MaxElapsedMs} ms)");

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.items[0]._new.field0")?.Value<string>(), Is.EqualTo("value0"));
        Assert.That(data.SelectToken("$.items[0]._new.field4")?.Value<string>(), Is.EqualTo("value4"));
        Assert.That(data.SelectToken($"$.items[0]._new.field{matchedResultsPerRule}"), Is.Null,
            "only the matched keys should be written, never the other 49,995 declared-but-unmatched outputs");
        Assert.That(sw.ElapsedMilliseconds, Is.LessThanOrEqualTo(MaxElapsedMs),
            $"{targetNodes} target nodes against a {declaredOutputs:N0}-output table took " +
            $"{sw.ElapsedMilliseconds} ms, exceeding the {MaxElapsedMs} ms threshold — " +
            "ApplyResults may be walking Outputs again instead of Results.");
    }

    [Test]
    public void OutputPathTemplate_AtScale_WritesEveryMatchedValueToTheTemplatedPath()
    {
        // Every declared output relies entirely on outputPathTemplate (no output declares its
        // own path) — the shape all three AFD scripts use after 2fb0385. Matches close to
        // afd1-to-afd2.tlio.json's real table (10,439 outputs, 375 rules -> here, one rule
        // standing in for "the one rule that matches this node's code").
        const int declaredOutputs = 10_439;
        const int matchedResultsPerRule = 62; // afd1-to-afd2's XA (address) rule sets 62 fields
        const int targetNodes = 500;

        var table = BuildTable(declaredOutputs, matchedResultsPerRule);
        for (var w = 0; w < WarmupIterations; w++)
            table.Execute(BuildTargets(10), _context);

        var data = BuildTargets(targetNodes);
        var sw = Stopwatch.StartNew();
        var result = table.Execute(data, _context);
        sw.Stop();

        TestContext.WriteLine(
            $"{declaredOutputs:N0} declared outputs (all outputPathTemplate), " +
            $"{matchedResultsPerRule} matched/rule, {targetNodes} target nodes: " +
            $"{sw.ElapsedMilliseconds} ms (threshold: {MaxElapsedMsAtScale} ms)");

        Assert.That(result.Success, Is.True);
        for (var i = 0; i < matchedResultsPerRule; i++)
            Assert.That(data.SelectToken($"$.items[{targetNodes - 1}]._new.field{i}")?.Value<string>(),
                Is.EqualTo($"value{i}"), $"field{i} on the last target node");
        Assert.That(sw.ElapsedMilliseconds, Is.LessThanOrEqualTo(MaxElapsedMsAtScale),
            $"{targetNodes} target nodes took {sw.ElapsedMilliseconds} ms, exceeding the " +
            $"{MaxElapsedMsAtScale} ms threshold.");
    }
}
