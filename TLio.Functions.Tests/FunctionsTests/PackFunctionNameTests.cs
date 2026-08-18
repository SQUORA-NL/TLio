using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// Extension-pack functions are generic types, so GetType().Name used to yield "Concat`1".
/// That name reached both log messages and ToScript() output, producing script text that
/// could not be parsed back.
/// </summary>
[TestFixture]
public class PackFunctionNameTests
{
    private ParseOptions<JToken> _options = null!;

    [SetUp]
    public void Setup()
    {
        _options = ParseOptions<JToken>.CreateDefault();
        _options.FunctionsProvider.RegisterText<JToken>();
        _options.FunctionsProvider.RegisterMath<JToken>();
        _options.FunctionsProvider.RegisterTimeDate<JToken>();
    }

    [Test]
    public void EveryRegisteredFunction_HasANameWithoutAritySuffix()
    {
        foreach (var name in _options.FunctionsProvider.GetRegisteredFunctionNames())
        {
            var fn = _options.FunctionsProvider.GetFunction(name);
            Assert.That(fn!.FunctionName, Does.Not.Contain("`"), $"registered as '{name}'");
        }
    }

    [Test]
    public void EveryRegisteredFunction_ReportsANameThatResolvesBack()
    {
        foreach (var name in _options.FunctionsProvider.GetRegisteredFunctionNames())
        {
            var reported = _options.FunctionsProvider.GetFunction(name)!.FunctionName;
            Assert.That(_options.FunctionsProvider.GetFunction(reported), Is.Not.Null,
                $"'{name}' reports itself as '{reported}', which is not resolvable");
        }
    }

    [Test]
    public void ErrorMessage_UsesTheScriptFunctionName()
    {
        var engine = new ScriptEngine<JToken>(_options.CommandsProvider, _options.FunctionsProvider);
        var context = JsonExecutionContext.CreateDefault();
        const string script = @"[{ ""command"": ""add"", ""path"": ""$.out"", ""value"": ""=concat($.a, $.missing)"" }]";

        engine.Execute(script, JToken.Parse(@"{ ""a"": ""x"" }"), context);

        var errors = context.GetLogEntries().Where(e => e.Level == LogLevel.Error).ToList();
        Assert.That(errors, Is.Not.Empty);
        Assert.That(errors.Any(e => e.Group == "concat"), Is.True);
        Assert.That(errors.Any(e => e.Group.Contains("`")), Is.False);
    }

    [Test]
    public void ToScript_OfPackFunction_IsParseableBack()
    {
        var converter = new FunctionConverter<JToken>(_options.FunctionsProvider);
        var script = converter.ParseValue("=concat($.a, ' ', $.b)", new JsonNodeAdapter())!.ToScript();

        Assert.That(script, Is.EqualTo("=concat($.a,' ',$.b)"));

        var reparsed = converter.ParseValue(script, new JsonNodeAdapter());
        Assert.That(reparsed!.ToScript(), Is.EqualTo(script));
    }
}
