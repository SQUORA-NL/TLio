using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Json;

namespace TLio.UnitTests.EngineTests;

/// <summary>
/// Ported from JLio.UnitTests.EngineTests.JLioEngineTests.
/// Adaptations:
///   - JLioEngine → ScriptEngine&lt;JToken&gt; with ParseOptions
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// Verifies fundamental ScriptEngine behaviour: script parsing, single-command
/// execution, unknown commands, and malformed input resilience.
/// </summary>
[TestFixture]
public class ScriptEngineTests
{
    private ScriptEngine<JToken> engine = null!;
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        context = JsonExecutionContext.CreateDefault();
    }

    // ── Empty / malformed scripts ─────────────────────────────────────────────

    [Test]
    public void Execute_EmptyScript_ReturnsSuccessWithUnchangedData()
    {
        var data = JToken.Parse(@"{ ""x"": 1 }");
        var result = engine.Execute("[]", data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.x")?.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void Execute_InvalidJson_DoesNotThrow_ReturnsSuccess()
    {
        var data = JToken.Parse(@"{ ""x"": 1 }");
        TLio.Core.Models.TLioExecutionResult<JToken> result = null!;
        Assert.DoesNotThrow(() => result = engine.Execute("not valid json", data, context));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.x")?.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void Execute_NonArrayRoot_TreatsAsEmptyScript()
    {
        var data = JToken.Parse(@"{ ""x"": 1 }");
        var result = engine.Execute(@"{ ""command"": ""set"" }", data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.x")?.Value<int>(), Is.EqualTo(1));
    }

    // ── Set command ───────────────────────────────────────────────────────────

    [Test]
    public void Execute_SetCommand_OverwritesExistingValue()
    {
        var data = JToken.Parse(@"{ ""x"": 1 }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.x"", ""value"": 42 }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.x")?.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void Execute_SetCommand_WithStringValue()
    {
        var data = JToken.Parse(@"{ ""name"": """" }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.name"", ""value"": ""Alice"" }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.name")?.Value<string>(), Is.EqualTo("Alice"));
    }

    [Test]
    public void Execute_SetCommand_WithBooleanValue()
    {
        var data = JToken.Parse(@"{ ""flag"": false }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.flag"", ""value"": true }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.flag")?.Value<bool>(), Is.True);
    }

    [Test]
    public void Execute_SetCommand_WithNullValue()
    {
        var data = JToken.Parse(@"{ ""x"": 99 }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.x"", ""value"": null }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.x")?.Type, Is.EqualTo(JTokenType.Null));
    }

    // ── Put / remove ──────────────────────────────────────────────────────────

    [Test]
    public void Execute_PutCommand_CreatesNewProperty()
    {
        var data = JToken.Parse("{}");
        const string script = @"[{ ""command"": ""put"", ""path"": ""$.name"", ""value"": ""Bob"" }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.name")?.Value<string>(), Is.EqualTo("Bob"));
    }

    [Test]
    public void Execute_RemoveCommand_DeletesProperty()
    {
        var data = JToken.Parse(@"{ ""a"": 1, ""b"": 2 }");
        const string script = @"[{ ""command"": ""remove"", ""path"": ""$.a"" }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.a"), Is.Null);
        Assert.That(result.Data.SelectToken("$.b")?.Value<int>(), Is.EqualTo(2));
    }

    // ── Multiple commands ─────────────────────────────────────────────────────

    [Test]
    public void Execute_MultipleCommands_RunInOrder()
    {
        var data = JToken.Parse("{}");
        const string script = @"[
            { ""command"": ""put"", ""path"": ""$.first"",  ""value"": 1 },
            { ""command"": ""put"", ""path"": ""$.second"", ""value"": 2 },
            { ""command"": ""put"", ""path"": ""$.third"",  ""value"": 3 }
        ]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.first")?.Value<int>(), Is.EqualTo(1));
        Assert.That(result.Data.SelectToken("$.second")?.Value<int>(), Is.EqualTo(2));
        Assert.That(result.Data.SelectToken("$.third")?.Value<int>(), Is.EqualTo(3));
    }

    // ── Unknown command ───────────────────────────────────────────────────────

    [Test]
    public void Execute_UnknownCommand_DoesNotThrow_SucceedsWithUnchangedData()
    {
        var data = JToken.Parse(@"{ ""x"": 1 }");
        const string script = @"[{ ""command"": ""definitelyNotRegistered"", ""path"": ""$"" }]";
        TLio.Core.Models.TLioExecutionResult<JToken> result = null!;
        Assert.DoesNotThrow(() => result = engine.Execute(script, data, context));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.x")?.Value<int>(), Is.EqualTo(1));
    }

    // ── Function values ───────────────────────────────────────────────────────

    [Test]
    public void Execute_FetchFunctionValue_CopiesFromSourcePath()
    {
        var data = JToken.Parse(@"{ ""src"": ""hello"", ""dst"": """" }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.dst"", ""value"": ""=fetch($.src)"" }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.dst")?.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void Execute_PathValue_CopiesFromSourcePath()
    {
        var data = JToken.Parse(@"{ ""src"": 42, ""dst"": 0 }");
        // A bare $-path as value acts as a PathValue
        const string script = @"[{ ""command"": ""set"", ""path"": ""$.dst"", ""value"": ""$.src"" }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.dst")?.Value<int>(), Is.EqualTo(42));
    }
}
