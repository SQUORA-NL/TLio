using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Extensions.ETL.Commands;
using TLio.Extensions.ETL.Commands.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.ETLTests;

/// <summary>
/// Tests for Flatten, Restore, and ToCsv commands.
/// Ported from JLio.UnitTests.CommandsTests.ETLTests.FlattenRestoreTests.
/// Uses direct command instantiation (CommandConverter does not auto-deserialize complex ETL settings).
/// </summary>
[TestFixture]
public class FlattenRestoreTests
{
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
    }

    // ── Flatten validation ────────────────────────────────────────────────────

    [Test]
    public void Flatten_Validation_EmptyPath_Fails()
    {
        var data = JToken.Parse(@"{ ""item"": { ""x"": 1 } }");
        var cmd = new Flatten<JToken> { Path = "" };
        Assert.That(cmd.Execute(data, context).Success, Is.False);
    }

    [Test]
    public void Flatten_Validation_MaxDepthZero_Fails()
    {
        var data = JToken.Parse(@"{ ""item"": { ""x"": 1 } }");
        var cmd = new Flatten<JToken>
        {
            Path = "$.item",
            FlattenSettings = new FlattenSettings { MaxDepth = 0 }
        };
        Assert.That(cmd.Execute(data, context).Success, Is.False);
    }

    // ── Flatten behaviour ─────────────────────────────────────────────────────

    [Test]
    public void Flatten_NestedObject_ProducesDelimitedKeys()
    {
        var data = JToken.Parse(@"{ ""item"": { ""name"": ""test"", ""nested"": { ""x"": 1 } } }");
        var cmd = new Flatten<JToken> { Path = "$.item" };

        Assert.That(cmd.Execute(data, context).Success, Is.True);

        var flat = data["item"] as JObject;
        Assert.That(flat, Is.Not.Null);
        Assert.That(flat!["name"]?.Value<string>(), Is.EqualTo("test"));
        Assert.That(flat["nested.x"]?.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void Flatten_PreservesTypes_AddsTypeKeys()
    {
        var data = JToken.Parse(@"{ ""item"": { ""count"": 5 } }");
        var cmd = new Flatten<JToken>
        {
            Path = "$.item",
            FlattenSettings = new FlattenSettings { PreserveTypes = true }
        };

        Assert.That(cmd.Execute(data, context).Success, Is.True);

        var flat = data["item"] as JObject;
        Assert.That(flat!["count_type"]?.Value<string>(), Is.EqualTo("Integer"));
    }

    // ── Flatten + Restore roundtrip ───────────────────────────────────────────

    [Test]
    public void Flatten_ThenRestore_RoundTrip()
    {
        var data = JToken.Parse(@"{
            ""item"": {
                ""name"": ""test"",
                ""value"": 42,
                ""nested"": { ""x"": 1 }
            }
        }");

        var flattenCmd = new Flatten<JToken> { Path = "$.item" };
        Assert.That(flattenCmd.Execute(data, context).Success, Is.True);
        Assert.That(data["_flattenMetadata"], Is.Not.Null);

        var restoreCmd = new Restore<JToken>
        {
            Path = "$.item",
            RestoreSettings = new RestoreSettings { MetadataPath = "$", RemoveMetadata = true }
        };
        Assert.That(restoreCmd.Execute(data, context).Success, Is.True);

        var item = data["item"] as JObject;
        Assert.That(item, Is.Not.Null);
        Assert.That(item!["name"]?.Value<string>(), Is.EqualTo("test"));
        Assert.That(item["value"]?.Value<int>(), Is.EqualTo(42));
        Assert.That(item["nested"]?["x"]?.Value<int>(), Is.EqualTo(1));
        Assert.That(data["_flattenMetadata"], Is.Null);
    }

    // ── ToCsv behaviour ───────────────────────────────────────────────────────

    [Test]
    public void ToCsv_Validation_EmptyPath_Fails()
    {
        var data = JToken.Parse(@"{ ""table"": [] }");
        var cmd = new ToCsv<JToken> { Path = "" };
        Assert.That(cmd.Execute(data, context).Success, Is.False);
    }

    [Test]
    public void ToCsv_ArrayOfObjects_ProducesAlphabeticHeaders()
    {
        var data = JToken.Parse(@"{
            ""table"": [
                { ""name"": ""Alice"", ""age"": 30 },
                { ""name"": ""Bob"",   ""age"": 25 }
            ]
        }");
        var cmd = new ToCsv<JToken> { Path = "$.table" };

        Assert.That(cmd.Execute(data, context).Success, Is.True);

        var csv = data["table"]?.Value<string>();
        Assert.That(csv, Is.Not.Null.And.Not.Empty);

        var lines = csv!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines[0], Is.EqualTo("age,name"));
        Assert.That(lines[1], Does.Contain("30").And.Contain("Alice"));
        Assert.That(lines[2], Does.Contain("25").And.Contain("Bob"));
    }

    [Test]
    public void ToCsv_SingleObject_ProducesSingleDataRow()
    {
        var data = JToken.Parse(@"{
            ""row"": { ""city"": ""Paris"", ""country"": ""France"" }
        }");
        var cmd = new ToCsv<JToken> { Path = "$.row" };

        Assert.That(cmd.Execute(data, context).Success, Is.True);

        var csv = data["row"]?.Value<string>();
        var lines = csv!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.EqualTo(2));
        Assert.That(lines[0], Is.EqualTo("city,country"));
        Assert.That(lines[1], Does.Contain("Paris").And.Contain("France"));
    }
}
