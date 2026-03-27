using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.DecisionTableBuilderTests.
/// Adaptations:
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// Verifies the fluent builder API for constructing DecisionTable configurations:
///   - DecisionTableConfigBuilder&lt;TNode&gt; produces a valid DecisionTableConfig
///   - TLioScript extension method AddDecisionTable chains correctly
/// </summary>
[TestFixture]
public class DecisionTableBuilderTests
{
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
    }

    // ── Config builder ─────────────────────────────────────────────────────────

    [Test]
    public void ConfigBuilder_ProducesCorrectInputsAndOutputs()
    {
        var config = DecisionTableBuilders.BuildConfig<JToken>()
            .WithInput("age", "@.age")
            .WithOutput("tier", "@.tier")
            .Build();

        Assert.That(config.Inputs.Count, Is.EqualTo(1));
        Assert.That(config.Inputs[0].Name, Is.EqualTo("age"));
        Assert.That(config.Inputs[0].Path, Is.EqualTo("@.age"));

        Assert.That(config.Outputs.Count, Is.EqualTo(1));
        Assert.That(config.Outputs[0].Name, Is.EqualTo("tier"));
        Assert.That(config.Outputs[0].Path, Is.EqualTo("@.tier"));
    }

    [Test]
    public void ConfigBuilder_WithStrategy_SetsStrategyProperties()
    {
        var config = DecisionTableBuilders.BuildConfig<JToken>()
            .WithStrategy("bestMatch", "lastWins")
            .Build();

        Assert.That(config.Strategy, Is.Not.Null);
        Assert.That(config.Strategy!.Mode, Is.EqualTo("bestMatch"));
        Assert.That(config.Strategy.ConflictResolution, Is.EqualTo("lastWins"));
    }

    [Test]
    public void ConfigBuilder_WithRule_AddsRuleToList()
    {
        var rule = new DecisionRule<JToken>
        {
            Priority   = 2,
            Conditions = new Dictionary<string, JToken> { ["x"] = new JValue("=1") },
            Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                { ["y"] = new FixedValue<JToken>(new JValue("done")) }
        };

        var config = DecisionTableBuilders.BuildConfig<JToken>()
            .WithInput("x", "@.x")
            .WithOutput("y", "@.y")
            .WithRule(rule)
            .Build();

        Assert.That(config.Rules.Count, Is.EqualTo(1));
        Assert.That(config.Rules[0].Priority, Is.EqualTo(2));
    }

    [Test]
    public void ConfigBuilder_WithDefaultResults_SetsDefaults()
    {
        var defaults = new Dictionary<string, IFunctionSupportedValue<JToken>>
        {
            ["out"] = new FixedValue<JToken>(new JValue("default-value"))
        };

        var config = DecisionTableBuilders.BuildConfig<JToken>()
            .WithOutput("out", "@.out")
            .WithDefaultResults(defaults)
            .Build();

        Assert.That(config.DefaultResults, Is.Not.Null);
        Assert.That(config.DefaultResults!.ContainsKey("out"), Is.True);
    }

    // ── Script extension method ────────────────────────────────────────────────

    [Test]
    public void AddDecisionTable_AppendsCommandToScript()
    {
        var config = DecisionTableBuilders.BuildConfig<JToken>()
            .WithInput("s", "@.s")
            .WithOutput("r", "@.r")
            .WithRule(new DecisionRule<JToken>
            {
                Conditions = new Dictionary<string, JToken> { ["s"] = new JValue("=yes") },
                Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                    { ["r"] = new FixedValue<JToken>(new JValue("ok")) }
            })
            .Build();

        var script = new TLioScript<JToken>()
            .AddDecisionTable("$", config);

        Assert.That(script.Count, Is.EqualTo(1));
        Assert.That(script[0], Is.InstanceOf<DecisionTable<JToken>>());
    }

    [Test]
    public void AddDecisionTable_ExecutesCorrectly()
    {
        var config = DecisionTableBuilders.BuildConfig<JToken>()
            .WithInput("s", "@.s")
            .WithOutput("r", "@.r")
            .WithRule(new DecisionRule<JToken>
            {
                Conditions = new Dictionary<string, JToken> { ["s"] = new JValue("=yes") },
                Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                    { ["r"] = new FixedValue<JToken>(new JValue("ok")) }
            })
            .Build();

        var data = JToken.Parse("{ \"s\": \"yes\" }");
        var script = new TLioScript<JToken>().AddDecisionTable("$", config);

        var result = script.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.r")?.Value<string>(), Is.EqualTo("ok"));
    }

    // ── Chaining multiple operations ─────────────────────────────────────────

    [Test]
    public void ChainedScriptWithDecisionTable_ExecutesAllCommands()
    {
        var config = DecisionTableBuilders.BuildConfig<JToken>()
            .WithInput("tier", "@.tier")
            .WithOutput("discount", "@.discount")
            .WithRule(new DecisionRule<JToken>
            {
                Conditions = new Dictionary<string, JToken> { ["tier"] = new JValue("=gold") },
                Results    = new Dictionary<string, IFunctionSupportedValue<JToken>>
                    { ["discount"] = new FixedValue<JToken>(new JValue(20)) }
            })
            .Build();

        var data = JToken.Parse("{ \"tier\": \"gold\" }");
        var script = new TLioScript<JToken>
        {
            new Put<JToken>("$.processed", new FixedValue<JToken>(new JValue(true)))
        };
        script.AddDecisionTable("$", config);

        var result = script.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.discount")?.Value<int>(), Is.EqualTo(20));
        Assert.That(result.Data.SelectToken("$.processed")?.Value<bool>(), Is.True);
    }
}
