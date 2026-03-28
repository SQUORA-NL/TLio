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
/// Ported from JLio.UnitTests.CommandsTests.SetTests.
/// Adaptations:
///   - FunctionSupportedValue(FixedValue(token, converter)) → FixedValue&lt;JToken&gt;(token)
///   - fluent JLioScript API → TLioScript&lt;JToken&gt; object list
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class SetTests
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

    [TestCase("$.myObject.myArray", "newData")]
    [TestCase("$.NewObject.newItem.NewSubItem", "newData")]
    [TestCase("$.myArray", "newData")]
    [TestCase("$.myNull", "newData")]
    [TestCase("$..myArray", "newData")]
    [TestCase("$.myString", "newData")]
    public void CanSetValues(string path, string value)
    {
        var valueToSet = new FixedValue<JToken>(new JValue(value));
        var result = new Set<JToken>(path, valueToSet).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [TestCase("$.myObject", "newData")]
    [TestCase("$.myArray", "newData")]
    [TestCase("$..myObject", "newData")]
    [TestCase("$..myArray", "newData")]
    [TestCase("$.myString", "newData")]
    public void CanSetCorrectValues(string path, string value)
    {
        var valueToSet = new FixedValue<JToken>(new JValue(value));
        var result = new Set<JToken>(path, valueToSet).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).All(i => i.Type != JTokenType.Null), Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.True);
        Assert.That(data.SelectTokens(path).All(t => t.Value<string>() == "newData"), Is.True);
    }

    [TestCase("$.myObject", "")]
    [TestCase("$.myArray", "")]
    [TestCase("$..myObject", "")]
    [TestCase("$..myArray", "")]
    [TestCase("$.myString", "")]
    public void CanSetCorrectValuesEmptyString(string path, string value)
    {
        var valueToSet = new FixedValue<JToken>(new JValue(value));
        var result = new Set<JToken>(path, valueToSet).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).All(i => i.Type != JTokenType.Null), Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.True);
        Assert.That(data.SelectTokens(path).All(t => t.Value<string>() == ""), Is.True);
    }

    [TestCase("", "newData", "Path property for set command is missing")]
    [TestCase("", null, "Path property for set command is missing")]
    public void CanExecuteWithArgumentsNotProvided(string path, string? value, string message)
    {
        var valueToAdd = new FixedValue<JToken>(new JValue(value));
        var result = new Set<JToken>(path, valueToAdd).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(l => l.Message == message), Is.True);
    }

    // ── Article X: logging assertions ────────────────────────────────────────

    [Test]
    public void Set_Success_LogsInfoEntry()
    {
        var result = new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("updated"))).Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }

    [Test]
    public void Set_PropertyNotFound_LogsWarning()
    {
        // New syntax: Property set but path selects nodes; property name not found on object → warning
        var command = new Set<JToken>
        {
            Path = "$.myObject",
            Property = "nonExistentProp",
            Value = new FixedValue<JToken>(new JValue("x"))
        };
        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e => e.Level == LogLevel.Warning && e.Message.Contains("not found")), Is.True);
    }

    [Test]
    public void CanUseScriptApi()
    {
        // Adapted from JLio's CanUseFluentApi — uses TLioScript<JToken> directly
        var scriptData = JObject.Parse("{ \"demo\" : \"old value\" , \"demo2\" : \"old value\" }");
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.demo", new FixedValue<JToken>(new JValue("new Value"))),
            new Set<JToken>("$.demo2", new FunctionSupportedValue<JToken>(new Datetime<JToken>()))
        };
        var result = script.Execute(scriptData, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.demo")?.Type, Is.Not.EqualTo(JTokenType.Null));
        Assert.That(result.Data.SelectToken("$.demo")?.Value<string>(), Is.EqualTo("new Value"));
        Assert.That(result.Data.SelectToken("$.demo2")?.Type, Is.Not.EqualTo(JTokenType.Null));
    }
}
