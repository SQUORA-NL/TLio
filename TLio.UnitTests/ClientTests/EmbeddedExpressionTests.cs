using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.ClientTests;

/// <summary>
/// A command whose "value" is an object or an array holds a template, and the "=func()" strings
/// inside it are evaluated per execution. The expressions are <b>parsed once</b>, when the
/// script is parsed — the text cannot change afterwards.
///
/// What that must not change is anything observable: the same script run twice has to produce
/// the same document and the same log both times, including the notation warnings a bad
/// expression raises. Parsing once and therefore warning once would be the natural way to get
/// this wrong, so that is what these tests hold down. Two occurrences of the same expression
/// text share one parsed instance, so a function that answers differently on each call still
/// has to be called for each of them.
///
/// Built-in functions only — this project deliberately does not reference the extension packs.
/// </summary>
[TestFixture]
public class EmbeddedExpressionTests
{
    private ScriptEngine<JToken> engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private (string Document, List<string> Log) Run(TLioScript<JToken> script, string input)
    {
        var context = JsonExecutionContext.CreateDefault();
        var result = engine.Execute(script, JToken.Parse(input), context);
        return (result.Data.ToString(Newtonsoft.Json.Formatting.None),
                context.GetLogEntries().Select(e => $"{e.Level}|{e.Message}").ToList());
    }

    private TLioScript<JToken> Parse(string script) =>
        engine.Parse(script, ScriptFormat.Json, JsonExecutionContext.CreateDefault().NodeAdapter);

    // ── The same script, run twice ────────────────────────────────────────────

