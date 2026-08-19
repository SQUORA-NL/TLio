using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// =scriptpath() — the absolute path of the node the command is currently working on, as a
/// string. Its whole value is that it differs per match, so the tests that matter are the ones
/// where a command fans out over an array: each element must record its own path, not the
/// path of the first, and not the document root.
/// </summary>
[TestFixture]
public class ScriptPathDepthTests
{
    private IExecutionContext<JToken> _context = null!;
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _context = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken Run(string script, string document) =>
        _engine.Execute(script, JToken.Parse(document), _context).Data;

    // ── Per-match paths ───────────────────────────────────────────────────────

    [Test]
    public void EachArrayElementRecordsItsOwnPath()
    {
        var data = Run(
            """[{"command":"add","path":"$.items[*]","property":"self","value":"=scriptpath()"}]""",
            """{"items":[{"n":"a"},{"n":"b"},{"n":"c"}]}""");

        Assert.That(data.SelectToken("$.items[0].self")!.Value<string>(), Is.EqualTo("$.items[0]"));
        Assert.That(data.SelectToken("$.items[1].self")!.Value<string>(), Is.EqualTo("$.items[1]"));
        Assert.That(data.SelectToken("$.items[2].self")!.Value<string>(), Is.EqualTo("$.items[2]"));
    }

    [Test]
    public void NestedElementsRecordTheFullPath()
    {
        var data = Run(
            """[{"command":"add","path":"$.orders[*].lines[*]","property":"self","value":"=scriptpath()"}]""",
            """{"orders":[{"lines":[{"x":1},{"x":2}]}]}""");

        Assert.That(data.SelectToken("$.orders[0].lines[0].self")!.Value<string>(),
            Is.EqualTo("$.orders[0].lines[0]"));
        Assert.That(data.SelectToken("$.orders[0].lines[1].self")!.Value<string>(),
            Is.EqualTo("$.orders[0].lines[1]"));
    }

    [Test]
    public void APathToANamedObjectIsReported()
    {
        var data = Run(
            """[{"command":"add","path":"$.address","property":"self","value":"=scriptpath()"}]""",
            """{"address":{"city":"Amsterdam"}}""");

        Assert.That(data.SelectToken("$.address.self")!.Value<string>(), Is.EqualTo("$.address"));
    }

    [Test]
    public void TheRootReportsTheRootIndicator()
    {
        var data = Run(
            """[{"command":"add","path":"$","property":"self","value":"=scriptpath()"}]""",
            """{"a":1}""");

        Assert.That(data.SelectToken("$.self")!.Value<string>(), Is.EqualTo("$"));
    }

    // ── The result is a string node ───────────────────────────────────────────

    [Test]
    public void TheResultIsAStringNode()
    {
        var data = Run(
            """[{"command":"add","path":"$.address","property":"self","value":"=scriptpath()"}]""",
            """{"address":{"city":"Amsterdam"}}""");

        Assert.That(data.SelectToken("$.address.self")!.Type, Is.EqualTo(JTokenType.String));
    }

    // ── Composition ───────────────────────────────────────────────────────────

    [Test]
    public void ThePathCanBeFedStraightBackIntoIndirect()
    {
        // scriptpath produces a path string, and indirect consumes one — so a recorded path
        // can be dereferenced later in the same script.
        var data = Run("""
            [
              {"command":"add","path":"$.address","property":"self","value":"=scriptpath()"},
              {"command":"put","path":"$.echo","value":"=indirect($.address.self)"}
            ]
            """, """{"address":{"city":"Amsterdam"}}""");

        Assert.That(data.SelectToken("$.echo.city")!.Value<string>(), Is.EqualTo("Amsterdam"));
    }

    // ── Arguments ─────────────────────────────────────────────────────────────

    [Test]
    public void ARelativeChildArgument_ReportsThatChildsPath()
    {
        // The '@' form is resolved against the current node, so each element reports the path
        // of its own child. This is the argument form that works as intended.
        var data = Run(
            """[{"command":"add","path":"$.items[*]","property":"p","value":"=scriptpath('@.n')"}]""",
            """{"items":[{"n":"a"},{"n":"b"}]}""");

        Assert.That(data.SelectToken("$.items[0].p")!.Value<string>(), Is.EqualTo("$.items[0].n"));
        Assert.That(data.SelectToken("$.items[1].p")!.Value<string>(), Is.EqualTo("$.items[1].n"));
    }

    [TestCase("$.address")]
    [TestCase("'$.address'")]
    public void AnAbsolutePathArgument_ReportsTheRootRatherThanThatPath(string argument)
    {
        // Documented, not endorsed. Only arguments starting with '@' go through
        // ResolveRelativePath; anything else is used as the subject node directly — and the
        // node an argument resolves to has no parent chain to walk, so GetPath reports "$".
        // The absolute-argument form therefore always answers "$", whatever you pass it.
        var data = Run(
            $$"""[{"command":"put","path":"$.out","value":"=scriptpath({{argument}})"}]""",
            """{"address":{"city":"Amsterdam"}}""");

        Assert.That(data.SelectToken("$.out")!.Value<string>(), Is.EqualTo("$"),
            "if this ever returns '$.address', the absolute-argument form has been fixed");
    }

    [Test]
    public void TheParentRelativeForm_AlsoReportsTheRoot()
    {
        // Same root cause as above: '@.<--' resolves to a parent node that is handed back
        // without its parent chain, so the reported path collapses to "$" instead of "$.items".
        var data = Run(
            """[{"command":"add","path":"$.items[*]","property":"p","value":"=scriptpath('@.<--')"}]""",
            """{"items":[{"n":"a"},{"n":"b"}]}""");

        Assert.That(data.SelectToken("$.items[0].p")!.Value<string>(), Is.EqualTo("$"),
            "if this ever returns '$.items', parent-relative scriptpath has been fixed");
    }

    [Test]
    public void NoArguments_NeverFails()
    {
        var result = _engine.Execute(
            """[{"command":"put","path":"$.out","value":"=scriptpath()"}]""",
            JToken.Parse("""{"a":1}"""), _context);

        Assert.That(result.Success, Is.True,
            "scriptpath always has a current node to describe, so it has no failure mode");
    }
}
