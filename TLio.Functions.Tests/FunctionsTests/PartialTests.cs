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
/// Ported from JLio.UnitTests.FunctionsTests.PartialTests.
/// Adaptations:
///   - ExecutionContext.CreateDefault() → JsonExecutionContext.CreateDefault()
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class PartialTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"items\": [\"first\", \"second\", \"third\"], \"result\": null }");
    }

    [Test]
    public void CanGetFirstElementWithNoIndex()
    {
        var partialFn = (IFunction<JToken>)new Partial<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.items[*]")) });
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.result", new FunctionSupportedValue<JToken>(partialFn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("first"));
    }

    [Test]
    public void CanGetSecondElementWithIndex1()
    {
        var partialFn = (IFunction<JToken>)new Partial<JToken>()
            .SetArguments(new Arguments<JToken>
            {
                new FixedValue<JToken>(new JValue("$.items[*]")),
                new FixedValue<JToken>(new JValue(1))
            });
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.result", new FunctionSupportedValue<JToken>(partialFn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("second"));
    }

    [Test]
    public void ReturnsFalseForOutOfRangeIndex()
    {
        var partialFn = (IFunction<JToken>)new Partial<JToken>()
            .SetArguments(new Arguments<JToken>
            {
                new FixedValue<JToken>(new JValue("$.items[*]")),
                new FixedValue<JToken>(new JValue(10))
            });
        var result = partialFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReturnsFalseWithNoArguments()
    {
        var partialFn = new Partial<JToken>();
        var result = partialFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReturnsFalseForNonExistentPath()
    {
        var partialFn = (IFunction<JToken>)new Partial<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.nonExistent")) });
        var result = partialFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReturnsFalseWithNoArguments_LogsWarningOrError()
    {
        var partialFn = new Partial<JToken>();
        partialFn.Execute(data, data, executeOptions);
        Assert.That(executeOptions.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning || e.Level == LogLevel.Error), Is.True);
    }
}
