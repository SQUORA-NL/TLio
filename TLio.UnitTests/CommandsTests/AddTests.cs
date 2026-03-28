using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.AddTests.
/// Adaptations:
///   - FunctionSupportedValue(FixedValue(token, converter)) → FixedValue&lt;JToken&gt;(token)
///   - new Add(path, jvalue) → new Add&lt;JToken&gt;(path, new FixedValue&lt;JToken&gt;(jvalue))
///   - fluent JLioScript API → TLioScript&lt;JToken&gt; object list
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///   - CanAddCorrectValuesAsFunctions skipped (nested =func() in FixedValue not yet implemented)
/// </summary>
[TestFixture]
public class AddTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{\r\n  \"myString\": \"demo2\",\r\n  \"myNumber\": 2.2,\r\n  \"myInteger\": 20,\r\n  \"myObject\": {\r\n    \"myObject\": {\"myArray\": [\r\n      2,\r\n      20,\r\n      200,\r\n      2000\r\n    ]},\r\n    \"myArray\": [\r\n      2,\r\n      20,\r\n      200,\r\n      2000\r\n    ]\r\n  },\r\n  \"myArray\": [\r\n    2,\r\n    20,\r\n    200,\r\n    2000\r\n  ],\r\n  \"myBoolean\": true,\r\n  \"myNull\": null\r\n}");
    }

    [TestCase("$.myObject.newItem", "newData")]
    [TestCase("$.NewObject.newItem.NewSubItem", "newData")]
    [TestCase("$.myArray", "newData")]
    [TestCase("$.myNull", "newData")]
    [TestCase("$..myObject.newItem", "newData")]
    [TestCase("$..myArray", "newData")]
    [TestCase("$.newProperty", "newData")]
    [TestCase("$..myObject[?(@.myArray)].newProperty)", "newData")]
    public void CanAddValues(string path, string value)
    {
        var valueToAdd = new FixedValue<JToken>(new JValue(value));
        var result = new Add<JToken>(path, valueToAdd).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [TestCase("$.newNumber", "01")]
    [TestCase("$.newNumber", 1)]
    [TestCase("$.myObject.newItem", "newData")]
    [TestCase("$.NewObject.newItem.NewSubItem", "newData")]
    [TestCase("$.myArray", "newData")]
    [TestCase("$..myObject.newItem", "newData")]
    [TestCase("$..myArray", "newData")]
    [TestCase("$.newProperty", "newData")]
    public void CanAddCorrectValues(string path, object value)
    {
        var valueToAdd = new FixedValue<JToken>(new JValue(value));
        var result = new Add<JToken>(path, valueToAdd).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).All(i => i.Type != JTokenType.Null), Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.True);
    }

    [TestCase("$.NewObject.newItem.NewSubItem", "newData")]
    public void CanAddCorrectValuesWithOtherConstructor(string path, string value)
    {
        // JLio: new Add(path, new JValue(value)) — TLio uses FixedValue wrapper
        var result = new Add<JToken>(path, new FixedValue<JToken>(new JValue(value))).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).All(i => i.Type != JTokenType.Null), Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.True);
    }

    [TestCase("$.myObject.newItem", "newData")]
    public void CanAddCorrectValuesWithEmptyConstructor(string path, string value)
    {
        var command = new Add<JToken>
        { Path = path, Value = new FixedValue<JToken>(new JValue(value)) };

        var result = command.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).All(i => i.Type != JTokenType.Null), Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.True);
    }

    [TestCase("$.newProperty", "{\"demo\" : 3}")]
    public void CanAddCorrectValuesAsTokens(string path, string value)
    {
        var tokenToAdd = JToken.Parse(value);
        var valueToAdd = new FixedValue<JToken>(tokenToAdd);
        var result = new Add<JToken>(path, valueToAdd).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).All(i => i.Type != JTokenType.Null), Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken(path), tokenToAdd), Is.True);
    }

    [TestCase("", "newData", "Path property for add command is missing")]
    [TestCase("", null, "Path property for add command is missing")]
    public void CanExecuteWithArgumentsNotProvided(string path, string? value, string message)
    {
        var valueToAdd = new FixedValue<JToken>(new JValue(value));
        var result = new Add<JToken>(path, valueToAdd).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(l => l.Message == message), Is.True);
    }

    // ── Article X: logging assertions ────────────────────────────────────────

    [Test]
    public void Add_Success_LogsInfoEntry()
    {
        var result = new Add<JToken>("$.newProp", new FixedValue<JToken>(new JValue("x"))).Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }

    [Test]
    public void Add_DuplicateProperty_LogsWarning()
    {
        // "$.myString" already exists — Add should log a warning and skip
        var result = new Add<JToken>("$.myString", new FixedValue<JToken>(new JValue("newValue"))).Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e => e.Level == LogLevel.Warning && e.Message.Contains("already exists")), Is.True);
    }

    // CanAddCorrectValuesAsFunctions skipped:
    // Requires nested =func() expansion inside FixedValue<JToken> (tasks.md 4B)

    [Test]
    public void CanUseScriptApi()
    {
        // Adapted from JLio's CanUseFluentApi — uses TLioScript<JToken> directly
        var emptyData = new JObject();
        var script = new TLioScript<JToken>
        {
            new Add<JToken>("$.demo", new FixedValue<JToken>(new JValue("new Value"))),
            new Add<JToken>("$.this.is.a.long.path.with.a.date",
                new FunctionSupportedValue<JToken>(new Datetime<JToken>()))
        };
        var result = script.Execute(emptyData, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.demo")?.Type, Is.Not.EqualTo(JTokenType.Null));
        Assert.That(result.Data.SelectToken("$.this.is.a.long.path.with.a.date")?.Type, Is.Not.EqualTo(JTokenType.Null));
    }
}
