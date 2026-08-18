using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.EngineTests;

/// <summary>
/// Covers the notation-consistency fixes: script round-trip fidelity, function names in
/// diagnostics, value-level literal handling, visibility of unresolved paths, and the
/// delivery of parse-time notation warnings.
/// </summary>
[TestFixture]
public class NotationConsistencyTests
{
    private ParseOptions<JToken> _options = null!;
    private ScriptEngine<JToken> _engine = null!;
    private INodeAdapter<JToken> _adapter = null!;

    [SetUp]
    public void Setup()
    {
        _options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(_options.CommandsProvider, _options.FunctionsProvider);
        _adapter = new JsonNodeAdapter();
    }

    private (JToken Data, IExecutionContext<JToken> Context) Run(string script, string json = @"{ ""a"": ""Alice"", ""b"": ""Smith"" }")
    {
        var context = JsonExecutionContext.CreateDefault();
        var result = _engine.Execute(script, JToken.Parse(json), context);
        return (result.Data, context);
    }

    private static string AddValue(string rawJsonValue)
        => $@"[{{ ""command"": ""add"", ""path"": ""$.out"", ""value"": {rawJsonValue} }}]";

    // ── Script round-trip (ToScript) ──────────────────────────────────────────

    [Test]
    public void ToScript_FunctionWithLiteralAndPathArgs_ReproducesTheExpression()
    {
        var converter = new FunctionConverter<JToken>(_options.FunctionsProvider);
        var parsed = converter.ParseValue("=partial($.source, 'name', 2)", _adapter);
        Assert.That(parsed!.ToScript(), Is.EqualTo("=partial($.source,'name',2)"));
    }

    [Test]
    public void ToScript_NestedFunction_ReproducesTheExpression()
    {
        var converter = new FunctionConverter<JToken>(_options.FunctionsProvider);
        var parsed = converter.ParseValue("=promote(=fetch($.a), 'wrapper')", _adapter);
        Assert.That(parsed!.ToScript(), Is.EqualTo("=promote(=fetch($.a),'wrapper')"));
    }

    [Test]
    public void ToScript_ProgrammaticFixedValue_QuotesTheLiteral()
    {
        var value = new FixedValue<JToken>(_adapter.CreateString("hello world"));
        Assert.That(value.ToScript(), Is.EqualTo("'hello world'"));
    }

    [Test]
    public void Serialize_ScriptWithFunctionValues_RoundTripsToRunnableScript()
    {
        const string original =
            @"[{""command"":""add"",""path"":""$.full"",""value"":""=partial($.a, 'name')""}]";

        var script = TLioConvert.Parse(original, _options, _adapter);
        var serialized = TLioConvert.Serialize(script, _adapter);

        // The serializer escapes quotes as ', so inspect the decoded value.
        var value = JArray.Parse(serialized)[0]["value"]!.Value<string>();
        Assert.That(value, Is.EqualTo("=partial($.a,'name')"));

        // …and the result must parse back into an equivalent script.
        var reparsed = TLioConvert.Parse(serialized, _options, _adapter);
        Assert.That(TLioConvert.Serialize(reparsed, _adapter), Is.EqualTo(serialized));
    }

    // ── Function names in diagnostics ─────────────────────────────────────────

    [Test]
    public void FunctionName_OfGenericFunction_HasNoAritySuffix()
    {
        foreach (var name in _options.FunctionsProvider.GetRegisteredFunctionNames())
        {
            var fn = _options.FunctionsProvider.GetFunction(name);
            Assert.That(fn!.FunctionName, Does.Not.Contain("`"), $"registered as '{name}'");
        }
    }

    // ── Value-level literals (#3) ─────────────────────────────────────────────

