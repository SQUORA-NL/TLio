using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Targets the base-class-specific behaviours of CopyMoveBase&lt;TNode&gt; that are
/// either not covered or only incidentally covered by CopyMoveTests and
/// CopyMoveDestinationAsArrayTests:
///
///   1. Validation — missing FromPath / ToPath returns failed + logs warning.
///   2. Array-index alignment — sources and destinations grouped by first [N] index
///      so that items in firstArray[0] only go to destinations also in firstArray[0].
///   3. Root destination — Copy-to-$ merges additive; Move-to-$ replaces root content.
///   4. Indirect path in ToPath — =indirect($.ref) reads the destination path from data.
///   5. DestinationAsArray flag — scalar/object promoted to [old, new]; existing array
///      has new value appended; DestinationAsArray=false on array still appends.
/// </summary>
[TestFixture]
public class CopyMoveBaseTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void Setup()
    {
        _context = JsonExecutionContext.CreateDefault();
    }

    // ── 1. Validation ──────────────────────────────────────────────────────────

    [Test]
    public void Copy_MissingFromPath_ReturnsFailed_AndLogsWarning()
    {
        var command = new Copy<JToken> { ToPath = "$.dest" };
        var data = JToken.Parse("{ \"a\": 1 }");

        var result = command.Execute(data, _context);

        Assert.That(result.Success, Is.False);
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("FromPath")), Is.True);
    }

    [Test]
    public void Copy_MissingToPath_ReturnsFailed_AndLogsWarning()
    {
        var command = new Copy<JToken> { FromPath = "$.a" };
        var data = JToken.Parse("{ \"a\": 1 }");

        var result = command.Execute(data, _context);

        Assert.That(result.Success, Is.False);
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("ToPath")), Is.True);
    }

    [Test]
    public void Move_MissingBothPaths_ReturnsFailed()
    {
        var command = new Move<JToken>();
        var data = JToken.Parse("{ \"a\": 1 }");

        var result = command.Execute(data, _context);

        Assert.That(result.Success, Is.False);
        Assert.That(_context.GetLogEntries().Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void Copy_FromPathMatchesNothing_ReturnsSuccess_AndLogsWarning()
    {
        var data = JToken.Parse("{ \"a\": 1 }");

        var result = new Copy<JToken>("$.nonExistent", "$.dest").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("no nodes found")), Is.True);
    }

    // ── 2. Array-index alignment ───────────────────────────────────────────────

    [Test]
    public void Copy_ArrayIndexAligned_SourcesRoutedToSameIndexDestinations()
    {
        // Each firstArray[N].source goes to firstArray[N].target, not cross-pollinating.
        var data = JToken.Parse(
            "{ \"items\": [" +
            "  { \"src\": 10, \"target\": 0 }," +
            "  { \"src\": 20, \"target\": 0 }," +
            "  { \"src\": 30, \"target\": 0 }" +
            "]}");

        var result = new Copy<JToken>("$.items[*].src", "$.items[*].target")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.items[0].target")?.Value<int>(), Is.EqualTo(10));
        Assert.That(data.SelectToken("$.items[1].target")?.Value<int>(), Is.EqualTo(20));
        Assert.That(data.SelectToken("$.items[2].target")?.Value<int>(), Is.EqualTo(30));
    }

    [Test]
    public void Move_ArrayIndexAligned_RemovesSourceAfterCopy()
    {
        var data = JToken.Parse(
            "{ \"rows\": [" +
            "  { \"old\": \"a\" }," +
            "  { \"old\": \"b\" }" +
            "]}");

        var result = new Move<JToken>("$.rows[*].old", "$.rows[*].new")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.rows[0].new")?.Value<string>(), Is.EqualTo("a"));
        Assert.That(data.SelectToken("$.rows[1].new")?.Value<string>(), Is.EqualTo("b"));
        Assert.That(data.SelectToken("$.rows[0].old"), Is.Null);
        Assert.That(data.SelectToken("$.rows[1].old"), Is.Null);
    }

    [Test]
    public void Copy_TwoLevelArrayAlignment_InnerItemsRoutedCorrectly()
    {
        // Mirrors JLio's CanCopyMovePropertiesInAnLayeredArray test pattern but
        // using Copy to isolate the alignment base-class behaviour.
        var data = JToken.Parse(
            "{\"groups\":[" +
            "  {\"items\":[{\"val\":1},{\"val\":2}],\"result\":[]}," +
            "  {\"items\":[{\"val\":3},{\"val\":4}],\"result\":[]}" +
            "]}");

        var result = new Copy<JToken>("$.groups[*].items[*].val", "$.groups[*].result")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var g0 = data.SelectTokens("$.groups[0].result[*]").Select(t => t.Value<int>()).ToList();
        var g1 = data.SelectTokens("$.groups[1].result[*]").Select(t => t.Value<int>()).ToList();
        Assert.That(g0, Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(g1, Is.EquivalentTo(new[] { 3, 4 }));
    }

    // ── 3. Root destination ────────────────────────────────────────────────────

    [Test]
    public void Copy_ToRoot_MergesSourceIntoRootAdditively()
    {
        // Root already has "existing"; copying an object with "added" should add it
        // without removing "existing".
        var data = JToken.Parse("{ \"existing\": 1, \"payload\": { \"added\": 99 } }");

        var result = new Copy<JToken>("$.payload", "$").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.existing")?.Value<int>(), Is.EqualTo(1));
        Assert.That(data.SelectToken("$.added")?.Value<int>(), Is.EqualTo(99));
    }

    [Test]
    public void Move_ToRoot_ReplacesEntireRootContent()
    {
        // Move-to-root replaces root: "existing" should disappear, "added" should appear.
        var data = JToken.Parse("{ \"existing\": 1, \"payload\": { \"added\": 99 } }");

        var result = new Move<JToken>("$.payload", "$").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.added")?.Value<int>(), Is.EqualTo(99));
        Assert.That(data.SelectToken("$.existing"), Is.Null);
        Assert.That(data.SelectToken("$.payload"), Is.Null);
    }

    // ── 4. Indirect path in ToPath ─────────────────────────────────────────────

    [Test]
    public void Copy_IndirectToPath_ResolvesDestinationFromData()
    {
        // $.destRef holds the actual destination path string "$.target".
        // Copy should follow the indirection and write to $.target.
        var data = JToken.Parse("{ \"source\": \"hello\", \"destRef\": \"$.target\" }");

        var result = new Copy<JToken>("$.source", "=indirect($.destRef)")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.target")?.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void Move_IndirectToPath_ResolvesDestinationFromData()
    {
        var data = JToken.Parse("{ \"value\": 42, \"pathRef\": \"$.moved\" }");

        var result = new Move<JToken>("$.value", "=indirect($.pathRef)")
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.moved")?.Value<int>(), Is.EqualTo(42));
        Assert.That(data.SelectToken("$.value"), Is.Null);
    }

    // ── 5. DestinationAsArray flag ─────────────────────────────────────────────

    [Test]
    public void Copy_DestinationAsArray_ScalarPromotion_OldAndNewInArray()
    {
        var data = JToken.Parse("{ \"val\": 5 }");

        var result = new Copy<JToken>("$.val", "$.val", destinationAsArray: true)
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var arr = data["val"] as JArray;
        Assert.That(arr, Is.Not.Null);
        Assert.That(arr!.Count, Is.EqualTo(2));
        Assert.That(arr[0].Value<int>(), Is.EqualTo(5));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(5));
    }

    [Test]
    public void Copy_DestinationAsArray_ObjectPromotion_OldAndNewInArray()
    {
        var data = JToken.Parse("{ \"obj\": { \"x\": 1 }, \"src\": { \"y\": 2 } }");

        var result = new Copy<JToken>("$.src", "$.obj", destinationAsArray: true)
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var arr = data["obj"] as JArray;
        Assert.That(arr, Is.Not.Null);
        Assert.That(arr!.Count, Is.EqualTo(2));
        Assert.That(arr[0]["x"]?.Value<int>(), Is.EqualTo(1));
        Assert.That(arr[1]["y"]?.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void Copy_DestinationAsArray_ExistingArray_Appends()
    {
        // Destination is already an array — DestinationAsArray flag just appends (no re-wrapping).
        var data = JToken.Parse("{ \"arr\": [10, 20], \"src\": 30 }");

        var result = new Copy<JToken>("$.src", "$.arr", destinationAsArray: true)
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var arr = data["arr"] as JArray;
        Assert.That(arr!.Count, Is.EqualTo(3));
        Assert.That(arr[2].Value<int>(), Is.EqualTo(30));
    }

    [Test]
    public void Copy_NoDestinationAsArray_ArrayDestination_AlwaysAppends()
    {
        // Without DestinationAsArray flag, copying to an array destination still appends.
        var data = JToken.Parse("{ \"arr\": [1, 2], \"src\": 3 }");

        var result = new Copy<JToken>("$.src", "$.arr").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var arr = data["arr"] as JArray;
        Assert.That(arr!.Count, Is.EqualTo(3));
        Assert.That(arr[2].Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void Move_DestinationAsArray_ScalarPromotion_SourceRemovedAfter()
    {
        var data = JToken.Parse("{ \"src\": 7, \"dest\": 8 }");

        var result = new Move<JToken>("$.src", "$.dest", destinationAsArray: true)
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var arr = data["dest"] as JArray;
        Assert.That(arr!.Count, Is.EqualTo(2));
        Assert.That(data.SelectToken("$.src"), Is.Null);
    }

    // ── Article X: logging assertions ────────────────────────────────────────

    [Test]
    public void Copy_Success_LogsInfoEntry()
    {
        var data = JToken.Parse("{ \"src\": 42, \"dest\": {} }");

        var result = new Copy<JToken>("$.src", "$.dest").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_context.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }
}
