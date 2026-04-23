using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// Ported from JLio.UnitTests.FunctionsTests.IndirectTests.
/// Adaptations:
///   - ExecutionContext.CreateDefault() → JsonExecutionContext.CreateDefault()
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class IndirectTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"pathRef\": \"$.source\", \"source\": \"hello\", \"target\": null }");
    }

    [Test]
    public void CanFetchValueViaIndirectPath()
    {
        // $.pathRef contains "$.source"; indirect should return data.source = "hello"
        var indirectFn = (IFunction<JToken>)new Indirect<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.pathRef")) });
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.target", new FunctionSupportedValue<JToken>(indirectFn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.target")?.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void ReturnsFalseForNonStringReference()
    {
        // $.source is "hello" (a string value, not a path)
        // indirect($.source) would try to use "hello" as a path — "hello" selects nothing
        var indirectFn = (IFunction<JToken>)new Indirect<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.source")) });
        var result = indirectFn.Execute(data, data, executeOptions);

        // "hello" is a valid string but not a valid path → selects nothing → fail
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReturnsFalseWithNoArguments()
    {
        var indirectFn = new Indirect<JToken>();
        var result = indirectFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReturnsFalseForNonExistentReference()
    {
        var indirectFn = (IFunction<JToken>)new Indirect<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.nonExistent")) });
        var result = indirectFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReturnsFalseWithNoArguments_LogsWarning()
    {
        var indirectFn = new Indirect<JToken>();
        indirectFn.Execute(data, data, executeOptions);
        Assert.That(executeOptions.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void NonStringPathReference_LogsWarning()
    {
        // $.ref holds an array (not a string). indirect cannot use an array as a path → logs warning.
        var d = JToken.Parse(@"{ ""ref"": [1, 2, 3] }");
        var indirectFn = (IFunction<JToken>)new Indirect<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.ref")) });
        var result = indirectFn.Execute(d, d, executeOptions);
        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }
}
