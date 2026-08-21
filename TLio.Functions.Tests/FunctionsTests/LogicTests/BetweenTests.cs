using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions.Logic;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.LogicTests;

/// <summary>
/// Coverage for =between(value, low, high) — inclusive on both bounds, the numeric sibling of
/// isDateBetween. The bound-inclusivity cases and the unsorted-bounds case are the ones that
/// keep the rate-table band checks honest.
/// </summary>
[TestFixture]
public class BetweenTests
{
    private const string Document = """
    {
      "age": 24,
      "ageText": "24",
      "low": 18,
      "high": 24,
      "nickname": null,
      "tags": ["a"],
      "address": { "city": "Utrecht" }
    }
    """;

    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(Document);
    }

    private static FixedValue<JToken> Literal(object value) => new(new JValue(value));

    private static bool Eval(IExecutionContext<JToken> context, JToken data,
        params IFunctionSupportedValue<JToken>[] args)
    {
        var fn = new BetweenFunction<JToken>();
        var arguments = new Arguments<JToken>();
        arguments.AddRange(args);
        fn.SetArguments(arguments);

        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True, "between should answer, not fail");
        return result.Data.First!.Value<bool>();
    }

    // ── Inclusive bounds ──────────────────────────────────────────────────────

    [TestCase(17, false)]
    [TestCase(18, true)]   // low bound is included
    [TestCase(21, true)]
    [TestCase(24, true)]   // high bound is included
    [TestCase(25, false)]
    public void Between_IsInclusiveOnBothBounds(int value, bool expected)
    {
        Assert.That(Eval(context, data, Literal(value), Literal(18), Literal(24)), Is.EqualTo(expected));
    }

    [Test]
    public void Between_ResolvesPathArguments()
    {
        Assert.That(Eval(context, data,
            new PathValue<JToken>("$.age"),
            new PathValue<JToken>("$.low"),
            new PathValue<JToken>("$.high")), Is.True);
    }

    /// <summary>Numeric text compares numerically, matching =equals and the decision table.</summary>
    [Test]
    public void Between_NumericTextComparesNumerically()
    {
        Assert.That(Eval(context, data, new PathValue<JToken>("$.ageText"), Literal(18), Literal(24)), Is.True);
    }

    /// <summary>Non-numeric operands fall back to ordinal string ordering, as elsewhere.</summary>
    [Test]
    public void Between_ComparesTextOrdinally()
    {
        Assert.That(Eval(context, data, Literal("m"), Literal("a"), Literal("z")), Is.True);
        Assert.That(Eval(context, data, Literal("Z"), Literal("a"), Literal("z")), Is.False);
    }

    /// <summary>Bounds are taken as given — swapping them does not sort them.</summary>
    [Test]
    public void Between_ReversedBounds_IsFalse()
    {
        Assert.That(Eval(context, data, Literal(21), Literal(24), Literal(18)), Is.False);
    }

    [Test]
    public void Between_EqualBounds_MatchesOnlyThatValue()
    {
        Assert.That(Eval(context, data, Literal(24), Literal(24), Literal(24)), Is.True);
        Assert.That(Eval(context, data, Literal(23), Literal(24), Literal(24)), Is.False);
    }

    // ── Missing / unorderable operands ────────────────────────────────────────

    /// <summary>
    /// A path matching nothing is false with a warning, not a failure — this is the deliberate
    /// divergence from isDateBetween, which fails. See the class summary on BetweenFunction.
    /// </summary>
    [Test]
    public void Between_PathNotFound_IsFalseNotFailed()
    {
        Assert.That(Eval(context, data, new PathValue<JToken>("$.missing"), Literal(18), Literal(24)), Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Group == "between"), Is.True);
    }

    [Test]
    public void Between_NullValue_IsFalse()
    {
        Assert.That(Eval(context, data, new PathValue<JToken>("$.nickname"), Literal(18), Literal(24)), Is.False);
    }

    [TestCase("$.tags")]
    [TestCase("$.address")]
    public void Between_ContainerValue_IsFalse(string path)
    {
        Assert.That(Eval(context, data, new PathValue<JToken>(path), Literal(18), Literal(24)), Is.False);
    }

    // ── Arity ─────────────────────────────────────────────────────────────────

    [Test]
    public void Between_TwoArguments_ReturnsFailed()
    {
        var fn = new BetweenFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.age"), Literal(18) });

        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    // ── Through the engine ────────────────────────────────────────────────────

    [Test]
    public void Between_ThroughTheEngine_ReplacesTheAndPair()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var script = """[{ "command": "add", "path": "$.inBand", "value": "=between($.age,$.low,$.high)" }]""";

        var result = engine.Execute(script, JToken.Parse(Document), context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data["inBand"]!.Value<bool>(), Is.True);
    }
}
