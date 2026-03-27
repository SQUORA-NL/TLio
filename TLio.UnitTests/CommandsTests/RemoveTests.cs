using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.RemoveTests.
/// Adaptations:
///   - fluent JLioScript API → TLioScript&lt;JToken&gt; object list
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class RemoveTests
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

    [TestCase("$.myString")]
    [TestCase("$.myNumber")]
    [TestCase("$.myInteger")]
    [TestCase("$.myBoolean")]
    [TestCase("$.myNull")]
    [TestCase("$.myObject")]
    [TestCase("$.myArray")]
    [TestCase("$..myArray")]
    [TestCase("$..myObject")]
    public void CanRemoveValues(string path)
    {
        var result = new Remove<JToken>(path).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [TestCase("$.myString")]
    [TestCase("$.myNumber")]
    [TestCase("$.myObject")]
    [TestCase("$.myArray")]
    public void CanRemoveCorrectValues(string path)
    {
        var result = new Remove<JToken>(path).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.False);
    }

    [TestCase("$..myArray")]
    [TestCase("$..myObject")]
    public void CanRemoveRecursiveValues(string path)
    {
        var result = new Remove<JToken>(path).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens(path).Any(), Is.False);
    }

    [TestCase("", "Path property for remove command is missing")]
    public void CanExecuteWithArgumentsNotProvided(string path, string message)
    {
        var result = new Remove<JToken>(path).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(l => l.Message == message), Is.True);
    }

    [Test]
    public void CanUseScriptApi()
    {
        var scriptData = JObject.Parse("{ \"demo\" : \"old value\", \"demo2\" : \"old value\" }");
        var script = new TLioScript<JToken>
        {
            new Remove<JToken>("$.demo"),
            new Remove<JToken>("$.demo2")
        };
        var result = script.Execute(scriptData, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.demo"), Is.Null);
        Assert.That(result.Data.SelectToken("$.demo2"), Is.Null);
    }
}
