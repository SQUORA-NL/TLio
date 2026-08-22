using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;

namespace TLio.Json.Tests;

/// <summary>
/// Writing through a path whose last step is a filter or a wildcard rather than a property name
/// or a position. The path names the nodes it matches, so the value goes to each match.
///
/// The XML spelling of the same rule is
/// <c>TLio.Xml.Tests.NativeXPath.SelectorLeafWriteTests</c> — the bug was in the shared command
/// layer, so it showed in both path languages.
/// </summary>
[TestFixture]
public class SelectorLeafWriteTests
{
    private const string Document = """
        { "coverages": [
            { "code": "WA",    "status": "REQ" },
            { "code": "CASCO", "status": "REQ" },
            { "code": "RB",    "status": "REQ" } ] }
        """;

    private static JToken Run(string script, string document = Document)
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var context = JsonExecutionContext.CreateDefault();

        var result = engine.Execute(script, JToken.Parse(document), context);
        Assert.That(result.Success, Is.True);
        return result.Data;
    }

    [Test]
    public void Put_ObjectThroughAFilter_ReplacesOnlyTheMatchAndKeepsItsSiblings()
    {
        var result = Run("""
            [ { "command": "put", "path": "$.coverages[?(@.code=='CASCO')]",
                "value": { "code": "CASCO", "premium": 42.5 } } ]
            """);

        var coverages = (JArray)result["coverages"]!;
        Assert.Multiple(() =>
        {
            Assert.That(coverages, Has.Count.EqualTo(3), "the array keeps its length");
            Assert.That(coverages[0]["code"]?.ToString(), Is.EqualTo("WA"));
            Assert.That(coverages[1]["premium"]?.Value<decimal>(), Is.EqualTo(42.5m));
            Assert.That(coverages[2]["code"]?.ToString(), Is.EqualTo("RB"));
        });
    }

    [Test]
    public void Put_ScalarUnderAFilter_StillWritesAPropertyOfTheMatch()
    {
        var coverages = (JArray)Run("""
            [ { "command": "put", "path": "$.coverages[?(@.code=='WA')].premium", "value": 10 } ]
            """)["coverages"]!;

        Assert.Multiple(() =>
        {
            Assert.That(coverages[0]["premium"]?.Value<int>(), Is.EqualTo(10));
            Assert.That(coverages[0]["status"]?.ToString(), Is.EqualTo("REQ"), "the match keeps its other fields");
            Assert.That(coverages[1]["premium"], Is.Null, "a non-match is untouched");
        });
    }

    [Test]
    public void Put_ObjectThroughAnIndex_IsUnchangedByTheSelectorBranch()
    {
        var coverages = (JArray)Run("""
            [ { "command": "put", "path": "$.coverages[1]", "value": { "code": "CASCO", "premium": 42.5 } } ]
            """)["coverages"]!;

        Assert.Multiple(() =>
        {
            Assert.That(coverages, Has.Count.EqualTo(3));
            Assert.That(coverages[1]["premium"]?.Value<decimal>(), Is.EqualTo(42.5m));
        });
    }

    [Test]
    public void Put_ThroughAWildcard_WritesToEveryElement()
    {
        var coverages = (JArray)Run("""
            [ { "command": "put", "path": "$.coverages[*].status", "value": "OK" } ]
            """)["coverages"]!;

        Assert.That(coverages.Select(c => c["status"]!.ToString()),
            Is.EqualTo(new[] { "OK", "OK", "OK" }));
    }

    [Test]
    public void Put_ThroughABracketQuotedKey_StillNamesAProperty()
    {
        // "$['a.b']" wears brackets but names a property, so it must not take the selector
        // branch — the dot inside is part of the name, not a step.
        var result = Run("""
            [ { "command": "put", "path": "$['a.b']", "value": "written" } ]
            """, """{ "x": 1 }""");

        Assert.That(result["a.b"]?.ToString(), Is.EqualTo("written"));
    }
}
