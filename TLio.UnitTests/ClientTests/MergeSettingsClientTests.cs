using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.ClientTests;

/// <summary>
/// Script parsing, serialization and fluent-API coverage for the merge settings
/// introduced with issue #30.
/// </summary>
[TestFixture]
public class MergeSettingsClientTests
{
    private ScriptEngine<JToken> _engine = null!;
    private JsonNodeAdapter _adapter = null!;
    private ParseOptions<JToken> _options = null!;

    [SetUp]
    public void SetUp()
    {
        _options = ParseOptions<JToken>.CreateDefault();
        _engine  = new ScriptEngine<JToken>(_options.CommandsProvider, _options.FunctionsProvider);
        _adapter = new JsonNodeAdapter();
    }

    private const string KeyMergeScript = """
        [{
          "command": "merge",
          "path": "$.source",
          "targetPath": "$.target",
          "settings": {
            "strategy": "fullMerge",
            "arraySettings": [
              { "arrayPath": "$.target.items", "keyPaths": ["id"], "uniqueItemsWithoutKeys": false }
            ],
            "matchSettings": { "keyPaths": [] }
          }
        }]
        """;

    [Test]
    public void Parse_ReadsSettingsFromScriptJson()
    {
        var parsed = TLioConvert.Parse(KeyMergeScript, _options, _adapter);
        var merge = (Merge<JToken>)parsed.Single();

        Assert.That(merge.Settings.Strategy, Is.EqualTo(MergeSettings.StrategyFullMerge));
        Assert.That(merge.Settings.ArraySettings.Count, Is.EqualTo(1));
        Assert.That(merge.Settings.ArraySettings[0].ArrayPath, Is.EqualTo("$.target.items"));
        Assert.That(merge.Settings.ArraySettings[0].KeyPaths, Is.EqualTo(new[] { "id" }));
        Assert.That(merge.Settings.ArraySettings[0].UniqueItemsWithoutKeys, Is.False);
    }

    [Test]
    public void Parse_SettingsDriveExecution()
    {
        var input = JToken.Parse("""
        {
          "source": { "items": [ { "id": 1, "v": 2 } ] },
          "target": { "items": [ { "id": 1, "v": 1 } ] }
        }
        """);

        var result = _engine.Execute(KeyMergeScript, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        var items = (JArray)result.Data!.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0]["v"]!.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void Parse_WithoutSettings_UsesDefaults()
    {
        var parsed = TLioConvert.Parse(
            """[{ "command": "merge", "path": "$.a", "targetPath": "$.b" }]""", _options, _adapter);
        var merge = (Merge<JToken>)parsed.Single();

        Assert.That(merge.Settings, Is.Not.Null);
        Assert.That(merge.Settings.IsDefault, Is.True);
    }

    [Test]
    public void Serialize_OmitsDefaultSettings()
    {
        var script = new TLioScript<JToken> { new Merge<JToken>("$.a", "$.b") };

        var json = TLioConvert.Serialize(script, _adapter);

        Assert.That(JArray.Parse(json)[0]["settings"], Is.Null);
    }

    [Test]
    public void Serialize_RoundTripsSettings()
    {
        var settings = new MergeSettings
        {
            Strategy = MergeSettings.StrategyOnlyValues,
            ArraySettings =
            {
                new MergeArraySettings { ArrayPath = "$.target.items", KeyPaths = { "id" } }
            },
            MatchSettings = { KeyPaths = { "id" } }
        };
        var script = new TLioScript<JToken> { new Merge<JToken>("$.a", "$.b", settings) };

        var json = TLioConvert.Serialize(script, _adapter);
        var parsed = (Merge<JToken>)TLioConvert.Parse(json, _options, _adapter).Single();

        Assert.That(parsed.Settings.Strategy, Is.EqualTo(MergeSettings.StrategyOnlyValues));
        Assert.That(parsed.Settings.ArraySettings[0].ArrayPath, Is.EqualTo("$.target.items"));
        Assert.That(parsed.Settings.ArraySettings[0].KeyPaths, Is.EqualTo(new[] { "id" }));
        Assert.That(parsed.Settings.MatchSettings.KeyPaths, Is.EqualTo(new[] { "id" }));
    }

    [Test]
    public void Fluent_WithArrayKeys_MergesByKey()
    {
        var script = new TLioScript<JToken>()
            .Merge().From("$.source").WithArrayKeys("$.target.items", "id").To("$.target");

        var input = JToken.Parse("""
        {
          "source": { "items": [ { "id": 1, "v": 2 } ] },
          "target": { "items": [ { "id": 1, "v": 1 } ] }
        }
        """);

        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        var items = (JArray)result.Data!.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0]["v"]!.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void Fluent_WithStrategy_AppliesStrategy()
    {
        var script = new TLioScript<JToken>()
            .Merge().From("$.source")
            .WithStrategy(MergeSettings.StrategyOnlyStructure)
            .To("$.target");

        var input = JToken.Parse("""
        { "source": { "a": "from-source", "c": "new" }, "target": { "a": "keep-me" } }
        """);

        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Data!.SelectToken("$.target.a")!.Value<string>(), Is.EqualTo("keep-me"));
        Assert.That(result.Data!.SelectToken("$.target.c")!.Value<string>(), Is.EqualTo("new"));
    }

    [Test]
    public void Fluent_WithArrayMergeMode_Replaces()
    {
        var script = new TLioScript<JToken>()
            .Merge().From("$.source")
            .WithArrayMergeMode(Core.Contracts.ArrayMergeMode.Replace)
            .To("$.target");

        var input = JToken.Parse("""{ "source": { "list": [3] }, "target": { "list": [1, 2] } }""");
        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        var list = (JArray)result.Data!.SelectToken("$.target.list")!;
        Assert.That(list.Select(t => t.Value<int>()), Is.EqualTo(new[] { 3 }));
    }
}
