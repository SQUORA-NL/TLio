using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.DecisionTableTests.
/// Adaptations:
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///   - fluent JLioScript API → TLioScript&lt;JToken&gt; object list
///
/// Tests basic DecisionTable scenarios: single match, no match with defaults,
/// multiple candidates with firstMatch, path selection, and validation.
/// </summary>
[TestFixture]
public class DecisionTableTests
{
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
    }

    // ── Helper: single-item config ─────────────────────────────────────────────

    private static DecisionTableConfig<JToken> SimpleCategoryConfig()
    {
        // Inputs: "status" from @.status
        // Outputs: "category" to @.category
        // Rules:
        //   Priority 0: status = "active"  → category = "A"
        //   Priority 1: status = "pending" → category = "B"
        // DefaultResults: category = "unknown"
        return new DecisionTableConfig<JToken>
        {
            Inputs = new List<DecisionInput>
            {
                new() { Name = "status", Path = "@.status" }
            },
            Outputs = new List<DecisionOutput>
            {
                new() { Name = "category", Path = "@.category" }
            },
            Rules = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Priority = 0,
                    Conditions = new Dictionary<string, JToken> { ["status"] = new JValue("=active") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["category"] = new FixedValue<JToken>(new JValue("A")) }
                },
                new()
                {
                    Priority = 1,
                    Conditions = new Dictionary<string, JToken> { ["status"] = new JValue("=pending") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["category"] = new FixedValue<JToken>(new JValue("B")) }
                }
            },
            DefaultResults = new Dictionary<string, IFunctionSupportedValue<JToken>>
            {
                ["category"] = new FixedValue<JToken>(new JValue("unknown"))
            }
        };
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    [Test]
    public void MissingPath_ReturnsFailed()
    {
        var cmd = new DecisionTable<JToken>(null!, SimpleCategoryConfig());
        var data = JToken.Parse("{}");

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void MissingConfig_ReturnsFailed()
    {
        var cmd = new DecisionTable<JToken> { Path = "$.items[*]" };
        var data = JToken.Parse("{ \"items\": [] }");

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.False);
    }

    // ── No matching nodes ──────────────────────────────────────────────────────

    [Test]
    public void NoTargetNodes_ReturnsSuccessWithoutChanges()
    {
        var data = JToken.Parse("{}");
        var cmd = new DecisionTable<JToken>("$.items[*]", SimpleCategoryConfig());

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.items"), Is.Null);
    }

    // ── Single match ────────────────────────────────────────────────────────────

    [Test]
    public void SingleNode_RuleMatches_WritesOutput()
    {
        var data = JToken.Parse("{ \"status\": \"active\" }");
        var cmd = new DecisionTable<JToken>("$", SimpleCategoryConfig());

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.category")?.Value<string>(), Is.EqualTo("A"));
    }

    [Test]
    public void SingleNode_SecondRuleMatches_WritesOutput()
    {
        var data = JToken.Parse("{ \"status\": \"pending\" }");
        var cmd = new DecisionTable<JToken>("$", SimpleCategoryConfig());

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.category")?.Value<string>(), Is.EqualTo("B"));
    }

    // ── No match → default results ─────────────────────────────────────────────

    [Test]
    public void NoRuleMatches_DefaultResultsApplied()
    {
        var data = JToken.Parse("{ \"status\": \"closed\" }");
        var cmd = new DecisionTable<JToken>("$", SimpleCategoryConfig());

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.category")?.Value<string>(), Is.EqualTo("unknown"));
    }

    [Test]
    public void NoRuleMatches_NoDefaultResults_LeavesOutputAbsent()
    {
        var config = new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "x", Path = "@.x" } },
            Outputs = new List<DecisionOutput> { new() { Name = "out", Path = "@.out" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Conditions = new Dictionary<string, JToken> { ["x"] = new JValue("=1") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["out"] = new FixedValue<JToken>(new JValue("yes")) }
                }
            }
        };

        var data = JToken.Parse("{ \"x\": 99 }");
        var cmd = new DecisionTable<JToken>("$", config);

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.out"), Is.Null);
    }

    // ── FirstMatch with multiple rules ─────────────────────────────────────────

    [Test]
    public void FirstMatch_StopsAtFirstMatchingRule()
    {
        // Both rules could match ("active" satisfies "=active" AND "!=pending"),
        // but firstMatch stops at the first (Priority 0).
        var config = new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "s", Path = "@.s" } },
            Outputs = new List<DecisionOutput> { new() { Name = "r", Path = "@.r" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Priority   = 0,
                    Conditions = new Dictionary<string, JToken> { ["s"] = new JValue("=active") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["r"] = new FixedValue<JToken>(new JValue("first")) }
                },
                new()
                {
                    Priority   = 1,
                    Conditions = new Dictionary<string, JToken> { ["s"] = new JValue("!=pending") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["r"] = new FixedValue<JToken>(new JValue("second")) }
                }
            },
            Strategy = new DecisionTableExecutionStrategy { Mode = "firstMatch" }
        };

        var data = JToken.Parse("{ \"s\": \"active\" }");
        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.r")?.Value<string>(), Is.EqualTo("first"));
    }

    // ── Multi-node path ($.items[*]) ─────────────────────────────────────────

    [Test]
    public void MultipleTargetNodes_EachEvaluatedIndependently()
    {
        var data = JToken.Parse(@"
        {
            ""items"": [
                { ""status"": ""active""  },
                { ""status"": ""pending"" },
                { ""status"": ""closed""  }
            ]
        }");

        var cmd = new DecisionTable<JToken>("$.items[*]", SimpleCategoryConfig());
        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.items[0].category")?.Value<string>(), Is.EqualTo("A"));
        Assert.That(data.SelectToken("$.items[1].category")?.Value<string>(), Is.EqualTo("B"));
        Assert.That(data.SelectToken("$.items[2].category")?.Value<string>(), Is.EqualTo("unknown"));
    }

    // ── Absolute output path ───────────────────────────────────────────────────

    [Test]
    public void AbsoluteOutputPath_WritesToAbsoluteLocation()
    {
        var config = new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "flag", Path = "@.flag" } },
            Outputs = new List<DecisionOutput> { new() { Name = "result", Path = "$.result" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Conditions = new Dictionary<string, JToken> { ["flag"] = new JValue("=true") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["result"] = new FixedValue<JToken>(new JValue("yes")) }
                }
            }
        };

        var data = JToken.Parse("{ \"flag\": true }");
        var cmd = new DecisionTable<JToken>("$", config);

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("yes"));
    }

    // ── Script API ─────────────────────────────────────────────────────────────

    [Test]
    public void CanUseScriptApi()
    {
        var data = JToken.Parse("{ \"status\": \"active\" }");

        var script = new TLioScript<JToken>
        {
            new DecisionTable<JToken>("$", SimpleCategoryConfig())
        };

        var result = script.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.category")?.Value<string>(), Is.EqualTo("A"));
    }

    // ── Article X: logging assertions ────────────────────────────────────────

    [Test]
    public void DecisionTable_Success_LogsInfoEntry()
    {
        var data = JToken.Parse("{ \"status\": \"active\" }");
        var result = new DecisionTable<JToken>("$", SimpleCategoryConfig()).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }

    [Test]
    public void DecisionTable_FirstMatchStrategy_OnlyFirstRuleApplied()
    {
        // Both rules can match when strategy is firstMatch (default Priority ordering)
        var data = JToken.Parse("{ \"status\": \"active\" }");
        var result = new DecisionTable<JToken>("$", SimpleCategoryConfig()).Execute(data, context);

        Assert.That(result.Success, Is.True);
        // Only the first matching rule (priority 0 → "A") should be applied
        Assert.That(data.SelectToken("$.category")?.Value<string>(), Is.EqualTo("A"));
    }
}
