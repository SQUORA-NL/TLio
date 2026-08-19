using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// =partial($.path, n) — picks the nth match (0-based) out of a multi-match path, or the
/// first when n is omitted. It is the escape hatch for "this path matches several nodes but
/// I want exactly one", so the index handling is the part that matters: out of range, negative,
/// non-numeric, and fractional indexes are all things an author will hit.
/// </summary>
[TestFixture]
public class PartialDepthTests
{
    private const string Doc =
        """{"items":[{"n":"a"},{"n":"b"},{"n":"c"}],"single":[{"n":"only"}],"empty":[],"scalar":7}""";

    private IExecutionContext<JToken> _context = null!;
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _context = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken? Pick(string args, string document = Doc)
    {
        var script = $$"""[{"command":"put","path":"$.out","value":"=partial({{args}})"}]""";
        return _engine.Execute(script, JToken.Parse(document), _context).Data.SelectToken("$.out");
    }

    private bool Warned() => _context.GetLogEntries().Any(e => e.Level >= LogLevel.Warning);

    // ── Index selection ───────────────────────────────────────────────────────

    [Test]
    public void NoIndex_TakesTheFirstMatch()
    {
        Assert.That(Pick("'$.items[*].n'")!.Value<string>(), Is.EqualTo("a"));
    }

    [TestCase(0, "a")]
    [TestCase(1, "b")]
    [TestCase(2, "c")]
    public void ExplicitIndex_TakesThatMatch(int index, string expected)
    {
        Assert.That(Pick($"'$.items[*].n',{index}")!.Value<string>(), Is.EqualTo(expected));
    }

    [Test]
    public void IndexIsZeroBased_UnlikeXPath()
    {
        Assert.That(Pick("'$.items[*].n',0")!.Value<string>(), Is.EqualTo("a"),
            "0 selects the first match — JSONPath conventions, not XPath's 1-based indexing");
    }

    [Test]
    public void ASingleMatchIsStillIndexable()
    {
        Assert.That(Pick("'$.single[*].n',0")!.Value<string>(), Is.EqualTo("only"));
    }

    [Test]
    public void SelectsAnObject_NotJustAScalar()
    {
        var result = Pick("'$.items[*]',1");

        Assert.That(result!["n"]!.Value<string>(), Is.EqualTo("b"));
    }

    [Test]
    public void AFractionalIndexIsTruncatedTowardZero()
    {
        Assert.That(Pick("'$.items[*].n',1.9")!.Value<string>(), Is.EqualTo("b"),
            "1.9 becomes 1, not 2");
    }

    [Test]
    public void AnIndexGivenAsAPath_IsNotResolvedAndSilentlyDefaultsToZero()
    {
        // Documented, not endorsed. The index argument is read with TryGetDouble on the raw
        // argument value, so '$.which' is the string "$.which", which is not numeric — and the
        // index quietly stays 0 instead of failing. A data-driven index therefore returns the
        // wrong element with no warning, which is the worst of the failure shapes here.
        var result = Pick("'$.items[*].n',$.which",
            """{"which":2,"items":[{"n":"a"},{"n":"b"},{"n":"c"}]}""");

        Assert.That(result!.Value<string>(), Is.EqualTo("a"),
            "if this ever returns 'c', path-valued indexes have been implemented — update this test");
    }

    // ── Out-of-range and malformed indexes ────────────────────────────────────

    [TestCase(3)]
    [TestCase(99)]
    public void IndexPastTheEnd_WarnsAndWritesNothing(int index)
    {
        Assert.That(Pick($"'$.items[*].n',{index}"), Is.Null);
        Assert.That(Warned(), Is.True);
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("out of range")), Is.True,
            "the message names the actual problem, and reports the count so it can be compared");
    }

    [Test]
    public void NegativeIndex_WarnsAndWritesNothing()
    {
        Assert.That(Pick("'$.items[*].n',-1"), Is.Null, "there is no from-the-end indexing");
        Assert.That(Warned(), Is.True);
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("out of range")), Is.True);
    }

    [Test]
    public void NonNumericIndex_FallsBackToTheFirstMatch()
    {
        // A non-numeric second argument leaves the index at its default rather than failing.
        var result = Pick("'$.items[*].n','second'");

        Assert.That(result!.Value<string>(), Is.EqualTo("a"),
            "documented, not endorsed: a mistyped index silently yields element 0");
    }

    // ── Path failures ─────────────────────────────────────────────────────────

    [Test]
    public void PathMatchingNothing_WarnsAndWritesNothing()
    {
        Assert.That(Pick("'$.nowhere[*]'"), Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void EmptyArray_WarnsAndWritesNothing()
    {
        Assert.That(Pick("'$.empty[*]'"), Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void NoArguments_WarnsAndWritesNothing()
    {
        var result = _engine
            .Execute("""[{"command":"put","path":"$.out","value":"=partial()"}]""",
                JToken.Parse(Doc), _context)
            .Data.SelectToken("$.out");

        Assert.That(result, Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void AMalformedPath_DoesNotThrow()
    {
        Assert.That(() => Pick("'not a path'"), Throws.Nothing);
    }
}
