using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Functions.Logic;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.LogicTests;

/// <summary>
/// Coverage for =if(condition, whenTrue, whenFalse) — the conditional value.
/// The laziness cases are the ones that matter: the branch not taken must never be evaluated.
/// </summary>
[TestFixture]
public class IfFunctionTests
{
    private const string Document = """
    {
      "flag": true,
      "off": false,
      "flagText": "true",
      "age": 22,
      "name": "Sanne",
      "nickname": null,
      "excess": 300
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

    private static IfFunction<JToken> If(params IFunctionSupportedValue<JToken>[] args)
    {
        var fn = new IfFunction<JToken>();
        var arguments = new Arguments<JToken>();
        arguments.AddRange(args);
        fn.SetArguments(arguments);
        return fn;
    }

    private static FixedValue<JToken> Literal(object value) => new(new JValue(value));

    // ── Branch selection ──────────────────────────────────────────────────────

    [Test]
    public void If_ConditionTrue_ReturnsWhenTrueBranch()
    {
        var result = If(new PathValue<JToken>("$.flag"), Literal("yes"), Literal("no"))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("yes"));
    }

    [Test]
    public void If_ConditionFalse_ReturnsWhenFalseBranch()
    {
        var result = If(new PathValue<JToken>("$.off"), Literal("yes"), Literal("no"))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("no"));
    }

    /// <summary>The text "true" is truthy — the same rule ifElse and decisionTable use.</summary>
    [Test]
    public void If_ConditionIsTheTextTrue_IsTruthy()
    {
        var result = If(new PathValue<JToken>("$.flagText"), Literal("yes"), Literal("no"))
            .Execute(data, data, context);

        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("yes"));
    }

    /// <summary>A number is not a condition — 22 is not truthy, so the false branch wins.</summary>
    [Test]
    public void If_ConditionIsANumber_IsNotTruthy()
    {
        var result = If(new PathValue<JToken>("$.age"), Literal("yes"), Literal("no"))
            .Execute(data, data, context);

        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("no"));
    }

    /// <summary>A condition path matching nothing is an answer (false), not a failure.</summary>
    [Test]
    public void If_ConditionPathNotFound_TakesFalseBranch()
    {
        var result = If(new PathValue<JToken>("$.missing"), Literal("yes"), Literal("no"))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("no"));
    }

    /// <summary>A present-but-null condition is falsy.</summary>
    [Test]
    public void If_ConditionIsNull_TakesFalseBranch()
    {
        var result = If(new PathValue<JToken>("$.nickname"), Literal("yes"), Literal("no"))
            .Execute(data, data, context);

        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("no"));
    }

    // ── Branch resolution ─────────────────────────────────────────────────────

    [Test]
    public void If_ChosenBranchIsAPath_ResolvesIt()
    {
        var result = If(new PathValue<JToken>("$.flag"), new PathValue<JToken>("$.excess"), Literal(0))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<int>(), Is.EqualTo(300));
    }

    [Test]
    public void If_ChosenBranchPathNotFound_ReturnsFailed()
    {
        var result = If(new PathValue<JToken>("$.flag"), new PathValue<JToken>("$.missing"), Literal(0))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.False);
    }

    // ── Laziness ──────────────────────────────────────────────────────────────

    [Test]
    public void If_UntakenBranchIsNeverEvaluated()
    {
        var whenTrue = new CountingValue(new JValue("yes"));
        var whenFalse = new CountingValue(new JValue("no"));

        If(new PathValue<JToken>("$.off"), whenTrue, whenFalse).Execute(data, data, context);

        Assert.That(whenTrue.Calls, Is.Zero, "the whenTrue branch must not be evaluated when the condition is false");
        Assert.That(whenFalse.Calls, Is.EqualTo(1));
    }

    /// <summary>
    /// The reason laziness matters: fetch on a missing path fails, and a failed function aborts
    /// the script. =if(=exists($.x),=fetch($.x),'-') must therefore be safe when $.x is absent.
    /// </summary>
    [Test]
    public void If_GuardedFetchOfMissingPath_Succeeds()
    {
        var engine = NewEngine();
        var script = """[{ "command": "add", "path": "$.out", "value": "=if(=exists($.missing),=fetch($.missing),'-')" }]""";

        var result = engine.Execute(script, JToken.Parse(Document), context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data["out"]!.Value<string>(), Is.EqualTo("-"));
        Assert.That(context.GetLogEntries().Any(e => e.Group == "fetch"), Is.False,
            "fetch must not have run at all");
    }

    [Test]
    public void If_ThroughTheEngine_CollapsesAnIfElseBlock()
    {
        var engine = NewEngine();
        var script = """
        [{ "command": "put", "path": "$.excess",
           "value": "=if(=lessThan($.age,24),=sum($.excess,300),=fetch($.excess))" }]
        """;

        var result = engine.Execute(script, JToken.Parse(Document), context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data["excess"]!.Value<int>(), Is.EqualTo(600));
    }

    // ── Arity ─────────────────────────────────────────────────────────────────

    [Test]
    public void If_TwoArguments_ReturnsFailed()
    {
        var result = If(new PathValue<JToken>("$.flag"), Literal("yes")).Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Group == "if"), Is.True);
    }

    [Test]
    public void If_NoArguments_ReturnsFailed()
    {
        var result = If().Execute(data, data, context);

        Assert.That(result.Success, Is.False);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ScriptEngine<JToken> NewEngine()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        return new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    /// <summary>Records how often it was asked for its value, so laziness can be asserted.</summary>
    private sealed class CountingValue : IFunctionSupportedValue<JToken>
    {
        private readonly JToken _value;

        public CountingValue(JToken value) => _value = value;

        public int Calls { get; private set; }

        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context)
        {
            Calls++;
            return FunctionResult<JToken>.Successful(_value);
        }

        public string ToScript() => _value.ToString();
    }
}
