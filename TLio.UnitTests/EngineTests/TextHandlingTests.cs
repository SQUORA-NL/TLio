using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.EngineTests;

/// <summary>
/// Ported from JLio.UnitTests.EngineTests.TextHandlingTests (ScriptTextHandling).
/// Adaptations:
///   - FunctionConverter → FunctionConverter&lt;JToken&gt;
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// Verifies that FunctionConverter correctly parses all value expression forms
/// used in TLio scripts, mirroring JLio's SplitText / FunctionConverter behaviour.
/// </summary>
[TestFixture]
public class TextHandlingTests
{
    private FunctionConverter<JToken> converter = null!;
    private INodeAdapter<JToken>      adapter   = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        converter = new FunctionConverter<JToken>(options.FunctionsProvider);
        adapter   = new JsonNodeAdapter();
    }

    // ── Function expressions ──────────────────────────────────────────────────

    [Test]
    public void ParseValue_FunctionExpression_ReturnsFunctionSupportedValue()
    {
        var result = converter.ParseValue("=fetch($.x)", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    [Test]
    public void ParseValue_FunctionWithNoArgs_ReturnsFunctionSupportedValue()
    {
        var result = converter.ParseValue("=datetime()", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    [Test]
    public void ParseValue_NestedFunctionArg_ParsesCorrectly()
    {
        // inner =fetch($.a) is the argument to =promote(...)
        var result = converter.ParseValue("=promote(=fetch($.a), 'wrapper')", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    [Test]
    public void ParseValue_UnknownFunction_ReturnsNull()
    {
        var result = converter.ParseValue("=definitelyNotRegistered()", adapter);
        Assert.That(result, Is.Null);
    }

    // ── Path expressions ──────────────────────────────────────────────────────

    [Test]
    public void ParseValue_AbsolutePath_ReturnsPathValue()
    {
        var result = converter.ParseValue("$.some.path", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<PathValue<JToken>>());
    }

    [Test]
    public void ParseValue_RelativePath_ReturnsPathValue()
    {
        var result = converter.ParseValue("@.property", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<PathValue<JToken>>());
    }

    // ── String literals ───────────────────────────────────────────────────────

    [Test]
    public void ParseValue_SingleQuotedString_ReturnsFixedStringValue()
    {
        var result = converter.ParseValue("'hello world'", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"),
                                    JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("hello world"));
    }

    [Test]
    public void ParseValue_DoubleQuotedString_ReturnsFixedStringValue()
    {
        var result = converter.ParseValue("\"quoted\"", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"),
                                    JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("quoted"));
    }

    // ── Numeric / boolean literals ────────────────────────────────────────────

    [Test]
    public void ParseValue_IntegerLiteral_ReturnsFixedNumberValue()
    {
        var result = converter.ParseValue("42", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"),
                                    JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<double>(), Is.EqualTo(42.0));
    }

    [Test]
    public void ParseValue_DecimalLiteral_ReturnsFixedNumberValue()
    {
        var result = converter.ParseValue("3.14", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"),
                                    JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<double>(), Is.EqualTo(3.14).Within(0.001));
    }

    [Test]
    public void ParseValue_BooleanTrue_ReturnsFixedBoolValue()
    {
        var result = converter.ParseValue("true", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"),
                                    JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<bool>(), Is.True);
    }

    [Test]
    public void ParseValue_BooleanFalse_ReturnsFixedBoolValue()
    {
        var result = converter.ParseValue("false", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"),
                                    JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<bool>(), Is.False);
    }

    // ── Bare string fallback ──────────────────────────────────────────────────

    [Test]
    public void ParseValue_UnquotedString_ReturnsFixedStringValue()
    {
        var result = converter.ParseValue("plain text", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"),
                                    JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("plain text"));
    }

    [Test]
    public void ParseValue_EmptyString_ReturnsFixedEmptyString()
    {
        var result = converter.ParseValue("", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"),
                                    JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo(""));
    }

    // ── Through-engine: function value in script ──────────────────────────────

    [Test]
    public void Engine_FetchFunction_ResolvesAtRuntime()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var data = JToken.Parse(@"{ ""in"": ""resolved"", ""out"": """" }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.out"", ""value"": ""=fetch($.in)"" }]";
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
        Assert.That(result.Data.SelectToken("$.out")?.Value<string>(), Is.EqualTo("resolved"));
    }

    [Test]
    public void Engine_PathValue_ResolvesAtRuntime()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var data = JToken.Parse(@"{ ""in"": 99, ""out"": 0 }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.out"", ""value"": ""$.in"" }]";
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
        Assert.That(result.Data.SelectToken("$.out")?.Value<int>(), Is.EqualTo(99));
    }

    // ── Multi-arg function expression ─────────────────────────────────────────

    [Test]
    public void ParseValue_MultiArgFunctionExpression_ParsesAllArgs()
    {
        // partial($.source, 'name', 'age') — 3 arguments
        var result = converter.ParseValue("=partial($.source, 'name', 'age')", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    [Test]
    public void ParseValue_FunctionArgWithCommaInQuotes_KeepsArgIntact()
    {
        // The quoted 'a,b' must not be split at the comma
        var result = converter.ParseValue("=partial($.source, 'a,b')", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }
}
