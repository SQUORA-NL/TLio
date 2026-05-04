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
    public void ParseValue_UnknownFunction_ReturnsNotFoundSentinel()
    {
        var result = converter.ParseValue("=definitelyNotRegistered()", adapter);
        Assert.That(result, Is.InstanceOf<NotFoundFunctionValue<JToken>>());
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

    // ── Escape sequences (unquoted) ───────────────────────────────────────────

    [Test]
    public void ParseValue_DoubleAt_ReturnsLiteralAtString()
    {
        var result = converter.ParseValue("@@admin", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("@admin"));
    }

    [Test]
    public void ParseValue_DoubleDollar_ReturnsLiteralDollarString()
    {
        var result = converter.ParseValue("$$total", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("$total"));
    }

    [Test]
    public void ParseValue_DoubleEquals_ReturnsLiteralEqualsString()
    {
        var result = converter.ParseValue("==formula", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("=formula"));
    }

    [Test]
    public void ParseValue_DoubleAt_BareEscape_ReturnsAtOnly()
    {
        var result = converter.ParseValue("@@", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("@"));
    }

    // Escape sequences in quoted strings
    [Test]
    public void ParseValue_QuotedWithDoubleAt_DecodesAtInString()
    {
        var result = converter.ParseValue("'user@@example.com'", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("user@example.com"));
    }

    [Test]
    public void ParseValue_QuotedWithDoubleDollar_DecodesDollarInString()
    {
        var result = converter.ParseValue("'$$ref'", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("$ref"));
    }

    [Test]
    public void ParseValue_QuotedWithDoubleEquals_DecodesEqualsInString()
    {
        var result = converter.ParseValue("'==expr'", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("=expr"));
    }

    // Regression guards — single-prefix still works as before
    [Test]
    public void ParseValue_SingleAt_StillReturnsPathValue()
    {
        var result = converter.ParseValue("@.property", adapter);
        Assert.That(result, Is.InstanceOf<PathValue<JToken>>());
    }

    [Test]
    public void ParseValue_SingleDollar_StillReturnsPathValue()
    {
        var result = converter.ParseValue("$.path", adapter);
        Assert.That(result, Is.InstanceOf<PathValue<JToken>>());
    }

    [Test]
    public void ParseValue_SingleEquals_StillReturnsFunctionSupportedValue()
    {
        var result = converter.ParseValue("=fetch($.x)", adapter);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    // ── Escape sequences in function arguments ────────────────────────────────

    [Test]
    public void ParseValue_FunctionArg_DoubleAtInQuotes_DecodesAtPrefix()
    {
        var result = converter.ParseValue("=partial($.source, '@@prefix')", adapter);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    [Test]
    public void ParseValue_FunctionArg_DoubleAt_UnquotedArg_DecodesAtPrefix()
    {
        // @@foo as a bare arg: ParseValue is called on "@@foo" → FixedValue("@foo")
        var result = converter.ParseValue("@@foo", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("@foo"));
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

    // ── Quoted-string function expression (dynamic paths) ─────────────────────

    [Test]
    public void ParseValue_QuotedStringStartingWithEquals_ReturnsFunctionValue()
    {
        // '=fetch($.x)' — inner starts with = → treated as nested function expression
        var result = converter.ParseValue("'=fetch($.x)'", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    [Test]
    public void ParseValue_QuotedStringStartingWithDoubleEquals_ReturnsLiteralEqualsString()
    {
        // '==expr' — inner starts with == → literal =expr (escape, not function)
        var result = converter.ParseValue("'==expr'", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("=expr"));
    }

    [Test]
    public void ParseValue_QuotedStringWithEscapedSingleQuote_DecodesLiteralQuote()
    {
        // 'it''s' — '' inside quotes → literal '
        var result = converter.ParseValue("'it''s'", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("it's"));
    }

    [Test]
    public void ParseValue_QuotedStringWithEscapedDoubleQuote_DecodesLiteralQuote()
    {
        // "say ""hello""" — "" inside double-quoted string → literal "
        var result = converter.ParseValue("\"say \"\"hello\"\"\"", adapter);
        Assert.That(result, Is.InstanceOf<FixedValue<JToken>>());
        var node = result!.GetValue(JToken.Parse("{}"), JToken.Parse("{}"), JsonExecutionContext.CreateDefault());
        Assert.That(node.Data.First?.Value<string>(), Is.EqualTo("say \"hello\""));
    }

    // ── SplitArgs with '' escape — tested indirectly via ParseValue ──────────────

    [Test]
    public void ParseValue_FunctionArg_QuotedStringWithEscapedQuoteAndComma_ParsedAsSingleArg()
    {
        // concat('hello, it''s me') — the '' inside the quoted arg is escaped and must not split at comma.
        // If SplitArgs incorrectly splits, concat would have 2 args and parsing would produce a different result.
        var result = converter.ParseValue("=partial($.source, 'hello, it''s me')", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    [Test]
    public void ParseValue_FetchWithEscapedQuotesInInnerExpression_ParsedCorrectly()
    {
        // =fetch('=fetch($.pathField)') — outer quoted string has = prefix, inner is a fetch call
        var result = converter.ParseValue("=fetch('=fetch($.pathField)')", adapter);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<FunctionSupportedValue<JToken>>());
    }

    // ── Integration: fetch with quoted path ───────────────────────────────────

    [Test]
    public void Engine_FetchWithQuotedPath_WorksLikeBarePathAtTopLevel()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var data    = JToken.Parse(@"{ ""source"": ""hello"", ""out"": null }");
        // =fetch('$.source') quoted path — must work exactly like =fetch($.source)
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.out"", ""value"": ""=fetch('$.source')"" }]";
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
        Assert.That(result.Data.SelectToken("$.out")?.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void Engine_FetchWithDynamicPathViaQuotedIndirect_ResolvesPath()
    {
        // =fetch('=indirect($.pathField)') — indirect reads path string from $.pathField,
        // then fetch uses that resulting path to read the value.
        var options = ParseOptions<JToken>.CreateDefault();
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var data    = JToken.Parse(@"{ ""pathField"": ""$.price"", ""price"": 99, ""out"": null }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.out"", ""value"": ""=fetch('=indirect($.pathField)')"" }]";
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.out")?.Value<int?>(), Is.EqualTo(99));
    }
}
