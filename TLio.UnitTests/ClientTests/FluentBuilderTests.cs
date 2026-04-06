using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.ClientTests;

/// <summary>
/// Inline tests for the fluent builder API (TLioScriptExtensions) and TLioConvert.
/// Verifies that fluent-built scripts execute correctly and round-trip through
/// TLioConvert.Serialize / TLioConvert.Parse.
/// </summary>
[TestFixture]
public class FluentBuilderTests
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

    // ── Execution tests ───────────────────────────────────────────────────────

    [Test]
    public void Add_FluentScript_ExecutesCorrectly()
    {
        var script = new TLioScript<JToken>()
            .Add(JValue.CreateString("hello")).OnPath("$.greeting");

        var input  = JObject.Parse("{}");
        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!["greeting"]!.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void Set_FluentScript_ExecutesCorrectly()
    {
        var script = new TLioScript<JToken>()
            .Set(new JValue(42)).OnPath("$.count");

        var input  = JObject.Parse("{\"count\": 0}");
        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!["count"]!.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void Remove_FluentScript_ExecutesCorrectly()
    {
        var script = new TLioScript<JToken>()
            .Remove().OnPath("$.temp");

        var input  = JObject.Parse("{\"keep\": 1, \"temp\": \"delete-me\"}");
        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!["temp"], Is.Null);
        Assert.That(result.Data!["keep"]!.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void Copy_FluentScript_ExecutesCorrectly()
    {
        var script = new TLioScript<JToken>()
            .Copy().From("$.source").To("$.dest");

        var input  = JObject.Parse("{\"source\": \"value\"}");
        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!["dest"]!.Value<string>(), Is.EqualTo("value"));
    }

    [Test]
    public void Compare_FluentScript_ExecutesCorrectly()
    {
        var script = new TLioScript<JToken>()
            .Compare().From("$.a").To("$.b").Result("$.verdict");

        var input  = JObject.Parse("{\"a\": 5, \"b\": 10}");
        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!["verdict"]!.Value<string>(), Is.EqualTo("less"));
    }

    [Test]
    public void Merge_FluentScript_ExecutesCorrectly()
    {
        var script = new TLioScript<JToken>()
            .Merge().From("$.patch").To("$.doc");

        var input  = JObject.Parse("{\"patch\": {\"name\": \"Alice\"}, \"doc\": {\"id\": 1}}");
        var result = _engine.Execute(script, input, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!["doc"]!["name"]!.Value<string>(), Is.EqualTo("Alice"));
        Assert.That(result.Data!["doc"]!["id"]!.Value<int>(), Is.EqualTo(1));
    }

    // ── TLioConvert round-trip tests ──────────────────────────────────────────

    [Test]
    public void TLioConvert_Serialize_ProducesValidJson()
    {
        var script = new TLioScript<JToken>()
            .Add(JValue.CreateString("hello")).OnPath("$.greeting")
            .Remove().OnPath("$.temp");

        var json = TLioConvert.Serialize(script, _adapter);

        Assert.That(json, Is.Not.Null.And.Not.Empty);
        var array = JArray.Parse(json);
        Assert.That(array.Count, Is.EqualTo(2));
        Assert.That(array[0]["command"]!.Value<string>(), Is.EqualTo("add"));
        Assert.That(array[1]["command"]!.Value<string>(), Is.EqualTo("remove"));
    }

    [Test]
    public void TLioConvert_Parse_ExecutesParsedScript()
    {
        const string scriptJson =
            "[{\"command\":\"add\",\"path\":\"$.x\",\"value\":\"hello\"}]";

        var parsed = TLioConvert.Parse(scriptJson, _options, _adapter);
        var result = _engine.Execute(parsed, JObject.Parse("{}"), JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!["x"]!.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void TLioConvert_RoundTrip_ProducesSameResult()
    {
        var script = new TLioScript<JToken>()
            .Set(new JValue(99)).OnPath("$.value");

        var json   = TLioConvert.Serialize(script, _adapter);
        var parsed = TLioConvert.Parse(json, _options, _adapter);

        var input   = JObject.Parse("{\"value\": 0}");
        var result1 = _engine.Execute(script, (JToken)input.DeepClone(), JsonExecutionContext.CreateDefault());
        var result2 = _engine.Execute(parsed, (JToken)input.DeepClone(), JsonExecutionContext.CreateDefault());

        // Compare numeric values tolerantly — Serialize emits the int 99 but
        // ParseOptions re-reads it as a double. Use Value<double> for comparison.
        var v1 = result1.Data!["value"]!.Value<double>();
        var v2 = result2.Data!["value"]!.Value<double>();
        Assert.That(v1, Is.EqualTo(v2),
            $"Fluent result: {result1.Data}\nParsed result: {result2.Data}");
    }
}
