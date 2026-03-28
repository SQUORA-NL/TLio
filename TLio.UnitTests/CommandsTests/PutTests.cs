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
/// Ported from JLio.UnitTests.CommandsTests.PutTests.
/// Adaptations:
///   - FunctionSupportedValue(FixedValue(token, converter)) → FixedValue&lt;JToken&gt;(token)
///   - new Put(path, jvalue) → new Put&lt;JToken&gt;(path, new FixedValue&lt;JToken&gt;(jvalue))
///   - fluent JLioScript API → TLioScript&lt;JToken&gt; object list
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class PutTests
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
    public void CanPutValues(string path, string value)
    {
        var valueToSet = new FixedValue<JToken>(new JValue(value));
        var result = new Put<JToken>(path, valueToSet).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [TestCase("$.myObject", "newData")]
    [TestCase("$.myArray", "newData")]
    [TestCase("$..myObject", "newData")]
    [TestCase("$..myArray", "newData")]
    [TestCase("$.myString", "newData")]
    public void CanPutCorrectValues(string path, string value)
    {
        var valueToSet = new FixedValue<JToken>(new JValue(value));
        var result = new Put<JToken>(path, valueToSet).Execute(data, executeOptions);

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
    public void CanPutCorrectValuesEmptyString(string path, string value)
    {
        var valueToSet = new FixedValue<JToken>(new JValue(value));
        var result = new Put<JToken>(path, valueToSet).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).All(i => i.Type != JTokenType.Null), Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.True);
        Assert.That(data.SelectTokens(path).All(t => t.Value<string>() == ""), Is.True);
    }

    [TestCase("$.newProperty", "newData")]
    public void CanPutNewProperty(string path, string value)
    {
        var valueToSet = new FixedValue<JToken>(new JValue(value));
        var result = new Put<JToken>(path, valueToSet).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.True);
        Assert.That(data.SelectTokens(path).All(t => t.Value<string>() == "newData"), Is.True);
    }

    [TestCase("", "newData", "Path property for put command is missing")]
    [TestCase("", null, "Path property for put command is missing")]
    public void CanExecuteWithArgumentsNotProvided(string path, string? value, string message)
    {
        var valueToAdd = new FixedValue<JToken>(new JValue(value));
        var result = new Put<JToken>(path, valueToAdd).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(l => l.Message == message), Is.True);
    }

    // ── Article X: logging assertions ────────────────────────────────────────

    [Test]
    public void Put_Success_LogsInfoEntry()
    {
        var result = new Put<JToken>("$.myString", new FixedValue<JToken>(new JValue("updated"))).Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }

    [Test]
    public void Put_ArrayTarget_NewSyntax_ReplacesArrayContents()
    {
        // New syntax: path selects the array node directly; Put replaces its contents
        var command = new Put<JToken>
        {
            Path = "$.myArray",
            Property = "0",
            Value = new FixedValue<JToken>(new JValue("replaced"))
        };
        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        var arr = data.SelectToken("$.myArray") as JArray;
        Assert.That(arr, Is.Not.Null);
        Assert.That(arr!.Count, Is.EqualTo(1));
        Assert.That(arr[0].Value<string>(), Is.EqualTo("replaced"));
    }

    [Test]
    public void CanUseScriptApi()
    {
        var scriptData = JObject.Parse("{ \"demo\" : \"old value\" , \"demo2\" : \"old value\" }");
        var script = new TLioScript<JToken>
        {
            new Put<JToken>("$.demo", new FixedValue<JToken>(new JValue("new Value"))),
            new Put<JToken>("$.demo2", new FunctionSupportedValue<JToken>(new Datetime<JToken>()))
        };
        var result = script.Execute(scriptData, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.demo")?.Type, Is.Not.EqualTo(JTokenType.Null));
        Assert.That(result.Data.SelectToken("$.demo")?.Value<string>(), Is.EqualTo("new Value"));
        Assert.That(result.Data.SelectToken("$.demo2")?.Type, Is.Not.EqualTo(JTokenType.Null));
    }
}
