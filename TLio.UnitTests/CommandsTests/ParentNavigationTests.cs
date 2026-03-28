using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.ParentNavigationTests.
/// Tests parent-path navigation patterns in Move/Copy/Set commands using:
///   - @.&lt;-- relative path resolution (navigate to parent)
///   - ScriptPath function returning the path of a node's parent
///   - =indirect() combining ScriptPath output with Move destination
///   - Multi-level parent traversal (@.&lt;--.&lt;--)
///
/// ETL/Math/Text extensions are available so these tests could not be ported
/// until those phases were complete.
/// </summary>
[TestFixture]
public class ParentNavigationTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void Setup()
    {
        _context = JsonExecutionContext.CreateDefault();
    }

    // ── ScriptPath with parent navigation ─────────────────────────────────────

    [Test]
    public void ScriptPath_WithParentArg_ReturnsParentPath()
    {
        // ScriptPath(@.<--) on $.person.name should return "$.person".
        // When passed as a script argument, @.<-- is a FixedValue string — ScriptPath
        // resolves it via ResolveRelativePath before calling GetPath.
        var data = JToken.Parse("{ \"person\": { \"name\": \"Alice\" } }");
        var node = data.SelectToken("$.person.name")!;

        var fn = new ScriptPath<JToken>();
        fn.SetArguments(new Arguments<JToken>(new[]
        {
            // In script JSON: =scriptpath(@.<--) → FunctionConverter stores "@.<--" as FixedValue string
            (IFunctionSupportedValue<JToken>) new FixedValue<JToken>(new JValue("@.<--"))
        }));

        var result = fn.Execute(node, data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Value<string>(), Is.EqualTo("$.person"));
    }

    [Test]
    public void ScriptPath_TwoLevelParent_ReturnsGrandparentPath()
    {
        var data = JToken.Parse("{ \"root\": { \"child\": { \"leaf\": 1 } } }");
        var node = data.SelectToken("$.root.child.leaf")!;

        var fn = new ScriptPath<JToken>();
        fn.SetArguments(new Arguments<JToken>(new[]
        {
            (IFunctionSupportedValue<JToken>) new FixedValue<JToken>(new JValue("@.<--.<--"))
        }));

        var result = fn.Execute(node, data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Value<string>(), Is.EqualTo("$.root"));
    }

    // ── Set using @.<-- (sibling navigation) ──────────────────────────────────

    [Test]
    public void Set_UsingParentNavigation_WritesToSiblingPath()
    {
        // From $.a.b, @.<--.c navigates to sibling $.a.c
        var data = JToken.Parse("{ \"a\": { \"b\": 1, \"c\": 0 } }");
        var node = data.SelectToken("$.a.b")!;

        var fetcher = JsonExecutionContext.CreateDefault().ItemsFetcher;
        var resolved = fetcher.ResolveRelativePath("@.<--.c", node, data);

        // Use the resolved path in a Set command
        var result = new Set<JToken>(resolved, new FixedValue<JToken>(new JValue(99)))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.a.c")?.Value<int>(), Is.EqualTo(99));
    }

    // ── Move property up to parent container ──────────────────────────────────

    [Test]
    public void Move_NestedPropertyToParent_PromotesProperty()
    {
        // Move $.container.item.nested.value up to $.container.item.value (promote by one level)
        var data = JToken.Parse(
            "{ \"container\": { \"item\": { \"id\": 1, \"nested\": { \"value\": 42 } } } }");

        var result = new Move<JToken>("$.container.item.nested.value", "$.container.item.value")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.container.item.value")?.Value<int>(), Is.EqualTo(42));
        // Source was removed
        Assert.That(data.SelectToken("$.container.item.nested.value"), Is.Null);
    }

    [Test]
    public void Move_RecursiveDescent_RemovesFromCorrectParent()
    {
        // Move $..val to $.result (array destination) — all sources appended and removed from
        // their respective parent objects. Use array destination to avoid stale-reference
        // issues that arise when multiple sources sequentially replace a scalar destination.
        var data = JToken.Parse(
            "{ \"a\": { \"val\": 10 }, \"b\": { \"val\": 20 }, \"result\": [] }");

        var result = new Move<JToken>("$..val", "$.result")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        // Both source nodes removed from their parent objects
        Assert.That(data.SelectToken("$.a.val"), Is.Null);
        Assert.That(data.SelectToken("$.b.val"), Is.Null);
        // Both values appended to array destination
        var arr = data.SelectToken("$.result") as JArray;
        Assert.That(arr!.Count, Is.EqualTo(2));
    }

    [Test]
    public void Move_DeepNestingThreeLevels_RemovesSourceFromParent()
    {
        var data = JToken.Parse(
            "{ \"l1\": { \"l2\": { \"l3\": { \"target\": \"deep\" } } }, \"dest\": null }");

        var result = new Move<JToken>("$.l1.l2.l3.target", "$.dest")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.dest")?.Value<string>(), Is.EqualTo("deep"));
        Assert.That(data.SelectToken("$.l1.l2.l3.target"), Is.Null);
        // Parent object still exists
        Assert.That(data.SelectToken("$.l1.l2.l3"), Is.Not.Null);
    }

    // ── ScriptPath + =indirect for dynamic-destination Move ───────────────────

    [Test]
    public void Move_IndirectToPath_ReadsDestinationFromData()
    {
        // $.pathRef holds the actual destination; Move resolves it via =indirect()
        var data = JToken.Parse("{ \"src\": \"moved\", \"pathRef\": \"$.dest\" }");

        var result = new Move<JToken>("$.src", "=indirect($.pathRef)")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.dest")?.Value<string>(), Is.EqualTo("moved"));
        Assert.That(data.SelectToken("$.src"), Is.Null);
    }

    [Test]
    public void Move_IndirectToPath_ThenParentRemoved_ChainIsClean()
    {
        // Move value to a dynamically resolved destination AND verify parent cleanup
        var data = JToken.Parse(
            "{ \"inner\": { \"val\": 77 }, \"destination\": \"$.result\", \"result\": null }");

        var result = new Move<JToken>("$.inner.val", "=indirect($.destination)")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<int>(), Is.EqualTo(77));
        Assert.That(data.SelectToken("$.inner.val"), Is.Null);
    }

    // ── Collection-level parent navigation ────────────────────────────────────

    [Test]
    public void ScriptPath_OnEachItemInCollection_ReturnsCorrectPath()
    {
        // Set each item's "path" field to the item's own path string via ScriptPath
        var data = JToken.Parse(
            "{ \"items\": [{ \"id\": 1, \"path\": null }, { \"id\": 2, \"path\": null }] }");

        var fn = new ScriptPath<JToken>();
        fn.SetArguments(new Arguments<JToken>());  // no args → path of currentNode

        var script = new TLioScript<JToken>
        {
            new Set<JToken> { Path = "$.items[*].path", Value = new FunctionSupportedValue<JToken>(fn) }
        };

        var result = script.Execute(data, _context);

        Assert.That(result.Success, Is.True);
        // Each item gets its own current-node path written to "path" field
        // (Set operates on the item's parent and sets the leaf "path",
        //  so currentNode is the parent item; ScriptPath returns the parent's path)
        Assert.That(data.SelectToken("$.items[0].path")?.Value<string>(), Is.Not.Null);
        Assert.That(data.SelectToken("$.items[1].path")?.Value<string>(), Is.Not.Null);
        Assert.That(data.SelectToken("$.items[0].path")?.Value<string>(),
            Is.Not.EqualTo(data.SelectToken("$.items[1].path")?.Value<string>()));
    }

    [Test]
    public void Move_ArrayItems_EachRemovedFromCorrectParent()
    {
        // Move each item in one array to a different per-index destination
        var data = JToken.Parse(
            "{ \"src\": [{ \"val\": \"a\" }, { \"val\": \"b\" }], " +
            "  \"dst\": [{ \"out\": null }, { \"out\": null }] }");

        var result = new Move<JToken>("$.src[*].val", "$.dst[*].out")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.dst[0].out")?.Value<string>(), Is.EqualTo("a"));
        Assert.That(data.SelectToken("$.dst[1].out")?.Value<string>(), Is.EqualTo("b"));
        Assert.That(data.SelectToken("$.src[0].val"), Is.Null);
        Assert.That(data.SelectToken("$.src[1].val"), Is.Null);
    }

    // ── Copy preserves source, verifying parent chains stay intact ─────────────

    [Test]
    public void Copy_DeepNested_SourceParentChainIntact()
    {
        var data = JToken.Parse(
            "{ \"a\": { \"b\": { \"c\": { \"value\": 5 } } }, \"result\": null }");

        var result = new Copy<JToken>("$.a.b.c.value", "$.result")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.result")?.Value<int>(), Is.EqualTo(5));
        // Source and its parent chain are intact
        Assert.That(data.SelectToken("$.a.b.c.value")?.Value<int>(), Is.EqualTo(5));
        Assert.That(data.SelectToken("$.a.b.c"), Is.Not.Null);
        Assert.That(data.SelectToken("$.a.b"), Is.Not.Null);
    }

    // ── Article X: logging assertions ────────────────────────────────────────

    [Test]
    public void Move_WithParentNavigation_LogsInfoEntry()
    {
        var data = JToken.Parse("{ \"items\": [{ \"val\": 1 }], \"target\": {} }");
        var result = new Move<JToken>("$.items[0].val", "$.target")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_context.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }
}
