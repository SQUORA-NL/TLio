using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Math;
using TLio.Json.SystemText;

namespace TLio.Json.SystemText.Tests;

/// <summary>
/// Regression: a boxed CLR value must not become a JsonValueCustomized.
///
/// CreateValue used JsonValue.Create(object), which boxes into JsonValueCustomized&lt;object&gt;.
/// Serializing one throws "JsonSerializerOptions instance must specify a TypeInfoResolver".
/// MathFunctionBase hands CreateValue a boxed long, so any whole-number result from
/// sum / count / countif / length / indexof left the document unserializable — the
/// transformation reported success but the output could never be produced.
/// </summary>
[TestFixture]
public class SystemTextJsonCreateValueTests
{
    [Test]
    public void CreateValue_FromBoxedLong_IsSerializable()
    {
        var adapter = new SystemTextJsonNodeAdapter();
        Assert.That(adapter.Serialize(adapter.CreateValue((long)42)), Is.EqualTo("42"));
    }

    [Test]
    public void CreateValue_FromBoxedInt_IsSerializable()
    {
        var adapter = new SystemTextJsonNodeAdapter();
        Assert.That(adapter.Serialize(adapter.CreateValue(7)), Is.EqualTo("7"));
    }

    [Test]
    public void CreateValue_FromBoxedDouble_IsSerializable()
    {
        var adapter = new SystemTextJsonNodeAdapter();
        Assert.That(adapter.Serialize(adapter.CreateValue(2.5)), Is.EqualTo("2.5"));
    }

    [Test]
    public void SumResult_CanStillBeSerialized()
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JsonNode>();
        var engine = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);

        var ctx = SystemTextJsonExecutionContext.CreateDefault();
        var doc = JsonNode.Parse("""{"nums":[10,20,30]}""")!;
        var result = engine.Execute(
            """[{"command":"put","path":"$.total","value":"=sum($.nums[*])"}]""", doc, ctx);

        Assert.That(result.Success, Is.True);
        Assert.That(() => ctx.NodeAdapter.Serialize(result.Data!), Throws.Nothing);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!), Does.Contain("\"total\":60"));
    }

    [Test]
    public void CountResult_CanStillBeSerialized()
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JsonNode>();
        var engine = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);

        var ctx = SystemTextJsonExecutionContext.CreateDefault();
        var doc = JsonNode.Parse("""{"nums":[10,20,30]}""")!;
        var result = engine.Execute(
            """[{"command":"put","path":"$.n","value":"=count($.nums[*])"}]""", doc, ctx);

        Assert.That(result.Success, Is.True);
        Assert.That(() => ctx.NodeAdapter.Serialize(result.Data!), Throws.Nothing);
    }
}
