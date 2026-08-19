using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// A path is a value like any other: <c>=indirect($.target).field</c> reads the real path out of
/// the document. Every command that takes a path must resolve it before handing it to the fetcher,
/// because a raw <c>=</c> is not legal in JsonPath or XPath.
///
/// Both halves used to be broken: remove, rename and copy/move's FromPath never resolved the
/// expression at all, and an expression that could not be resolved fell back to the raw path —
/// so an unresolvable =indirect() threw a JsonException straight out of the engine in every
/// command, script and all.
/// </summary>
[TestFixture]
public class IndirectPathTests
{
    private const string Doc =
        """{"target":"$.dest","dest":{"existing":1},"src":"payload"}""";

    private IExecutionContext<JToken> _context = null!;
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _context = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken Run(string script, string document = Doc)
    {
        var result = _engine.Execute(script, JToken.Parse(document), _context);
        Assert.That(result.Success, Is.True,
            $"log: {string.Join(" | ", _context.GetLogEntries().Select(e => $"[{e.Level}] {e.Message}"))}");
        return result.Data;
    }

    private bool WarnedAboutIndirect() => _context.GetLogEntries()
        .Any(e => e.Level >= LogLevel.Warning && e.Message.Contains("=indirect()"));

    // ── Every command resolves =indirect() in its path ────────────────────────

    [Test]
    public void Set_ResolvesIndirectInPath()
    {
        var data = Run("""[{"command":"set","path":"=indirect($.target).existing","value":99}]""");

        Assert.That(data.SelectToken("$.dest.existing")!.Value<int>(), Is.EqualTo(99));
    }

    [Test]
    public void Put_ResolvesIndirectInPath()
    {
        var data = Run("""[{"command":"put","path":"=indirect($.target).fresh","value":7}]""");

        Assert.That(data.SelectToken("$.dest.fresh")!.Value<int>(), Is.EqualTo(7));
    }

    [Test]
    public void Add_ResolvesIndirectInPath()
    {
        var data = Run("""[{"command":"add","path":"=indirect($.target).added","value":7}]""");

        Assert.That(data.SelectToken("$.dest.added")!.Value<int>(), Is.EqualTo(7));
    }

    [Test]
    public void Copy_ResolvesIndirectInToPath()
    {
        var data = Run("""[{"command":"copy","fromPath":"$.src","toPath":"=indirect($.target).copied"}]""");

        Assert.That(data.SelectToken("$.dest.copied")!.Value<string>(), Is.EqualTo("payload"));
        Assert.That(data.SelectToken("$.src"), Is.Not.Null, "copy keeps the source");
    }

    [Test]
    public void Move_ResolvesIndirectInToPath()
    {
        var data = Run("""[{"command":"move","fromPath":"$.src","toPath":"=indirect($.target).moved"}]""");

        Assert.That(data.SelectToken("$.dest.moved")!.Value<string>(), Is.EqualTo("payload"));
        Assert.That(data.SelectToken("$.src"), Is.Null, "move removes the source");
    }

    [Test]
    public void Copy_ResolvesIndirectInFromPath()
    {
        var data = Run("""[{"command":"copy","fromPath":"=indirect($.target).existing","toPath":"$.out"}]""");

        Assert.That(data.SelectToken("$.out")!.Value<int>(), Is.EqualTo(1),
            "FromPath was previously never resolved and threw");
    }

    [Test]
    public void Move_ResolvesIndirectInFromPath()
    {
        var data = Run("""[{"command":"move","fromPath":"=indirect($.target).existing","toPath":"$.out"}]""");

        Assert.That(data.SelectToken("$.out")!.Value<int>(), Is.EqualTo(1));
        Assert.That(data.SelectToken("$.dest.existing"), Is.Null);
    }

    [Test]
    public void Remove_ResolvesIndirectInPath()
    {
        var data = Run("""[{"command":"remove","path":"=indirect($.target).existing"}]""");

        Assert.That(data.SelectToken("$.dest.existing"), Is.Null,
            "remove was previously the only write command that ignored =indirect() entirely");
    }

    [Test]
    public void Rename_ResolvesIndirectInPath()
    {
        var data = Run("""[{"command":"rename","path":"=indirect($.target).existing","name":"renamed"}]""");

        Assert.That(data.SelectToken("$.dest.renamed")!.Value<int>(), Is.EqualTo(1));
        Assert.That(data.SelectToken("$.dest.existing"), Is.Null);
    }

    // ── An unresolvable expression warns and no-ops; it never throws ──────────

