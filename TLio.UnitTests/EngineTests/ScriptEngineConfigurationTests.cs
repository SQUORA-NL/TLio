using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Commands;
using TLio.Json;

namespace TLio.UnitTests.EngineTests;

/// <summary>
/// Ported from JLio.UnitTests.EngineTests.JLioEngineConfigurationTests.
/// Adaptations:
///   - ParseOptions.CreateDefault() → ParseOptions&lt;JToken&gt;.CreateDefault()
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// Verifies that ParseOptions registers all built-in commands and functions,
/// that unknown commands are handled gracefully, and that custom commands
/// can be added to a provider.
/// </summary>
[TestFixture]
public class ScriptEngineConfigurationTests
{
    // ── Built-in command registration ─────────────────────────────────────────

    [TestCase("set")]
    [TestCase("add")]
    [TestCase("put")]
    [TestCase("remove")]
    [TestCase("copy")]
    [TestCase("move")]
    [TestCase("ifElse")]
    [TestCase("compare")]
    [TestCase("merge")]
    [TestCase("decisionTable")]
    public void CreateDefault_RegistersBuiltinCommand(string commandName)
    {
        var options = ParseOptions<JToken>.CreateDefault();
        Assert.That(options.CommandsProvider.GetCommand(commandName), Is.Not.Null,
            $"built-in command '{commandName}' must be registered");
    }

    // ── Built-in function registration ────────────────────────────────────────

    [TestCase("fetch")]
    [TestCase("indirect")]
    [TestCase("promote")]
    [TestCase("partial")]
    [TestCase("scriptpath")]
    [TestCase("datetime")]
    public void CreateDefault_RegistersBuiltinFunction(string functionName)
    {
        var options = ParseOptions<JToken>.CreateDefault();
        Assert.That(options.FunctionsProvider.GetFunction(functionName), Is.Not.Null,
            $"built-in function '{functionName}' must be registered");
    }

    // ── GetRegisteredCommandNames ─────────────────────────────────────────────

    [Test]
    public void CommandsProvider_GetRegisteredCommandNames_ReturnsAllTen()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var names = options.CommandsProvider.GetRegisteredCommandNames().ToList();
        Assert.That(names.Count, Is.EqualTo(10));
    }

    // ── Unknown command resilience ────────────────────────────────────────────

    [Test]
    public void UnknownCommand_DoesNotThrow_DataUnchanged()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var data = JToken.Parse(@"{ ""x"": 99 }");
        const string script = @"[{ ""command"": ""notRegistered"", ""path"": ""$"" }]";

        TLio.Core.Models.TLioExecutionResult<JToken> result = null!;
        Assert.DoesNotThrow(() => result = engine.Execute(script, data, JsonExecutionContext.CreateDefault()));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.x")?.Value<int>(), Is.EqualTo(99));
    }

    // ── Custom command registration ───────────────────────────────────────────

    [Test]
    public void RegisterCustomCommand_CanBeUsedInScript()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.Register("mySet", () => new Set<JToken>());
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        var data = JToken.Parse(@"{ ""x"": 0 }");
        const string script = @"[{ ""command"": ""mySet"", ""path"": ""$.x"", ""value"": 100 }]";
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
        Assert.That(result.Data.SelectToken("$.x")?.Value<int>(), Is.EqualTo(100));
    }

    // ── Minimal provider ──────────────────────────────────────────────────────

    [Test]
    public void ScriptEngine_WithEmptyProviders_DoesNotThrowOnConstruction()
    {
        var cmds = new CommandsProvider<JToken>();
        var fns  = new FunctionsProvider<JToken>();
        Assert.DoesNotThrow(() => _ = new ScriptEngine<JToken>(cmds, fns));
    }

    // ── Fluent registration ───────────────────────────────────────────────────

    [Test]
    public void RegisterCommand_FluentChaining_ReturnsSameOptions()
    {
        var options = new ParseOptions<JToken>();
        var returned = options.RegisterCommand<Set<JToken>>("s1")
                              .RegisterCommand<Put<JToken>>("p1");
        Assert.That(returned, Is.SameAs(options));
    }

    [Test]
    public void RegisterFunction_FluentChaining_ReturnsSameOptions()
    {
        var options = new ParseOptions<JToken>();
        var returned = options.RegisterFunction<TLio.Functions.Fetch<JToken>>("myFetch");
        Assert.That(returned, Is.SameAs(options));
    }
}