    [TestCase("\"42\"", "42")]
    [TestCase("\"007\"", "007")]
    [TestCase("\"1e3\"", "1e3")]
    [TestCase("\"true\"", "true")]
    [TestCase("\"null\"", "null")]
    public void ValueLevel_StringLiteral_StaysAString(string rawJsonValue, string expected)
    {
        var (data, _) = Run(AddValue(rawJsonValue));
        Assert.That(data["out"]!.Type, Is.EqualTo(JTokenType.String));
        Assert.That(data["out"]!.Value<string>(), Is.EqualTo(expected));
    }

    [Test]
    public void ValueLevel_JsonIntegerLiteral_StaysAnInteger()
    {
        var (data, _) = Run(AddValue("152"));
        Assert.That(data["out"]!.Type, Is.EqualTo(JTokenType.Integer));
        Assert.That(data["out"]!.Value<long>(), Is.EqualTo(152));
    }

    [Test]
    public void ValueLevel_JsonFloatLiteral_StaysAFloat()
    {
        var (data, _) = Run(AddValue("14.5"));
        Assert.That(data["out"]!.Type, Is.EqualTo(JTokenType.Float));
        Assert.That(data["out"]!.Value<double>(), Is.EqualTo(14.5));
    }

    [Test]
    public void ArgumentLevel_BareNumber_IsStillParsedAsNumber()
    {
        // partial's index argument only works when 1 is a number, not the string "1"
        var (data, _) = Run(
            @"[{ ""command"": ""add"", ""path"": ""$.out"", ""value"": ""=partial($.items[*], 1)"" }]",
            @"{ ""items"": [""first"", ""second""] }");
        Assert.That(data["out"]!.Value<string>(), Is.EqualTo("second"));
    }

    // ── Unresolved paths are visible (#4) ─────────────────────────────────────

    [Test]
    public void ValueLevel_PathThatMatchesNothing_LogsWarning()
    {
        var (data, context) = Run(AddValue("\"$.doesNotExist\""));
        Assert.That(data["out"], Is.Null);
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("$.doesNotExist")), Is.True);
    }

    // ── Parse warnings are delivered (#5) ─────────────────────────────────────

    [Test]
    public void ParseWarning_UnknownNestedFunction_IsLoggedOnExecution()
    {
        var (_, context) = Run(AddValue("\"=promote(fetc($.a), 'w')\""));
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("fetc")), Is.True);
    }

    [Test]
    public void ParseWarning_RelativePathMissingDot_IsLoggedOnExecution()
    {
        var (_, context) = Run(AddValue("\"@a\""));
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("@.a")), Is.True);
    }

    [Test]
    public void ParseWarning_RelativePathMissingDotInsideFunctionArg_IsLoggedOnExecution()
    {
        var (_, context) = Run(AddValue("\"=promote(@a, 'w')\""));
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("@.a")), Is.True);
    }

    [Test]
    public void ParseWarning_SurvivesCompilation_AndRepeatsPerExecution()
    {
        var compiled = _engine.Compile(AddValue("\"=promote(fetc($.a), 'w')\""), _adapter);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var context = JsonExecutionContext.CreateDefault();
            compiled.Execute(JToken.Parse(@"{ ""a"": 1 }"), context);
            Assert.That(context.GetLogEntries().Any(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("fetc")), Is.True);
        }
    }

    [Test]
    public void ParseWarning_CleanScript_ProducesNoWarnings()
    {
        var (_, context) = Run(AddValue("\"=fetch($.a)\""));
        Assert.That(context.GetLogEntries().Any(e => e.Level >= LogLevel.Warning), Is.False);
    }

    // ── Case-insensitive function names (#7) ──────────────────────────────────

    [TestCase("=fetch($.a)")]
    [TestCase("=Fetch($.a)")]
    [TestCase("=FETCH($.a)")]
    public void FunctionNames_AreCaseInsensitive(string expression)
    {
        var (data, _) = Run(AddValue($"\"{expression}\""));
        Assert.That(data["out"]!.Value<string>(), Is.EqualTo("Alice"));
    }
}
