using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Client;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Rename over JSON. The name of a JSON value belongs to the property that holds it, so
/// renaming rewrites that property — and the root, having no property, cannot be renamed.
/// See TLio.Xml.Tests for the XML side, where elements carry their own name and even the
/// document element renames.
/// </summary>
[TestFixture]
public class RenameTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse("""
            {
              "first": "Ada",
              "address": { "town": "Amsterdam", "zip": "1011" },
              "items": [ { "town": "Delft" } ]
            }
            """);
    }

    [Test]
    public void Rename_Property_ChangesTheKeyAndKeepsTheValue()
    {
        var result = new Rename<JToken>("$.first", "given").Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(data["given"]?.Value<string>(), Is.EqualTo("Ada"));
        Assert.That(data["first"], Is.Null);
    }

    [Test]
    public void Rename_Property_KeepsKeyOrder()
    {
        new Rename<JToken>("$.address.town", "city").Execute(data, executeOptions);

        var address = (JObject)data["address"]!;
        Assert.That(address.Properties().Select(p => p.Name), Is.EqualTo(new[] { "city", "zip" }),
            "renaming rewrites the property in place rather than appending it at the end");
    }

    [Test]
    public void Rename_NestedObject_KeepsItsChildren()
    {
        new Rename<JToken>("$.address", "location").Execute(data, executeOptions);

        Assert.That(data["location"]?["town"]?.Value<string>(), Is.EqualTo("Amsterdam"));
        Assert.That(data["address"], Is.Null);
    }

    [Test]
    public void Rename_RecursiveDescent_RenamesEveryMatch()
    {
        var result = new Rename<JToken>("$..town", "city").Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(data["address"]?["city"]?.Value<string>(), Is.EqualTo("Amsterdam"));
        Assert.That(data["items"]?[0]?["city"]?.Value<string>(), Is.EqualTo("Delft"));
    }

    [Test]
    public void Rename_MissingPath_WarnsAndContinues()
    {
        var before = data.DeepClone();

        var result = new Rename<JToken>("$.does.not.exist", "whatever").Execute(data, executeOptions);

        Assert.That(result.Success, Is.True, "a missing path is a no-op, not a failure");
        Assert.That(JToken.DeepEquals(data, before), Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no nodes matched")), Is.True);
    }

    [Test]
    public void Rename_JsonRoot_WarnsBecauseItHasNoNameOfItsOwn()
    {
        var before = data.DeepClone();

        var result = new Rename<JToken>("$", "opdracht").Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data, before), Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("no name to change")), Is.True,
            "a JSON root is named by nothing — unlike an XML document element");
    }

    [Test]
    public void Rename_ArrayElement_WarnsBecauseItIsNamedByItsPosition()
    {
        var result = new Rename<JToken>("$.items[0]", "first").Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no name to change")), Is.True);
    }

    [TestCase(null, "Path property for rename command is missing")]
    [TestCase("", "Path property for rename command is missing")]
    public void Rename_WithoutPath_Fails(string? path, string message)
    {
        var result = new Rename<JToken> { Path = path, Name = "x" }.Execute(data, executeOptions);

        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(l => l.Message == message), Is.True);
    }

    [Test]
    public void Rename_WithoutName_Fails()
    {
        var result = new Rename<JToken> { Path = "$.first" }.Execute(data, executeOptions);

        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(l =>
            l.Message == "Name property for rename command is missing"), Is.True);
    }

    [Test]
    public void CanUseFluentApi()
    {
        var script = new TLioScript<JToken>()
            .Rename("given").OnPath("$.first")
            .Rename("location").OnPath("$.address");

        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.given")?.Value<string>(), Is.EqualTo("Ada"));
        Assert.That(result.Data.SelectToken("$.location.town")?.Value<string>(), Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void CanUseScriptApi()
    {
        var script = new TLioScript<JToken>
        {
            new Rename<JToken>("$.first", "given"),
            new Rename<JToken>("$.address", "location")
        };

        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.given")?.Value<string>(), Is.EqualTo("Ada"));
        Assert.That(result.Data.SelectToken("$.location.town")?.Value<string>(), Is.EqualTo("Amsterdam"));
    }
}
