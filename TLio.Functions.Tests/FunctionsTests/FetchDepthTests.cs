using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// =fetch($.path) — read a value from elsewhere in the document.
///
/// The subtle part is the path-detection rule: the resolved argument is treated as a path only
/// when it is a string of length ≥ 2 starting with '$' or '@'. Anything else is returned as-is.
/// That is what lets =fetch(=concat(...)) return a computed value instead of trying to select
/// with it — and it is also what makes a string that merely happens to start with '$' behave
/// unexpectedly, so both sides are pinned here.
/// </summary>
[TestFixture]
public class FetchDepthTests
{
    private const string Doc = """
        {
          "order": { "email": "a@b.com", "total": 42, "flag": true, "nothing": null },
          "items": [ { "sku": "x" }, { "sku": "y" } ],
          "ref": "$.order.email",
          "literal": "not-a-path",
          "dollar": "$"
        }
        """;

    private IExecutionContext<JToken> _context = null!;
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _context = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken? Fetch(string args, string document = Doc)
    {
        var script = $$"""[{"command":"put","path":"$.out","value":"=fetch({{args}})"}]""";
        return _engine.Execute(script, JToken.Parse(document), _context).Data.SelectToken("$.out");
    }

    private bool Warned() => _context.GetLogEntries().Any(e => e.Level >= LogLevel.Warning);

    // ── Reading values ────────────────────────────────────────────────────────

    [Test]
    public void ReadsAString() =>
        Assert.That(Fetch("$.order.email")!.Value<string>(), Is.EqualTo("a@b.com"));

    [Test]
    public void ReadsANumber() =>
        Assert.That(Fetch("$.order.total")!.Value<int>(), Is.EqualTo(42));

    [Test]
    public void ReadsABoolean() =>
        Assert.That(Fetch("$.order.flag")!.Value<bool>(), Is.True);

    [Test]
    public void ReadsAnExplicitNull()
    {
        var result = Fetch("$.order.nothing");

        Assert.That(result, Is.Not.Null, "a JSON null is a value, not a miss");
        Assert.That(result!.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void ReadsAnObject()
    {
        var result = Fetch("$.order");

        Assert.That(result!["email"]!.Value<string>(), Is.EqualTo("a@b.com"));
    }

    [Test]
    public void ReadsAnArrayElement() =>
        Assert.That(Fetch("$.items[1].sku")!.Value<string>(), Is.EqualTo("y"));

    [Test]
    public void AQuotedPathBehavesLikeABareOne() =>
        Assert.That(Fetch("'$.order.email'")!.Value<string>(), Is.EqualTo("a@b.com"));

    // ── Numbers keep their type ───────────────────────────────────────────────

    [Test]
    public void ANumberIsNotStringified()
    {
        var result = Fetch("$.order.total");

        Assert.That(result!.Type, Is.EqualTo(JTokenType.Integer),
            "fetch returns the node, not a rendering of it");
    }

    // ── The path-detection rule ───────────────────────────────────────────────

    [Test]
    public void AComputedNonPathValueIsReturnedAsIs()
    {
        // The whole point of the detection rule: an inner function that produces a plain
        // string must not be re-interpreted as a path expression.
        var result = Fetch("=concat($.literal,'-suffix')");

        Assert.That(result!.Value<string>(), Is.EqualTo("not-a-path-suffix"));
    }

    [Test]
    public void AComputedPathIsFollowed()
    {
        var result = Fetch("=indirect($.ref)");

        Assert.That(result!.Value<string>(), Is.EqualTo("a@b.com"),
            "indirect yields the email itself; fetch returns it unchanged");
    }

    [Test]
    public void ALoneDollarIsTooShortToBeAPath()
    {
        // Length < 2 — '$' alone selects nothing useful, so it is returned as the string it is.
        var result = Fetch("$.dollar");

        Assert.That(result!.Value<string>(), Is.EqualTo("$"));
    }

    [TestCase("""{"price":"$5"}""", "$.price", "$5")]
    [TestCase("""{"handle":"@ada"}""", "$.handle", "@ada")]
    public void AnOrdinaryStringStartingWithDollarOrAt_SurvivesIntact(
        string document, string argument, string expected)
    {
        // A currency value like "$5" or a handle like "@ada" satisfies the first half of the
        // detection rule, but selecting with it matches nothing and the original value is
        // handed back rather than the fetch failing. Worth pinning: the rule is forgiving here.
        var result = Fetch(argument, document);

        Assert.That(result!.Value<string>(), Is.EqualTo(expected));
    }

    // ── Default value ─────────────────────────────────────────────────────────

    [Test]
    public void ASecondArgumentIsUsedWhenThePathMatchesNothing()
    {
        var result = Fetch("$.order.missing,'fallback'");

        Assert.That(result!.Value<string>(), Is.EqualTo("fallback"));
    }

    [Test]
    public void TheDefaultIsNotUsedWhenThePathResolves()
    {
        var result = Fetch("$.order.email,'fallback'");

        Assert.That(result!.Value<string>(), Is.EqualTo("a@b.com"));
    }

    [Test]
    public void TheDefaultIsNotUsedForAnExplicitNull()
    {
        var result = Fetch("$.order.nothing,'fallback'");

        Assert.That(result!.Type, Is.EqualTo(JTokenType.Null),
            "null is a value the path matched — the default is for a miss, not for a null");
    }

    [Test]
    public void TheDefaultCanBeANumber()
    {
        var result = Fetch("$.order.missing,0");

        Assert.That(result!.Value<int>(), Is.EqualTo(0));
    }

    [Test]
    public void ADefaultGivenAsAPath_IsNotResolved()
    {
        // Documented, not endorsed. Only the FIRST argument goes through path detection; the
        // default is returned exactly as written, so "$.order.email" lands in the document as
        // that literal string rather than the address it points at.
        var result = Fetch("$.order.missing,$.order.email");

        Assert.That(result!.Value<string>(), Is.EqualTo("$.order.email"),
            "if this ever returns the email, path-valued defaults have been implemented");
    }

    // ── Failures ──────────────────────────────────────────────────────────────

    [Test]
    public void APathMatchingNothingWithNoDefault_WritesNothing()
    {
        Assert.That(Fetch("$.order.missing"), Is.Null);
    }

    [Test]
    public void NoArguments_WarnsAndWritesNothing()
    {
        var result = _engine
            .Execute("""[{"command":"put","path":"$.out","value":"=fetch()"}]""",
                JToken.Parse(Doc), _context)
            .Data.SelectToken("$.out");

        Assert.That(result, Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void AMalformedPath_DoesNotThrow()
    {
        Assert.That(() => Fetch("'$.['"), Throws.Nothing);
    }

    // ── Multi-match ───────────────────────────────────────────────────────────

    [Test]
    public void APathMatchingManyNodes_ReturnsTheWholeSet()
    {
        var result = Fetch("'$.items[*].sku'");

        Assert.That(result, Is.Not.Null,
            "fetch does not silently narrow to the first match — use =partial() for that");
    }
}
