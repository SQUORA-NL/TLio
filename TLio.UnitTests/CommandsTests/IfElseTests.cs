using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.IfElseTests.
/// Adaptations:
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///   - fluent JLioScript API → TLioScript&lt;JToken&gt; object list
/// </summary>
[TestFixture]
public class IfElseTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{\r\n  \"myString\": \"demo2\",\r\n  \"myNumber\": 2.2,\r\n  \"myInteger\": 20,\r\n  \"myBoolean\": true,\r\n  \"myNull\": null\r\n}");
    }

    [Test]
    public void CanExecuteIfBranchOnTrue()
    {
        var script = new TLioScript<JToken>
        {
            new IfElse<JToken>(
                new FixedValue<JToken>(new JValue(true)),
                new TLioScript<JToken> { new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("if-branch"))) },
                new TLioScript<JToken> { new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("else-branch"))) })
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.myString")?.Value<string>(), Is.EqualTo("if-branch"));
    }

    [Test]
    public void CanExecuteElseBranchOnFalse()
    {
        var script = new TLioScript<JToken>
        {
            new IfElse<JToken>(
                new FixedValue<JToken>(new JValue(false)),
                new TLioScript<JToken> { new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("if-branch"))) },
                new TLioScript<JToken> { new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("else-branch"))) })
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.myString")?.Value<string>(), Is.EqualTo("else-branch"));
    }

    [Test]
    public void CanExecuteIfBranchOnTrueString()
    {
        var script = new TLioScript<JToken>
        {
            new IfElse<JToken>(
                new FixedValue<JToken>(new JValue("true")),
                new TLioScript<JToken> { new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("if-branch"))) },
                new TLioScript<JToken> { new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("else-branch"))) })
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.myString")?.Value<string>(), Is.EqualTo("if-branch"));
    }

    [Test]
    public void CanExecuteWithNullElseScript()
    {
        var script = new TLioScript<JToken>
        {
            new IfElse<JToken>(
                new FixedValue<JToken>(new JValue(false)),
                new TLioScript<JToken> { new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("if-branch"))) },
                null)
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        // original value unchanged since else is null
        Assert.That(result.Data.SelectToken("$.myString")?.Value<string>(), Is.EqualTo("demo2"));
    }

    [Test]
    public void CanExecuteWithNullIfScript()
    {
        var script = new TLioScript<JToken>
        {
            new IfElse<JToken>(
                new FixedValue<JToken>(new JValue(true)),
                null,
                new TLioScript<JToken> { new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("else-branch"))) })
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        // original value unchanged since if is null
        Assert.That(result.Data.SelectToken("$.myString")?.Value<string>(), Is.EqualTo("demo2"));
    }

    [Test]
    public void CanExecuteWithMissingCondition()
    {
        var command = new IfElse<JToken>();
        var result = command.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
    }
}