    [TestCase("""[{"command":"set","path":"=indirect($.nope).x","value":1}]""")]
    [TestCase("""[{"command":"put","path":"=indirect($.nope).x","value":1}]""")]
    [TestCase("""[{"command":"add","path":"=indirect($.nope).x","value":1}]""")]
    [TestCase("""[{"command":"remove","path":"=indirect($.nope).x"}]""")]
    [TestCase("""[{"command":"rename","path":"=indirect($.nope).x","name":"y"}]""")]
    [TestCase("""[{"command":"copy","fromPath":"$.src","toPath":"=indirect($.nope).x"}]""")]
    [TestCase("""[{"command":"move","fromPath":"$.src","toPath":"=indirect($.nope).x"}]""")]
    [TestCase("""[{"command":"copy","fromPath":"=indirect($.nope).x","toPath":"$.out"}]""")]
    public void MissingIndirectTarget_WarnsAndLeavesTheDocumentAlone(string script)
    {
        var before = JToken.Parse(Doc);

        JToken? data = null;
        Assert.That(() => data = Run(script), Throws.Nothing,
            "the raw path used to reach JsonPath and throw a JsonException out of the engine");

        Assert.That(JToken.DeepEquals(data!, before), Is.True, $"document changed: {data}");
        Assert.That(WarnedAboutIndirect(), Is.True, "the author needs to know the path never resolved");
    }

    [Test]
    public void IndirectTargetThatIsNotAString_WarnsAndNoOps()
    {
        // $.dest holds an object, not a path string
        JToken? data = null;
        Assert.That(() => data = Run("""[{"command":"set","path":"=indirect($.dest).x","value":1}]"""),
            Throws.Nothing);

        Assert.That(JToken.DeepEquals(data!, JToken.Parse(Doc)), Is.True);
        Assert.That(WarnedAboutIndirect(), Is.True);
    }

    [Test]
    public void IndirectTargetThatIsAnEmptyString_WarnsAndNoOps()
    {
        JToken? data = null;
        Assert.That(() => data = Run("""[{"command":"set","path":"=indirect($.blank).x","value":1}]""",
            """{"blank":"","dest":{"existing":1}}"""), Throws.Nothing);

        Assert.That(WarnedAboutIndirect(), Is.True);
    }

    [Test]
    public void AnUnresolvableIndirect_DoesNotStopLaterSteps()
    {
        var data = Run("""
            [
              {"command":"set","path":"=indirect($.nope).x","value":1},
              {"command":"put","path":"$.dest.after","value":"ran"}
            ]
            """);

        Assert.That(data.SelectToken("$.dest.after")!.Value<string>(), Is.EqualTo("ran"),
            "one unresolvable path must not abort the script");
        Assert.That(WarnedAboutIndirect(), Is.True);
    }


    // ── Advanced commands resolve it in every one of their paths ──────────────

    [Test]
    public void Merge_ResolvesIndirectInBothPaths()
    {
        var data = Run("""[{"command":"merge","path":"=indirect($.target)","targetPath":"$.other"}]""",
            """{"target":"$.dest","dest":{"a":1},"other":{"b":2}}""");

        Assert.That(data.SelectToken("$.other.a")!.Value<int>(), Is.EqualTo(1));

        SetUp();
        data = Run("""[{"command":"merge","path":"$.dest","targetPath":"=indirect($.target2)"}]""",
            """{"target2":"$.other","dest":{"a":1},"other":{"b":2}}""");

        Assert.That(data.SelectToken("$.other.a")!.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void Compare_ResolvesIndirectInAllThreePaths()
    {
        var data = Run(
            """[{"command":"compare","firstPath":"=indirect($.p1)","secondPath":"=indirect($.p2)","resultPath":"=indirect($.p3)"}]""",
            """{"p1":"$.a","p2":"$.b","p3":"$.verdict","a":2,"b":2}""");

        Assert.That(data.SelectToken("$.verdict")!.Value<string>(), Is.EqualTo("equal"),
            "firstPath, secondPath and resultPath all accept =indirect()");
    }

    [TestCase("""[{"command":"merge","path":"=indirect($.nope)","targetPath":"$.other"}]""")]
    [TestCase("""[{"command":"merge","path":"$.dest","targetPath":"=indirect($.nope)"}]""")]
    [TestCase("""[{"command":"compare","firstPath":"=indirect($.nope)","secondPath":"$.other","resultPath":"$.r"}]""")]
    [TestCase("""[{"command":"compare","firstPath":"$.dest","secondPath":"$.other","resultPath":"=indirect($.nope)"}]""")]
    public void AdvancedCommands_UnresolvableIndirect_WarnsAndNoOps(string script)
    {
        const string doc = """{"target":"$.dest","dest":{"a":1},"other":{"b":2}}""";
        var before = JToken.Parse(doc);

        JToken? data = null;
        Assert.That(() => data = Run(script, doc), Throws.Nothing,
            "merge and compare used to throw a JsonException out of the engine");

        Assert.That(JToken.DeepEquals(data!, before), Is.True, $"document changed: {data}");
        Assert.That(WarnedAboutIndirect(), Is.True);
    }

    // ── Paths with no indirect expression are untouched ───────────────────────

    [Test]
    public void APlainPathIsPassedThroughUnchanged()
    {
        var data = Run("""[{"command":"set","path":"$.dest.existing","value":42}]""");

        Assert.That(data.SelectToken("$.dest.existing")!.Value<int>(), Is.EqualTo(42));
        Assert.That(WarnedAboutIndirect(), Is.False);
    }
}