    [Test]
    public void OneParsedScriptRunTwice_ProducesTheSameDocumentBothTimes()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out", "value": {
                 "picked": "=fetch($.a)",
                 "chosen": "=if(=greaterThan($.a,$.b),'a','b')",
                 "nested": { "deep": [ "=fetch($.word)", "plain" ] } } }]
            """);

        var first = Run(script, """{ "a": 5, "b": 2, "word": "tlio" }""");
        var second = Run(script, """{ "a": 5, "b": 2, "word": "tlio" }""");

        Assert.That(second.Document, Is.EqualTo(first.Document));
        Assert.That(first.Document, Does.Contain("\"picked\":5").And.Contain("\"chosen\":\"a\""));
    }

    [Test]
    public void OneParsedScriptRunAgainstDifferentInput_RecomputesFromTheNewDocument()
    {
        var script = Parse("""[{ "command": "add", "path": "$.out", "value": { "picked": "=fetch($.a)" } }]""");

        var first = Run(script, """{ "a": 1 }""");
        var second = Run(script, """{ "a": 99 }""");

        Assert.Multiple(() =>
        {
            Assert.That(first.Document, Does.Contain("\"picked\":1"));
            Assert.That(second.Document, Does.Contain("\"picked\":99"),
                "the expression is parsed once but must be evaluated against each document");
        });
    }

    [Test]
    public void ANotationWarningIsRaisedOnEveryRun_NotOnlyTheFirst()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out", "value": { "x": "=coalesce(noSuchFunction($.a),'y')" } }]
            """);

        var first = Run(script, """{ "a": 1 }""");
        var second = Run(script, """{ "a": 1 }""");

        Assert.Multiple(() =>
        {
            Assert.That(first.Log.Any(l => l.Contains("noSuchFunction")), Is.True,
                "the first run must report the unregistered function");
            Assert.That(second.Log, Is.EqualTo(first.Log),
                "a second run reads the same log — parsing once must not mean warning once");
        });
    }

    [Test]
    public void AnUnknownFunctionLeavesTheTextInPlace_AndLogsOnEveryRun()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out", "value": { "x": "=noSuchFunction($.a)" } }]
            """);

        var first = Run(script, """{ "a": 1 }""");
        var second = Run(script, """{ "a": 1 }""");

        Assert.Multiple(() =>
        {
            Assert.That(first.Document, Does.Contain("=noSuchFunction($.a)"));
            Assert.That(first.Log.Any(l => l.Contains("Unknown function: noSuchFunction")), Is.True);
            Assert.That(second.Document, Is.EqualTo(first.Document));
            Assert.That(second.Log, Is.EqualTo(first.Log));
        });
    }

    [Test]
    public void AFunctionThatFails_LeavesTheTextInPlaceOnEveryRun()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out", "value": { "x": "=fetch($.missing)" } }]
            """);

        var first = Run(script, """{ "a": 1 }""");
        var second = Run(script, """{ "a": 1 }""");

        Assert.Multiple(() =>
        {
            Assert.That(first.Document, Does.Contain("=fetch($.missing)"));
            Assert.That(second.Document, Is.EqualTo(first.Document));
            Assert.That(second.Log, Is.EqualTo(first.Log));
        });
    }

    // ── Chaining: what a later expression can and cannot see ──────────────────

    [Test]
    public void NestedCallsInOneExpression_FeedTheirResultToTheOuterCall()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out",
               "value": { "verdict": "=if(=greaterThan($.a,$.b),'a-wins','b-wins')" } }]
            """);

        var (document, _) = Run(script, """{ "a": 5, "b": 2 }""");

        Assert.That(document, Does.Contain("\"verdict\":\"a-wins\""),
            "the inner comparison's result is what the outer call branches on");
    }

    [Test]
    public void ALaterCommandSeesWhatAnEarlierCommandWrote()
    {
        var script = Parse("""
            [ { "command": "add", "path": "$.first",  "value": "=fetch($.a)" },
              { "command": "add", "path": "$.second", "value": { "copied": "=fetch($.first)" } } ]
            """);

        var (document, _) = Run(script, """{ "a": 7 }""");

        Assert.That(document, Does.Contain("\"copied\":7"),
            "the first command wrote to the document, so the second reads a value and not the original");
    }

    [Test]
    public void WithinOneTemplate_ALaterExpressionCannotSeeAnEarlierOnesResult()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out",
               "value": { "first": "=fetch($.a)", "second": "=fetch($.out.first)" } }]
            """);

        var (document, _) = Run(script, """{ "a": 7 }""");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("\"first\":7"));
            Assert.That(document, Does.Contain("=fetch($.out.first)"),
                "a template is expanded in a clone that is not attached to the document yet, so "
                + "$.out.first does not exist while the walk runs — chain through separate "
                + "commands, or nest the calls inside one expression");
        });
    }

    // ── Shapes the walk has to keep straight ──────────────────────────────────

    [Test]
    public void TheSameExpressionTwiceInOneTemplate_ExpandsAtBothPlaces()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out",
               "value": { "one": "=fetch($.a)", "two": "=fetch($.a)",
                          "list": [ "=fetch($.a)" ] } }]
            """);

        var (document, _) = Run(script, """{ "a": 7 }""");

        Assert.That(document, Is.EqualTo("""{"a":7,"out":{"one":7,"two":7,"list":[7]}}"""));
    }

    [Test]
    public void TheSameExpressionTwice_IsStillEvaluatedTwice()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out", "value": { "one": "=newGuid()", "two": "=newGuid()" } }]
            """);

        var (document, _) = Run(script, "{}");
        var output = JObject.Parse(document)["out"]!;

        Assert.That((string?)output["one"], Is.Not.EqualTo((string?)output["two"]),
            "two occurrences share one parsed instance, but each occurrence is its own call");
    }

    [Test]
    public void ATemplateWithoutExpressions_ComesThroughUntouched()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out",
               "value": { "n": 1, "t": true, "z": null, "s": "plain",
                          "notAnExpression": "a = b",
                          "escaped": "==literal",
                          "deep": { "arr": [ 1, "two", { "three": 3 } ] } } }]
            """);

        var (document, log) = Run(script, "{}");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("\"notAnExpression\":\"a = b\""),
                "an '=' that is not the first character is not an expression");
            Assert.That(document, Does.Contain("\"escaped\":\"=literal\""));
            Assert.That(document, Does.Contain("\"deep\":{\"arr\":[1,\"two\",{\"three\":3}]}"));
            Assert.That(log.Any(l => l.StartsWith(nameof(LogLevel.Warning))), Is.False);
        });
    }

    [Test]
    public void AQuotedExpression_StaysText()
    {
        var script = Parse("""
            [{ "command": "add", "path": "$.out", "value": { "x": "'=fetch($.a)'" } }]
            """);

        var (document, _) = Run(script, """{ "a": 1 }""");

        Assert.That(document, Does.Contain("'=fetch($.a)'"),
            "quoting wins — the walk only expands a string that itself starts with '='");
    }

    [Test]
    public void ATemplateSurvivesSerialization_WithItsExpressionsStillWritten()
    {
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var script = Parse("""
            [{"command":"add","path":"$.out","value":{"picked":"=fetch($.a)","plain":1}}]
            """);

        var written = TLioConvert.Serialize(script, adapter);

        Assert.That(written, Does.Contain("=fetch($.a)"),
            "planning must not consume the template — the serializer writes it back as authored");
    }
}
