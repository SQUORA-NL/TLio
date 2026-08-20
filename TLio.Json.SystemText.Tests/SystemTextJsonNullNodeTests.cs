using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Client;
using TLio.Json.SystemText;

namespace TLio.Json.SystemText.Tests;

/// <summary>
/// Regression: a JSON null is a C# null in System.Text.Json, so a null-valued property had no
/// node to select — remove, rename, copy, move, merge and =fetch all reported "no nodes
/// matched" where Newtonsoft, XML and YAML found the node. The fetcher now hands out a
/// detached placeholder that remembers its slot (NullSlots), and the adapter answers for it
/// the way Newtonsoft answers for a null JValue. These tests pin that the two JSON engines
/// give the same answer for every null-node operation.
/// </summary>
[TestFixture]
public class SystemTextJsonNullNodeTests
{
    private static (TLio.Core.Models.TLioExecutionResult<JsonNode> Result,
                    TLio.Core.Models.ExecutionContext<JsonNode> Context)
        Run(string document, string script)
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        var engine = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
        var ctx = SystemTextJsonExecutionContext.CreateDefault();
        var result = engine.Execute(script, JsonNode.Parse(document)!, ctx);
        return (result, ctx);
    }

    private static void AssertNoMissWarnings(TLio.Core.Models.ExecutionContext<JsonNode> ctx)
    {
        var misses = ctx.GetLogEntries()
            .Where(e => e.Level == LogLevel.Warning &&
                        (e.Message.Contains("no nodes") ||
                         e.Message.Contains("not found") ||
                         e.Message.Contains("cannot")))
            .Select(e => e.Message)
            .ToList();
        Assert.That(misses, Is.Empty,
            "a null-valued node must be as selectable as it is in Newtonsoft");
    }

    [Test]
    public void Remove_OnANullValuedProperty_RemovesIt()
    {
        var (result, ctx) = Run("""{"a":null,"b":1}""",
            """[{"command":"remove","path":"$.a"}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!), Is.EqualTo("""{"b":1}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Rename_OnANullValuedProperty_KeepsValueAndKeyOrder()
    {
        var (result, ctx) = Run("""{"a":null,"b":1}""",
            """[{"command":"rename","path":"$.a","name":"z"}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!), Is.EqualTo("""{"z":null,"b":1}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Copy_OfANullValuedProperty_CopiesTheNull()
    {
        var (result, ctx) = Run("""{"a":null}""",
            """[{"command":"copy","fromPath":"$.a","toPath":"$.c"}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!), Is.EqualTo("""{"a":null,"c":null}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Move_OfANullValuedProperty_MovesTheNull()
    {
        var (result, ctx) = Run("""{"a":null,"b":1}""",
            """[{"command":"move","fromPath":"$.a","toPath":"$.c"}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!), Is.EqualTo("""{"b":1,"c":null}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Merge_IntoANullTarget_TheSourceWins()
    {
        var (result, ctx) = Run("""{"a":null,"s":{"x":1}}""",
            """[{"command":"merge","path":"$.s","toPath":"$.a"}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!),
            Is.EqualTo("""{"a":{"x":1},"s":{"x":1}}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Merge_OfANullSource_OverwritesTheTargetLikeAnyScalar()
    {
        var (result, ctx) = Run("""{"a":{"x":1},"s":null}""",
            """[{"command":"merge","path":"$.s","toPath":"$.a"}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!), Is.EqualTo("""{"a":null,"s":null}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Fetch_OfANullPath_ReturnsTheNull()
    {
        var (result, ctx) = Run("""{"a":null}""",
            """[{"command":"add","path":"$.b","value":"=fetch($.a)"}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!), Is.EqualTo("""{"a":null,"b":null}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Add_IntoANullNode_UpgradesItToAnObject()
    {
        var (result, ctx) = Run("""{"customer":null}""",
            """[{"command":"add","path":"$.customer.demo","value":3}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!),
            Is.EqualTo("""{"customer":{"demo":3}}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Add_DeepThroughANullNode_BuildsThePath()
    {
        var (result, ctx) = Run("""{"a":null}""",
            """[{"command":"add","path":"$.a.b.c","value":3}]""");

        Assert.That(result.Success, Is.True);
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!),
            Is.EqualTo("""{"a":{"b":{"c":3}}}"""));
        AssertNoMissWarnings(ctx);
    }

    [Test]
    public void Set_ThroughANullNode_StaysStrictAndChangesNothing()
    {
        var (result, ctx) = Run("""{"a":null}""",
            """[{"command":"set","path":"$.a.b","value":3}]""");

        Assert.That(result.Success, Is.True, "set on a missing path is a no-op, not a failure");
        Assert.That(ctx.NodeAdapter.Serialize(result.Data!), Is.EqualTo("""{"a":null}"""));
    }

    [Test]
    public void Replace_KeepsThePropertyInItsPlace()
    {
        var adapter = new SystemTextJsonNodeAdapter();
        var doc = JsonNode.Parse("""{"a":1,"b":2}""")!;

        adapter.Replace(doc["a"]!, adapter.CreateNumber(9));

        Assert.That(adapter.Serialize(doc), Is.EqualTo("""{"a":9,"b":2}"""));
    }

    [Test]
    public void ADocumentWithCopiedNulls_SerializesAsNull_NeverAsAPlaceholderObject()
    {
        var (result, ctx) = Run("""{"a":null}""",
            """[{"command":"copy","fromPath":"$.a","toPath":"$.c"}]""");

        var serialized = ctx.NodeAdapter.Serialize(result.Data!);
        Assert.That(serialized, Is.EqualTo("""{"a":null,"c":null}"""));
        Assert.That(serialized, Does.Not.Contain("{}"),
            "a null placeholder must never leak into the document as an empty object");
    }
}
