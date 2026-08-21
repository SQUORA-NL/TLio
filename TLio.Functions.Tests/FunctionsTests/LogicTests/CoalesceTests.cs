using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions.Logic;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.LogicTests;

/// <summary>
/// Coverage for =coalesce(a, b, ...) — the first argument that is neither null nor empty text.
/// The interesting cases are the skips: a missing path is skipped, and nothing left standing
/// is a failure rather than a silent null.
/// </summary>
[TestFixture]
public class CoalesceTests
{
    private const string Document = """
    {
      "primary": null,
      "secondary": "",
      "tertiary": "sanne@example.nl",
      "zero": 0,
      "off": false,
      "emptyList": [],
      "emptyObject": {},
      "party": { "role": "policyholder" }
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

    private static CoalesceFunction<JToken> Coalesce(params IFunctionSupportedValue<JToken>[] args)
    {
        var fn = new CoalesceFunction<JToken>();
        var arguments = new Arguments<JToken>();
        arguments.AddRange(args);
        fn.SetArguments(arguments);
        return fn;
    }

    private static FixedValue<JToken> Literal(object value) => new(new JValue(value));

    // ── Happy path ────────────────────────────────────────────────────────────

    [Test]
    public void Coalesce_SkipsNullAndEmpty_ReturnsFirstRealValue()
    {
        var result = Coalesce(
            new PathValue<JToken>("$.primary"),
            new PathValue<JToken>("$.secondary"),
            new PathValue<JToken>("$.tertiary")).Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("sanne@example.nl"));
    }

    [Test]
    public void Coalesce_FirstArgumentQualifies_ReturnsItImmediately()
    {
        var result = Coalesce(Literal("first"), Literal("second")).Execute(data, data, context);

        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("first"));
    }

    [Test]
    public void Coalesce_PathNotFound_IsSkippedNotFailed()
    {
        var result = Coalesce(new PathValue<JToken>("$.missing"), Literal("fallback"))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("fallback"));
    }

    // ── The values people get wrong ───────────────────────────────────────────

    /// <summary>Zero and false are values. Only null and "" are skipped.</summary>
    [TestCase("$.zero", 0)]
    [TestCase("$.off", false)]
    public void Coalesce_FalsyButPresentValuesQualify(string path, object expected)
    {
        var result = Coalesce(new PathValue<JToken>(path), Literal("fallback"))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(((JValue)result.Data.First!).Value, Is.EqualTo(expected));
    }

    /// <summary>An empty array or object is a value too — this is where isEmpty differs.</summary>
    [TestCase("$.emptyList")]
    [TestCase("$.emptyObject")]
    public void Coalesce_EmptyContainersQualify(string path)
    {
        var result = Coalesce(new PathValue<JToken>(path), Literal("fallback"))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.Not.EqualTo(JTokenType.String));
    }

    [Test]
    public void Coalesce_ReturnsObjectsWhole()
    {
        var result = Coalesce(new PathValue<JToken>("$.primary"), new PathValue<JToken>("$.party"))
            .Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!["role"]!.Value<string>(), Is.EqualTo("policyholder"));
    }

    // ── Failure ───────────────────────────────────────────────────────────────

    [Test]
    public void Coalesce_NothingQualifies_ReturnsFailedAndLogsError()
    {
        var result = Coalesce(
            new PathValue<JToken>("$.primary"),
            new PathValue<JToken>("$.secondary"),
            new PathValue<JToken>("$.missing")).Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Group == "coalesce" &&
            e.Level == Microsoft.Extensions.Logging.LogLevel.Error), Is.True);
    }

    [Test]
    public void Coalesce_NoArguments_ReturnsFailed()
    {
        var result = Coalesce().Execute(data, data, context);

        Assert.That(result.Success, Is.False);
    }

    // ── Through the engine ────────────────────────────────────────────────────

    [Test]
    public void Coalesce_ThroughTheEngine_ReplacesNestedFetchDefaults()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var script = """
        [{ "command": "add", "path": "$.email",
           "value": "=coalesce($.primary,$.secondary,$.tertiary,'unknown')" }]
        """;

        var result = engine.Execute(script, JToken.Parse(Document), context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data["email"]!.Value<string>(), Is.EqualTo("sanne@example.nl"));
    }
}
