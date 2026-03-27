using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.CopyMoveTests.
/// Adaptations:
///   - ExecutionContext.CreateDefault() → JsonExecutionContext.CreateDefault()
///   - new Copy/Move(from, to) → new Copy/Move&lt;JToken&gt;(from, to)
///   - JLioScript fluent API → TLioScript&lt;JToken&gt; collection initialiser
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// NOTE (constitution §VI): These tests use inline data. A follow-up task will
/// refactor all command/function tests to use file-based fixture triplets
/// (input.json / script.json / result.json) per the plan's xUnit Theory requirement.
/// </summary>
[TestFixture]
public class CopyMoveTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"myString\": \"demo2\", \"myNumber\": 2.2, \"myInteger\": 20, \"myObject\": { \"myObject\": {\"myArray\": [ 2, 20, 200, 2000 ]}, \"myArray\": [ 2, 20, 200, 2000 ] }, \"myArray\": [ 2, 20, 200, 2000 ], \"myBoolean\": true, \"myNull\": null}");
    }

    [TestCase("$.myString", "$.myNewObject.newItem", "'demo2'")]
    [TestCase("$.myNumber", "$.myNewObject.newItem", "2.2")]
    [TestCase("$.myInteger", "$.myNewObject.newItem", "20")]
    [TestCase("$.myObject", "$.myNewObject.newItem",
        "{ \"myObject\": { \"myArray\": [ 2, 20, 200, 2000 ] }, \"myArray\": [ 2, 20, 200, 2000 ] }")]
    [TestCase("$.myArray", "$.myNewObject.newItem", "[ 2, 20, 200, 2000 ]")]
    [TestCase("$.myBoolean", "$.myNewObject.newItem", "true")]
    [TestCase("$.myNull", "$.myNewObject.newItem", "null")]
    [TestCase("$.myString", "$.myObject", "'demo2'")]
    [TestCase("$.myNumber", "$.myObject", "2.2")]
    [TestCase("$.myInteger", "$.myObject", "20")]
    [TestCase("$.myObject", "$.myObject",
        "{ \"myObject\": { \"myArray\": [ 2, 20, 200, 2000 ] }, \"myArray\": [ 2, 20, 200, 2000 ] }")]
    [TestCase("$.myArray", "$.myObject", "[ 2, 20, 200, 2000 ]")]
    [TestCase("$.myBoolean", "$.myObject", "true")]
    [TestCase("$.myNull", "$.myObject", "null")]
    public void PropertyCopyTests(string from, string to, string expectedValueToPath)
    {
        var result = new Copy<JToken>(from, to).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(JToken.Parse(expectedValueToPath), data.SelectToken(to)), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken(from), data.SelectToken(to)), Is.True);
    }

    [TestCase("$.myString", "$.myArray", "[ 2, 20, 200, 2000, 'demo2' ]")]
    [TestCase("$.myNumber", "$.myArray", "[ 2, 20, 200, 2000, 2.2]")]
    [TestCase("$.myInteger", "$.myArray", "[ 2, 20, 200, 2000, 20]")]
    [TestCase("$.myObject", "$.myArray",
        "[ 2, 20, 200, 2000, { \"myObject\": { \"myArray\": [ 2, 20, 200, 2000 ] }, \"myArray\": [ 2, 20, 200, 2000 ] }]")]
    [TestCase("$.myArray[*]", "$.myArray", "[ 2, 20, 200, 2000 ,2, 20, 200, 2000 ]")]
    [TestCase("$.myArray[?(@ > 20)]", "$.myArray", "[ 2, 20, 200, 2000 , 200, 2000 ]")]
    [TestCase("$.myArray", "$.myArray", "[ 2, 20, 200, 2000 ,[2, 20, 200, 2000] ]")]
    [TestCase("$.myBoolean", "$.myArray", "[ 2, 20, 200, 2000, true]")]
    [TestCase("$.myNull", "$.myArray", "[ 2, 20, 200, 2000, null]")]
    public void PropertyCopyArrayTests(string from, string to, string expectedValueToPath)
    {
        var result = new Copy<JToken>(from, to).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(JToken.Parse(expectedValueToPath), data.SelectToken(to)), Is.True);
    }

    [TestCase("$.myString", "$.myArray", "[ 2, 20, 200, 2000, 'demo2' ]")]
    [TestCase("$.myNumber", "$.myArray", "[ 2, 20, 200, 2000, 2.2]")]
    [TestCase("$.myInteger", "$.myArray", "[ 2, 20, 200, 2000, 20]")]
    [TestCase("$.myObject", "$.myArray",
        "[ 2, 20, 200, 2000, { \"myObject\": { \"myArray\": [ 2, 20, 200, 2000 ] }, \"myArray\": [ 2, 20, 200, 2000 ] }]")]
    [TestCase("$.myArray[*]", "$.myArray", "[ 2, 20, 200, 2000 ]")]
    [TestCase("$.myObject.myArray[?(@ > 20)]", "$.myArray", "[ 2, 20, 200, 2000 , 200, 2000 ]")]
    [TestCase("$.myBoolean", "$.myArray", "[ 2, 20, 200, 2000, true]")]
    [TestCase("$.myNull", "$.myArray", "[ 2, 20, 200, 2000, null]")]
    public void PropertyMoveArrayTests(string from, string to, string expectedValueToPath)
    {
        var result = new Move<JToken>(from, to).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(JToken.Parse(expectedValueToPath), data.SelectToken(to)), Is.True);
    }

    [TestCase("$.myObject", "$",
        "{ \"myObject\": {\"myArray\": [ 2, 20, 200, 2000 ]}, \"myArray\": [ 2, 20, 200, 2000 ] }")]
    public void PropertyMoveToRootTests(string from, string to, string expectedValueToPath)
    {
        var result = new Move<JToken>(from, to).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(JToken.Parse(expectedValueToPath), data.SelectToken(to)), Is.True);
    }

    [TestCase("$.myString", "$.mystring", "'demo2'")]
    [TestCase("$.myString", "$.myNewObject.newItem", "'demo2'")]
    [TestCase("$.myNumber", "$.myNewObject.newItem", "2.2")]
    [TestCase("$.myInteger", "$.myNewObject.newItem", "20")]
    [TestCase("$.myObject", "$.myNewObject.newItem",
        "{ \"myObject\": { \"myArray\": [ 2, 20, 200, 2000 ] }, \"myArray\": [ 2, 20, 200, 2000 ] }")]
    [TestCase("$.myArray", "$.myNewObject.newItem", "[ 2, 20, 200, 2000 ]")]
    [TestCase("$.myBoolean", "$.myNewObject.newItem", "true")]
    [TestCase("$.myNull", "$.myNewObject.newItem", "null")]
    [TestCase("$.myString", "$.myObject", "'demo2'")]
    [TestCase("$.myNumber", "$.myObject", "2.2")]
    [TestCase("$.myInteger", "$.myObject", "20")]
    [TestCase("$.myObject", "$.myObject",
        "{ \"myObject\": { \"myArray\": [ 2, 20, 200, 2000 ] }, \"myArray\": [ 2, 20, 200, 2000 ] }")]
    [TestCase("$.myArray", "$.myObject", "[ 2, 20, 200, 2000 ]")]
    [TestCase("$.myBoolean", "$.myObject", "true")]
    [TestCase("$.myNull", "$.myObject", "null")]
    public void PropertyMoveTests(string from, string to, string expectedValueToPath)
    {
        var result = new Move<JToken>(from, to).Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(JToken.Parse(expectedValueToPath), data.SelectToken(to)), Is.True);
        Assert.That(from == to || data.SelectToken(from) == null, Is.True);
    }

    [TestCase("{\"myData\":[{\"demo\":[{\"demo2\":3}]},{\"demo\":[{\"demo2\":4}]},{\"demo\":[{\"demo2\":5}]}]}",
              "$.myData[*].demo[*].demo2",
              "$.myData[*].demo[*].new",
              "{\"myData\":[{\"demo\":[{\"new\":3}]},{\"demo\":[{\"new\":4}]},{\"demo\":[{\"new\":5}]}]}")]
    [TestCase("{\"myData\":[{\"demo\":[{\"demo2\":{\"oldProperty\":1}}]},{\"demo\":[{\"demo2\":{\"oldProperty\":2}}]},{\"demo\":[{\"demo2\":{\"oldProperty\":3}}]}]}",
              "$.myData[*].demo[*].demo2.oldProperty",
              "$.myData[*].demo[*].new",
              "{\"myData\":[{\"demo\":[{\"demo2\":{},\"new\":1}]},{\"demo\":[{\"demo2\":{},\"new\":2}]},{\"demo\":[{\"demo2\":{},\"new\":3}]}]}")]
    [TestCase("{\"myData\":[{\"demo\":[{\"demo2\":{\"oldProperty\":1},\"newProperty\":[{}]}]},{\"demo\":[{\"demo2\":{\"oldProperty\":2},\"newProperty\":[{}]}]},{\"demo\":[{\"demo2\":{\"oldProperty\":3},\"newProperty\":[{}]}]}]}",
              "$.myData[*].demo[*].demo2.oldProperty",
              "$.myData[*].demo[*].newProperty[*].test",
              "{\"myData\":[{\"demo\":[{\"demo2\":{},\"newProperty\":[{\"test\":1}]}]},{\"demo\":[{\"demo2\":{},\"newProperty\":[{\"test\":2}]}]},{\"demo\":[{\"demo2\":{},\"newProperty\":[{\"test\":3}]}]}]}")]
    public void CanCopyMovePropertiesInAnLayeredArray(string startobject, string moveFrom, string moveTo, string expectedValue)
    {
        var result = new Move<JToken>(moveFrom, moveTo).Execute(JToken.Parse(startobject), executeOptions);
        Assert.That(JToken.DeepEquals(result.Data, JToken.Parse(expectedValue)), Is.True);
    }

    [TestCase(
       "{\"firstArray\":[{\"target\":[],\"secondArray\":[{\"id\":\"item1\",\"sub\":{\"name\":\"item 1\"}},{\"id\":\"item2\",\"sub\":{\"name\":\"item 2\"}}]},{\"target\":[],\"secondArray\":[{\"id\":\"item3\",\"sub\":{\"name\":\"item 3\"}},{\"id\":\"item4\",\"sub\":{\"name\":\"item 4\"}}]}]}",
       "$.firstArray[*].secondArray[*].sub",
       "$.firstArray[*].target",
       "{\"firstArray\":[{\"target\":[{\"name\":\"item 1\"},{\"name\":\"item 2\"}],\"secondArray\":[{\"id\":\"item1\",\"sub\":{\"name\":\"item 1\"}},{\"id\":\"item2\",\"sub\":{\"name\":\"item 2\"}}]},{\"target\":[{\"name\":\"item 3\"},{\"name\":\"item 4\"}],\"secondArray\":[{\"id\":\"item3\",\"sub\":{\"name\":\"item 3\"}},{\"id\":\"item4\",\"sub\":{\"name\":\"item 4\"}}]}]}"
       )]
    public void CanCopyPropertiesInAnLayeredArray(string startobject, string copyFrom, string copyTo, string expectedValue)
    {
        var result = new Copy<JToken>(copyFrom, copyTo).Execute(JToken.Parse(startobject), executeOptions);
        Assert.That(JToken.DeepEquals(result.Data, JToken.Parse(expectedValue)), Is.True);
    }

    [Test]
    public void CanCopyMovePropertiesInAnArray()
    {
        var startObject = "{\"myData\":[{\"demo\":[{\"old\":3}]},{\"demo\":[{\"old\":4}]},{\"demo\":[{\"old\":5}]}]}";
        var result = new Move<JToken>("$.myData[*].demo", "$.myData[*].new").Execute(
            JToken.Parse(startObject), executeOptions);
        Assert.That(JToken.DeepEquals(result.Data,
            JToken.Parse("{\"myData\":[{\"new\":[{\"old\":3}]},{\"new\":[{\"old\":4}]},{\"new\":[{\"old\":5}]}]}")),
            Is.True);
    }

    [Test]
    public void CanCopyMovePropertiesInAnArrayCaseSensitive()
    {
        var startObject = "{\"myData\":[{\"demo\":[{\"old\":3}]},{\"demo\":[{\"old\":4}]},{\"demo\":[{\"old\":5}]}]}";
        var result = new Move<JToken>("$.myData[*].demo", "$.myData[*].Demo").Execute(
            JToken.Parse(startObject), executeOptions);
        Assert.That(JToken.DeepEquals(result.Data,
            JToken.Parse("{\"myData\":[{\"Demo\":[{\"old\":3}]},{\"Demo\":[{\"old\":4}]},{\"Demo\":[{\"old\":5}]}]}")),
            Is.True);
    }

    [Test]
    public void CanUseFluentApi()
    {
        var testData = JObject.Parse("{ \"demo\" : \"item\" }");
        var script = new TLioScript<JToken>
        {
            new Copy<JToken>("$.demo", "$.copiedDemo"),
            new Move<JToken>("$.copiedDemo", "$.result")
        };
        var result = script.Execute(testData, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.SelectToken("$.demo"), result.Data.SelectToken("$.result")), Is.True);
        Assert.That(result.Data.SelectToken("$.copiedDemo"), Is.Null);
    }

    [Test]
    public void CanExecuteCopyWithoutParametersSet()
    {
        var command = new Copy<JToken>();
        var result = command.Execute(JToken.Parse("{\"first\":true,\"second\":true}"), executeOptions);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(), Is.True);
    }

    [Test]
    public void CanExecuteMoveWithoutParametersSet()
    {
        var command = new Move<JToken>();
        var result = command.Execute(JToken.Parse("{\"first\":true,\"second\":true}"), executeOptions);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(), Is.True);
    }
}
