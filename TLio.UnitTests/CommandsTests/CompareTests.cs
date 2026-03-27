using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.CompareTests.
/// Adaptations:
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///   - fluent JLioScript API → TLioScript&lt;JToken&gt; object list
/// </summary>
[TestFixture]
public class CompareTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"a\": 1, \"b\": 2, \"c\": 1, \"d\": \"hello\", \"e\": \"hello\", \"f\": \"world\" }");
    }

    [Test]
    public void CanCompareEqualNumbers()
    {
        var result = new Compare<JToken>("$.a", "$.c", "$.result").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("equal"));
    }

    [Test]
    public void CanCompareGreaterNumber()
    {
        var result = new Compare<JToken>("$.b", "$.a", "$.result").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("greater"));
    }

    [Test]
    public void CanCompareLessNumber()
    {
        var result = new Compare<JToken>("$.a", "$.b", "$.result").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("less"));
    }

    [Test]
    public void CanCompareEqualStrings()
    {
        var result = new Compare<JToken>("$.d", "$.e", "$.result").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("equal"));
    }

    [Test]
    public void CanCompareDifferentStrings()
    {
        var result = new Compare<JToken>("$.d", "$.f", "$.result").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("different"));
    }

    [Test]
    public void CanWriteResultToNestedPath()
    {
        var result = new Compare<JToken>("$.a", "$.b", "$.nested.result").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.nested.result")?.Value<string>(), Is.Not.Null);
    }

    [Test]
    public void CanHandleMissingFirstPath()
    {
        var result = new Compare<JToken>("$.missing", "$.a", "$.result").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result"), Is.Null);
    }

    [Test]
    public void CanHandleMissingSecondPath()
    {
        var result = new Compare<JToken>("$.a", "$.missing", "$.result").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result"), Is.Null);
    }

    [Test]
    public void CanExecuteWithMissingPaths()
    {
        var result = new Compare<JToken>("", "", "").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void CanUseScriptApi()
    {
        var script = new TLioScript<JToken>
        {
            new Compare<JToken>("$.a", "$.b", "$.comparison")
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.comparison")?.Type, Is.Not.EqualTo(JTokenType.Null));
    }
}
