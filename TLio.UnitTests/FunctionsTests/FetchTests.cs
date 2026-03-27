using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests;

/// <summary>
/// Ported from JLio.UnitTests.FunctionsTests.FetchTests.
/// Adaptations:
///   - ExecutionContext.CreateDefault() → JsonExecutionContext.CreateDefault()
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class FetchTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"source\": \"hello\", \"number\": 42, \"nested\": { \"value\": \"world\" }, \"target\": null }");
    }

    [Test]
    public void CanFetchStringValue()
    {
        var fetchFn = (IFunction<JToken>)new Fetch<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.source")) });
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.target", new FunctionSupportedValue<JToken>(fetchFn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.target")?.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void CanFetchNestedValue()
    {
        var fetchFn = (IFunction<JToken>)new Fetch<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.nested.value")) });
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.target", new FunctionSupportedValue<JToken>(fetchFn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.target")?.Value<string>(), Is.EqualTo("world"));
    }

    [Test]
    public void CanFetchNumericValue()
    {
        var fetchFn = (IFunction<JToken>)new Fetch<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.number")) });
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.target", new FunctionSupportedValue<JToken>(fetchFn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.target")?.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void ReturnsFalseForNonExistentPath()
    {
        var fetchFn = (IFunction<JToken>)new Fetch<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.nonExistent")) });
        var result = fetchFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReturnsFalseWithNoArguments()
    {
        var fetchFn = new Fetch<JToken>();
        var result = fetchFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }
}
