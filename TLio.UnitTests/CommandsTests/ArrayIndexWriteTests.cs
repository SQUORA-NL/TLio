using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Writing through an array subscript.
///
/// A trailing <c>[n]</c> used to be split off as a leaf *name*, so a command went looking for a
/// property called <c>items[1]</c>: <c>set</c> reported it as not found and left the array
/// alone, and <c>put</c> created a property literally named <c>items[1]</c> beside the array it
/// was meant to edit. Every test here names the behaviour it replaced.
/// </summary>
[TestFixture]
public class ArrayIndexWriteTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void SetUp() => _context = JsonExecutionContext.CreateDefault();

    private static JToken Doc(string json) => JToken.Parse(json);

    private IFunctionSupportedValue<JToken> Value(object v) =>
        new FixedValue<JToken>(_context.NodeAdapter.CreateValue(v));

    private static string Json(JToken t) => t.ToString(Newtonsoft.Json.Formatting.None);

    private string[] Warnings() =>
        _context.GetLogEntries()
            .Where(e => e.Level >= Microsoft.Extensions.Logging.LogLevel.Warning)
            .Select(e => e.Message).ToArray();

    // ── set ───────────────────────────────────────────────────────────────────

    [Test]
    public void Set_WritesToTheElementAtThatPosition()
    {
        var data = Doc("""{"items":["a","b","c"]}""");

        new Set<JToken>("$.items[1]", Value("Z")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"items":["a","Z","c"]}"""));
    }

    [Test]
    public void Set_OutOfRange_LeavesTheArrayAlone()
    {
        var data = Doc("""{"items":["a"]}""");

        new Set<JToken>("$.items[5]", Value("Z")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"items":["a"]}"""));
        Assert.That(Warnings(), Has.Some.Contains("no nodes matched"));
    }

    [Test]
    public void Set_ThroughANestedSubscript_ReachesTheProperty()
    {
        var data = Doc("""{"a":{"items":[{"n":1},{"n":2}]}}""");

        new Set<JToken>("$.a.items[0].n", Value(9L)).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"a":{"items":[{"n":9},{"n":2}]}}"""));
    }

    // ── put ───────────────────────────────────────────────────────────────────

    [Test]
    public void Put_WritesToTheElementInsteadOfCreatingAPropertyNamedAfterThePath()
    {
        var data = Doc("""{"items":["a","b"]}""");

        new Put<JToken>("$.items[0]", Value("Z")).Execute(data, _context);

        // Was: {"items":["a","b"],"items[0]":"Z"}
        Assert.That(Json(data), Is.EqualTo("""{"items":["Z","b"]}"""));
    }

    // ── add ───────────────────────────────────────────────────────────────────

    [Test]
    public void Add_AtTheNextFreePosition_Appends()
    {
        var data = Doc("""{"items":["a"]}""");

        new Add<JToken>("$.items[1]", Value("b")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"items":["a","b"]}"""));
    }

    [Test]
    public void Add_AtPositionZeroOfAnEmptyArray_Appends()
    {
        var data = Doc("""{"items":[]}""");

        new Add<JToken>("$.items[0]", Value("a")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"items":["a"]}"""));
    }

    [Test]
    public void Add_AtAnOccupiedPosition_IsSkipped()
    {
        var data = Doc("""{"items":["a","b"]}""");

        new Add<JToken>("$.items[0]", Value("z")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"items":["a","b"]}"""));
        Assert.That(Warnings(), Has.Some.Contains("already exists"));
    }

    [Test]
    public void Add_BeyondTheEnd_IsRefusedRatherThanAppended()
    {
        // Appending would land the element at index 2, which is not the position the path
        // named — a silently misplaced value is worse than a no-op.
        var data = Doc("""{"items":["a","b"]}""");

        new Add<JToken>("$.items[7]", Value("z")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"items":["a","b"]}"""));
        Assert.That(Warnings(), Has.Some.Contains("next position that can be added is 2"));
    }

    [Test]
    public void Add_CreatesTheArrayWhenItIsNotThere()
    {
        var data = Doc("""{"a":1}""");

        new Add<JToken>("$.tags[0]", Value("first")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"a":1,"tags":["first"]}"""));
    }

    [Test]
    public void Add_CreatesTheObjectsAboveAMissingArrayToo()
    {
        var data = Doc("""{"a":1}""");

        new Add<JToken>("$.meta.tags[0]", Value("first")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"a":1,"meta":{"tags":["first"]}}"""));
    }

    [Test]
    public void Add_WillNotCreateAnArrayAtANonZeroPosition()
    {
        var data = Doc("""{"a":1}""");

        new Add<JToken>("$.tags[3]", Value("x")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"a":1}"""));
        Assert.That(Warnings(), Has.Some.Contains("only position that can be added is 0"));
    }

    [Test]
    public void Add_RefusesAPositionOnSomethingThatIsNotAnArray()
    {
        var data = Doc("""{"tags":"not-an-array"}""");

        new Add<JToken>("$.tags[0]", Value("x")).Execute(data, _context);

        Assert.That(Json(data), Is.EqualTo("""{"tags":"not-an-array"}"""));
        Assert.That(Warnings(), Has.Some.Contains("is not an array"));
    }

    // ── What is and is not a position ─────────────────────────────────────────

    [TestCase("$.items[0]",       true)]
    [TestCase("$.items[12]",      true)]
    [TestCase("$.a.b[3]",         true)]
    [TestCase("$.items",          false)]
    [TestCase("$.items[*]",       false)]
    [TestCase("$['a.b']",         false)]
    [TestCase("$.items[-1]",      false)]
    [TestCase("$.items[ 1 ]",     false)]
    [TestCase("$.items[?(@.n>1)]", false)]
    [TestCase("$",                false)]
    public void OnlyAnIntegerSubscriptNamesAPosition(string path, bool expected)
    {
        Assert.That(_context.ItemsFetcher.IsLeafArrayIndex(path), Is.EqualTo(expected));
    }

    [Test]
    public void TheArrayPathAndPositionAreReadBackFromThePath()
    {
        Assert.That(_context.ItemsFetcher.TrySplitArrayIndex("$.a.items[2]", out var arrayPath, out var index),
            Is.True);
        Assert.That(arrayPath, Is.EqualTo("$.a.items"));
        Assert.That(index, Is.EqualTo(2));
    }
}
