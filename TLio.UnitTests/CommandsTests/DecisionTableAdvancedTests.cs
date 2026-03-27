using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.DecisionTableAdvancedTests.
/// Adaptations:
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// Covers advanced DecisionTable features:
///   - Compound conditions (&amp;&amp; / ||)
///   - Array membership conditions
///   - Numeric operators (&gt;=, &lt;=, &gt;, &lt;)
///   - bestMatch strategy with scoring
///   - allMatches strategy with conflict resolution (priority, lastWins, merge)
/// </summary>
[TestFixture]
public class DecisionTableAdvancedTests
{
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
    }

    // ── Numeric operators ────────────────────────────────────────────────────

    [Test]
    public void NumericGreaterThan_Matches()
    {
        var data = JToken.Parse("{ \"score\": 80 }");
        var config = NumericRangeConfig(">=75", "pass");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.grade")?.Value<string>(), Is.EqualTo("pass"));
    }

    [Test]
    public void NumericLessThan_Matches()
    {
        var data = JToken.Parse("{ \"score\": 40 }");
        var config = NumericRangeConfig("<50", "fail");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.grade")?.Value<string>(), Is.EqualTo("fail"));
    }

    [Test]
    public void NumericNotEquals_Matches()
    {
        var data = JToken.Parse("{ \"score\": 0 }");
        var config = NumericRangeConfig("!=100", "not-perfect");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.grade")?.Value<string>(), Is.EqualTo("not-perfect"));
    }

    // ── Compound AND conditions ──────────────────────────────────────────────

    [Test]
    public void AndCondition_BothPartsMustPass()
    {
        var data = JToken.Parse("{ \"score\": 75 }");
        var config = NumericRangeConfig(">=50 && <=100", "pass");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.grade")?.Value<string>(), Is.EqualTo("pass"));
    }

    [Test]
    public void AndCondition_OnlyFirstPartPasses_DoesNotMatch()
    {
        var data = JToken.Parse("{ \"score\": 150 }");
        var config = NumericRangeConfig(">=50 && <=100", "pass");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.grade"), Is.Null);
    }

    // ── Compound OR conditions ───────────────────────────────────────────────

    [Test]
    public void OrCondition_FirstAlternativeMatches()
    {
        var data = JToken.Parse("{ \"score\": 5 }");
        var config = NumericRangeConfig("<=10 || >=90", "extreme");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.grade")?.Value<string>(), Is.EqualTo("extreme"));
    }

    [Test]
    public void OrCondition_SecondAlternativeMatches()
    {
        var data = JToken.Parse("{ \"score\": 95 }");
        var config = NumericRangeConfig("<=10 || >=90", "extreme");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.grade")?.Value<string>(), Is.EqualTo("extreme"));
    }

    [Test]
    public void OrCondition_NeitherAlternativeMatches_NoOutput()
    {
        var data = JToken.Parse("{ \"score\": 50 }");
        var config = NumericRangeConfig("<=10 || >=90", "extreme");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.grade"), Is.Null);
    }

    // ── Array membership ─────────────────────────────────────────────────────

    [Test]
    public void ArrayMembership_ValueInList_Matches()
    {
        var data = JToken.Parse("{ \"status\": \"active\" }");
        var allowedStatuses = new JArray("active", "pending");

        var config = new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "status", Path = "@.status" } },
            Outputs = new List<DecisionOutput> { new() { Name = "eligible", Path = "@.eligible" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Conditions = new Dictionary<string, JToken> { ["status"] = allowedStatuses },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["eligible"] = new FixedValue<JToken>(new JValue(true)) }
                }
            }
        };

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.eligible")?.Value<bool>(), Is.True);
    }

    [Test]
    public void ArrayMembership_ValueNotInList_NoMatch()
    {
        var data = JToken.Parse("{ \"status\": \"closed\" }");
        var allowedStatuses = new JArray("active", "pending");

        var config = new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "status", Path = "@.status" } },
            Outputs = new List<DecisionOutput> { new() { Name = "eligible", Path = "@.eligible" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Conditions = new Dictionary<string, JToken> { ["status"] = allowedStatuses },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["eligible"] = new FixedValue<JToken>(new JValue(true)) }
                }
            }
        };

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.eligible"), Is.Null);
    }

    // ── bestMatch strategy ────────────────────────────────────────────────────

    [Test]
    public void BestMatch_RuleWithMoreConditionsMatchedWins()
    {
        // Rule A (Priority 0): score >= 0         → 1 condition matched
        // Rule B (Priority 1): score >= 0 && <= 100 → 2 conditions (but in one string they are AND-ed, so still 1 condition entry)
        // Use two separate inputs to get different condition counts
        var config = new DecisionTableConfig<JToken>
        {
            Inputs = new List<DecisionInput>
            {
                new() { Name = "score", Path = "@.score" },
                new() { Name = "type",  Path = "@.type"  }
            },
            Outputs = new List<DecisionOutput>
            {
                new() { Name = "label", Path = "@.label" }
            },
            Rules = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Priority   = 0,
                    Conditions = new Dictionary<string, JToken>
                    {
                        ["score"] = new JValue(">=0")
                    },
                    Results = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["label"] = new FixedValue<JToken>(new JValue("generic")) }
                },
                new()
                {
                    Priority   = 1,
                    Conditions = new Dictionary<string, JToken>
                    {
                        ["score"] = new JValue(">=0"),
                        ["type"]  = new JValue("=premium")
                    },
                    Results = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["label"] = new FixedValue<JToken>(new JValue("premium")) }
                }
            },
            Strategy = new DecisionTableExecutionStrategy { Mode = "bestMatch" }
        };

        var data = JToken.Parse("{ \"score\": 80, \"type\": \"premium\" }");
        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        // Rule B has 2 conditions matched vs Rule A with 1 → Rule B wins
        Assert.That(data.SelectToken("$.label")?.Value<string>(), Is.EqualTo("premium"));
    }

    [Test]
    public void BestMatch_TiedConditions_LowerPriorityNumberWins()
    {
        // Both rules match with 1 condition each; lower Priority number wins via scoring
        var config = new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "x", Path = "@.x" } },
            Outputs = new List<DecisionOutput> { new() { Name = "r", Path = "@.r" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Priority   = 5,
                    Conditions = new Dictionary<string, JToken> { ["x"] = new JValue("=yes") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["r"] = new FixedValue<JToken>(new JValue("low-prio")) }
                },
                new()
                {
                    Priority   = 1,
                    Conditions = new Dictionary<string, JToken> { ["x"] = new JValue("=yes") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["r"] = new FixedValue<JToken>(new JValue("high-prio")) }
                }
            },
            Strategy = new DecisionTableExecutionStrategy { Mode = "bestMatch" }
        };

        var data = JToken.Parse("{ \"x\": \"yes\" }");
        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        // Higher score = conditionsMatched(1)*100 - priority; priority 1 → score 99 beats priority 5 → score 95
        Assert.That(data.SelectToken("$.r")?.Value<string>(), Is.EqualTo("high-prio"));
    }

    // ── allMatches + conflict resolution ─────────────────────────────────────

    [Test]
    public void AllMatches_Priority_LowestPriorityNumberWins()
    {
        var config = AllMatchesConfig("priority");
        var data = JToken.Parse("{ \"v\": 50 }");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.out")?.Value<string>(), Is.EqualTo("rule-0")); // priority 0 wins
    }

    [Test]
    public void AllMatches_LastWins_LastRuleResultWritten()
    {
        var config = AllMatchesConfig("lastWins");
        var data = JToken.Parse("{ \"v\": 50 }");

        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.out")?.Value<string>(), Is.EqualTo("rule-1")); // last written
    }

    [Test]
    public void AllMatches_Merge_NumbersAreMaxed()
    {
        var config = new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "v", Path = "@.v" } },
            Outputs = new List<DecisionOutput> { new() { Name = "out", Path = "@.out" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Priority   = 0,
                    Conditions = new Dictionary<string, JToken> { ["v"] = new JValue(">=0") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["out"] = new FixedValue<JToken>(new JValue(10.0)) }
                },
                new()
                {
                    Priority   = 1,
                    Conditions = new Dictionary<string, JToken> { ["v"] = new JValue(">=0") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["out"] = new FixedValue<JToken>(new JValue(50.0)) }
                }
            },
            Strategy = new DecisionTableExecutionStrategy { Mode = "allMatches", ConflictResolution = "merge" }
        };

        var data = JToken.Parse("{ \"v\": 5 }");
        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        // merge of 10 and 50 → max = 50
        Assert.That(data.SelectToken("$.out")?.Value<double>(), Is.EqualTo(50.0));
    }

    // ── Multiple inputs ───────────────────────────────────────────────────────

    [Test]
    public void MultipleInputs_AllMustMatch()
    {
        var config = new DecisionTableConfig<JToken>
        {
            Inputs = new List<DecisionInput>
            {
                new() { Name = "age",    Path = "@.age"    },
                new() { Name = "status", Path = "@.status" }
            },
            Outputs = new List<DecisionOutput>
            {
                new() { Name = "eligible", Path = "@.eligible" }
            },
            Rules = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Conditions = new Dictionary<string, JToken>
                    {
                        ["age"]    = new JValue(">=18"),
                        ["status"] = new JValue("=active")
                    },
                    Results = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["eligible"] = new FixedValue<JToken>(new JValue(true)) }
                }
            }
        };

        // Both conditions met
        var data1 = JToken.Parse("{ \"age\": 25, \"status\": \"active\" }");
        var r1 = new DecisionTable<JToken>("$", config).Execute(data1, context);
        Assert.That(r1.Success, Is.True);
        Assert.That(data1.SelectToken("$.eligible")?.Value<bool>(), Is.True);

        // One condition not met (age < 18)
        var data2 = JToken.Parse("{ \"age\": 16, \"status\": \"active\" }");
        var r2 = new DecisionTable<JToken>("$", config).Execute(data2, context);
        Assert.That(r2.Success, Is.True);
        Assert.That(data2.SelectToken("$.eligible"), Is.Null);
    }

    // ── Boolean conditions ─────────────────────────────────────────────────

    [Test]
    public void BooleanCondition_TrueMatches()
    {
        var config = new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "flag", Path = "@.flag" } },
            Outputs = new List<DecisionOutput> { new() { Name = "result", Path = "@.result" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Conditions = new Dictionary<string, JToken> { ["flag"] = new JValue("=true") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["result"] = new FixedValue<JToken>(new JValue("flagged")) }
                }
            }
        };

        var data = JToken.Parse("{ \"flag\": true }");
        var result = new DecisionTable<JToken>("$", config).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("flagged"));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static DecisionTableConfig<JToken> NumericRangeConfig(string condition, string gradeValue)
    {
        return new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "score", Path = "@.score" } },
            Outputs = new List<DecisionOutput> { new() { Name = "grade", Path = "@.grade" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Conditions = new Dictionary<string, JToken> { ["score"] = new JValue(condition) },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["grade"] = new FixedValue<JToken>(new JValue(gradeValue)) }
                }
            }
        };
    }

    private static DecisionTableConfig<JToken> AllMatchesConfig(string conflictResolution)
    {
        return new DecisionTableConfig<JToken>
        {
            Inputs  = new List<DecisionInput> { new() { Name = "v", Path = "@.v" } },
            Outputs = new List<DecisionOutput> { new() { Name = "out", Path = "@.out" } },
            Rules   = new List<DecisionRule<JToken>>
            {
                new()
                {
                    Priority   = 0,
                    Conditions = new Dictionary<string, JToken> { ["v"] = new JValue(">=0") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["out"] = new FixedValue<JToken>(new JValue("rule-0")) }
                },
                new()
                {
                    Priority   = 1,
                    Conditions = new Dictionary<string, JToken> { ["v"] = new JValue(">=0") },
                    Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                        { ["out"] = new FixedValue<JToken>(new JValue("rule-1")) }
                }
            },
            Strategy = new DecisionTableExecutionStrategy
            {
                Mode               = "allMatches",
                ConflictResolution = conflictResolution
            }
        };
    }
}
