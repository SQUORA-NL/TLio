using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.UnitTests.CoreTests;

/// <summary>
/// Tests the value pipeline — the chain of IFunctionSupportedValue implementations
/// that supply values to commands at execution time:
///
///   FixedValue&lt;TNode&gt;              — literal node; always returns the stored value
///   PathValue&lt;TNode&gt;               — evaluates a JsonPath at GetValue time
///   FunctionSupportedValue&lt;TNode&gt;  — delegates to IFunction.Execute
///   ExpandingFixedValue&lt;TNode&gt;     — object/array template with embedded =func() strings
///
/// The first three are in TLio.Core and are tested directly.
/// ExpandingFixedValue is internal to TLio.Client and is tested via the script engine.
/// </summary>
[TestFixture]
public class ValuePipelineTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void Setup()
    {
        _context = JsonExecutionContext.CreateDefault();
    }

    // ── FixedValue ─────────────────────────────────────────────────────────────

    [Test]
    public void FixedValue_StringLiteral_ReturnsNode()
    {
        var data = JToken.Parse("{}");
        var fv = new FixedValue<JToken>(new JValue("hello"));
        var result = fv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void FixedValue_NumberLiteral_ReturnsNode()
    {
        var data = JToken.Parse("{}");
        var fv = new FixedValue<JToken>(new JValue(42.5));
        var result = fv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Value<double>(), Is.EqualTo(42.5));
    }

    [Test]
    public void FixedValue_BooleanLiteral_ReturnsNode()
    {
        var data = JToken.Parse("{}");
        var fv = new FixedValue<JToken>(new JValue(true));
        var result = fv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Value<bool>(), Is.True);
    }

    [Test]
    public void FixedValue_NullLiteral_ReturnsNullNode()
    {
        var data = JToken.Parse("{}");
        var fv = new FixedValue<JToken>(JValue.CreateNull());
        var result = fv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void FixedValue_ObjectNode_ReturnsEntireObject()
    {
        var data = JToken.Parse("{}");
        var obj = JToken.Parse("{ \"x\": 1, \"y\": 2 }");
        var fv = new FixedValue<JToken>(obj);
        var result = fv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.First, obj), Is.True);
    }

    [Test]
    public void FixedValue_ArrayNode_ReturnsEntireArray()
    {
        var data = JToken.Parse("{}");
        var arr = JToken.Parse("[1, 2, 3]");
        var fv = new FixedValue<JToken>(arr);
        var result = fv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.First, arr), Is.True);
    }

    [Test]
    public void FixedValue_ReturnsSameReferenceOnEachCall()
    {
        var data = JToken.Parse("{}");
        var node = new JValue(99);
        var fv = new FixedValue<JToken>(node);
        var r1 = fv.GetValue(data, data, _context);
        var r2 = fv.GetValue(data, data, _context);
        Assert.That(ReferenceEquals(r1.Data.First, r2.Data.First), Is.True);
    }

    // ── PathValue ──────────────────────────────────────────────────────────────

    [Test]
    public void PathValue_ExistingPath_ReturnsFirstMatch()
    {
        var data = JToken.Parse("{ \"a\": { \"b\": 7 } }");
        var pv = new PathValue<JToken>("$.a.b");
        var result = pv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Value<int>(), Is.EqualTo(7));
    }

    [Test]
    public void PathValue_NonExistentPath_ReturnsFailed()
    {
        var data = JToken.Parse("{ \"a\": 1 }");
        var pv = new PathValue<JToken>("$.missing");
        var result = pv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void PathValue_MultipleMatches_ReturnsAllInData()
    {
        var data = JToken.Parse("{ \"items\": [{ \"v\": 1 }, { \"v\": 2 }, { \"v\": 3 }] }");
        var pv = new PathValue<JToken>("$.items[*].v");
        var result = pv.GetValue(data, data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.Count, Is.EqualTo(3));
    }

    [Test]
    public void PathValue_WhenUsedAsCommandValue_SetsPropertyFromPath()
    {
        var data = JToken.Parse("{ \"src\": \"hello\", \"dest\": \"\" }");
        var command = new TLio.Commands.Set<JToken>("$.dest", new PathValue<JToken>("$.src"));
        var result = command.Execute(data, _context);
        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.dest")?.Value<string>(), Is.EqualTo("hello"));
    }

    // ── FunctionSupportedValue ─────────────────────────────────────────────────

    [Test]
    public void FunctionSupportedValue_WithFetchFunction_ReturnsValueFromPath()
    {
        var data = JToken.Parse("{ \"source\": 42, \"target\": 0 }");
        var fetch = new Fetch<JToken>();
        fetch.SetArguments(new Arguments<JToken>(
            new[] { new FixedValue<JToken>(new JValue("$.source")) }));

        var fsv = new FunctionSupportedValue<JToken>(fetch);
        var result = fsv.GetValue(data, data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void FunctionSupportedValue_WhenFunctionFails_ReturnsFailed_AndLogsWarning()
    {
        // Fetch with a path that matches nothing → failure
        var data = JToken.Parse("{ \"a\": 1 }");
        var fetch = new Fetch<JToken>();
        fetch.SetArguments(new Arguments<JToken>(
            new[] { new FixedValue<JToken>(new JValue("$.nonExistent")) }));

        var fsv = new FunctionSupportedValue<JToken>(fetch);
        var result = fsv.GetValue(data, data, _context);

        Assert.That(result.Success, Is.False);
    }

    // ── ExpandingFixedValue (via ScriptEngine) ─────────────────────────────────

    [Test]
    public void Script_ObjectValue_NoFunctions_SetAsLiteral()
    {
        // When "value" is a JSON object with no "=" strings, it should be set as-is.
        var data = JToken.Parse("{ \"dest\": null }");
        var script = "[{ \"command\": \"put\", \"path\": \"$.dest\", \"value\": { \"x\": 1, \"y\": 2 } }]";

        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result = engine.Execute(script, data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.dest.x")?.Value<int>(), Is.EqualTo(1));
        Assert.That(data.SelectToken("$.dest.y")?.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void Script_ArrayValue_NoFunctions_SetAsLiteral()
    {
        var data = JToken.Parse("{ \"dest\": null }");
        var script = "[{ \"command\": \"put\", \"path\": \"$.dest\", \"value\": [10, 20, 30] }]";

        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result = engine.Execute(script, data, _context);

        Assert.That(result.Success, Is.True);
        var arr = data.SelectToken("$.dest") as JArray;
        Assert.That(arr, Is.Not.Null);
        Assert.That(arr!.Count, Is.EqualTo(3));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(20));
    }

    [Test]
    public void Script_ObjectValue_WithEmbeddedFetch_ExpandsAtRuntime()
    {
        // "value" is an object whose "name" field contains "=fetch($.source)".
        // The fetch function should be evaluated and its result substituted.
        var data = JToken.Parse("{ \"source\": \"Alice\", \"dest\": null }");
        var script = "[{ \"command\": \"put\", \"path\": \"$.dest\", \"value\": { \"name\": \"=fetch($.source)\" } }]";

        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result = engine.Execute(script, data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.dest.name")?.Value<string>(), Is.EqualTo("Alice"));
    }

    [Test]
    public void Script_ArrayValue_WithEmbeddedFetch_ExpandsAtRuntime()
    {
        var data = JToken.Parse("{ \"a\": 1, \"b\": 2, \"dest\": null }");
        var script = "[{ \"command\": \"put\", \"path\": \"$.dest\", \"value\": [\"=fetch($.a)\", \"=fetch($.b)\"] }]";

        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result = engine.Execute(script, data, _context);

        Assert.That(result.Success, Is.True);
        var arr = data.SelectToken("$.dest") as JArray;
        Assert.That(arr, Is.Not.Null);
        Assert.That(arr!.Count, Is.EqualTo(2));
        Assert.That(arr[0].Value<int>(), Is.EqualTo(1));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void Script_ObjectValue_WithNestedObject_ExpandsDeep()
    {
        // Nested structure: { "outer": { "inner": "=fetch($.src)" } }
        var data = JToken.Parse("{ \"src\": 99, \"dest\": null }");
        var script = "[{ \"command\": \"put\", \"path\": \"$.dest\", " +
                     "\"value\": { \"outer\": { \"inner\": \"=fetch($.src)\" } } }]";

        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result = engine.Execute(script, data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.dest.outer.inner")?.Value<int>(), Is.EqualTo(99));
    }

    [Test]
    public void Script_ObjectValue_MixedLiteralAndFunction_ExpandsOnlyFunctionFields()
    {
        var data = JToken.Parse("{ \"src\": \"fetched\", \"dest\": null }");
        var script = "[{ \"command\": \"put\", \"path\": \"$.dest\", " +
                     "\"value\": { \"literal\": \"plain\", \"dynamic\": \"=fetch($.src)\" } }]";

        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result = engine.Execute(script, data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.dest.literal")?.Value<string>(), Is.EqualTo("plain"));
        Assert.That(data.SelectToken("$.dest.dynamic")?.Value<string>(), Is.EqualTo("fetched"));
    }

    [Test]
    public void Script_ObjectValue_MultipleCallsSameScript_EachEvaluationIsIndependent()
    {
        // Run the same script twice on different data — each expansion is fresh
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var script = "[{ \"command\": \"put\", \"path\": \"$.dest\", \"value\": { \"v\": \"=fetch($.src)\" } }]";

        var data1 = JToken.Parse("{ \"src\": 10, \"dest\": null }");
        var data2 = JToken.Parse("{ \"src\": 20, \"dest\": null }");

        engine.Execute(script, data1, JsonExecutionContext.CreateDefault());
        engine.Execute(script, data2, JsonExecutionContext.CreateDefault());

        Assert.That(data1.SelectToken("$.dest.v")?.Value<int>(), Is.EqualTo(10));
        Assert.That(data2.SelectToken("$.dest.v")?.Value<int>(), Is.EqualTo(20));
    }
}
