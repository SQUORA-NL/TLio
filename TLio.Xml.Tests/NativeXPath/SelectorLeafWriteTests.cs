using System.Xml.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;

namespace TLio.Xml.Tests.NativeXPath;

/// <summary>
/// Writing through a path whose last step is a <b>selector</b> — a predicate or a wildcard —
/// rather than a property name or a position.
///
/// Such a path names the nodes it matches. Before <c>IsLeafNodeSelector</c> the leaf was split
/// off as a property name no property has, which left the collection itself standing in as the
/// parent: <c>put</c> then took the array branch of its upsert and replaced every sibling with
/// the one value, so writing one entity emptied the rest out of the document.
/// </summary>
[TestFixture]
public class SelectorLeafWriteTests
{
    private XmlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new XmlNodeAdapter();

    private string Run(string xml, string scriptJson)
    {
        var options = ParseOptions<XElement>.CreateDefault();
        var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
        var context = XmlExecutionContext.CreateWithNativeXPath();

        var result = engine.Execute(scriptJson, _adapter.Parse(xml), context);
        Assert.That(result.Success, Is.True);
        return _adapter.Serialize(result.Data);
    }

    private const string Coverages =
        "<policy><coverages>" +
        "<coverage><code>WA</code><status>REQ</status></coverage>" +
        "<coverage><code>CASCO</code><status>REQ</status></coverage>" +
        "<coverage><code>RB</code><status>REQ</status></coverage>" +
        "</coverages></policy>";

    [Test]
    public void Put_ObjectThroughAPredicate_ReplacesOnlyTheMatchAndKeepsItsSiblings()
    {
        var result = Run(Coverages, """
            [ { "command": "put", "path": "/policy/coverages/coverage[code='CASCO']",
                "value": { "code": "CASCO", "premium": "42.50" } } ]
            """);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("<code>WA</code>"), "the first sibling survives");
            Assert.That(result, Does.Contain("<code>RB</code>"), "the last sibling survives");
            Assert.That(result, Does.Contain("<premium>42.50</premium>"), "the match was rewritten");
            Assert.That(result, Does.Not.Contain("<item>"),
                "the collection keeps its element name — it was not rebuilt as a canonical array");
        });
    }

    [Test]
    public void Put_ObjectThroughAPredicate_LeavesTheCollectionLengthAlone()
    {
        var result = XElement.Parse(Run(Coverages, """
            [ { "command": "put", "path": "/policy/coverages/coverage[code='CASCO']",
                "value": { "code": "CASCO", "premium": "42.50" } } ]
            """));

        Assert.That(result.Element("coverages")!.Elements().Count(), Is.EqualTo(3));
    }

    [Test]
    public void Put_ScalarUnderAPredicate_StillWritesAChildOfTheMatch()
    {
        // The case that always worked, kept so the fix cannot regress it: only the *leaf* being
        // a selector diverts, and here the leaf is the ordinary name "premium".
        var result = Run(Coverages, """
            [ { "command": "put", "path": "/policy/coverages/coverage[code='WA']/premium", "value": "10.00" } ]
            """);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("<code>WA</code><status>REQ</status><premium>10.00</premium>"));
            Assert.That(result, Does.Contain("<code>CASCO</code><status>REQ</status></coverage>"),
                "the untouched siblings keep exactly the children they had");
        });
    }

    [Test]
    public void Put_ObjectThroughAnIndex_IsUnchangedByTheSelectorBranch()
    {
        // An integer subscript names a position, not a selector, and keeps its own route.
        var result = XElement.Parse(Run(Coverages, """
            [ { "command": "put", "path": "/policy/coverages/coverage[2]",
                "value": { "code": "CASCO", "premium": "42.50" } } ]
            """));

        Assert.Multiple(() =>
        {
            Assert.That(result.Element("coverages")!.Elements().Count(), Is.EqualTo(3));
            Assert.That(result.ToString(SaveOptions.DisableFormatting), Does.Contain("<premium>42.50</premium>"));
        });
    }

    [Test]
    public void Put_ThroughAWildcard_WritesToEveryElement()
    {
        var result = XElement.Parse(Run(Coverages, """
            [ { "command": "put", "path": "/policy/coverages/coverage/status", "value": "OK" } ]
            """));

        Assert.That(result.Descendants("status").Select(e => e.Value),
            Is.EqualTo(new[] { "OK", "OK", "OK" }));
    }

    [Test]
    public void Put_ThroughAPredicateThatMatchesNothing_WarnsRatherThanInventingStructure()
    {
        var options = ParseOptions<XElement>.CreateDefault();
        var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
        var context = XmlExecutionContext.CreateWithNativeXPath();

        var result = engine.Execute("""
            [ { "command": "put", "path": "/policy/coverages/coverage[code='NOPE']",
                "value": { "code": "NOPE" } } ]
            """, _adapter.Parse(Coverages), context);

        // A selector describes no single structure, so there is nothing to scaffold.
        Assert.Multiple(() =>
        {
            Assert.That(_adapter.Serialize(result.Data), Does.Not.Contain("NOPE"));
            Assert.That(context.GetLogEntries().Select(e => e.Message),
                Has.Some.Contains("a selector cannot be created"));
        });
    }
}
