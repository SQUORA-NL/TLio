using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Parity.Tests;

/// <summary>
/// <c>resolve</c> writes the value it looked up, in every format.
///
/// <para>
/// A resolve setting addresses the matched reference entry with <c>@.field</c>. That marker
/// belongs to the script, not to the data — it is written the same way whatever the document is —
/// and <c>keyPath</c> and <c>targetPath</c> always honoured it, because they go through the
/// adapter's own property access. <c>value</c> did not: it is a value template, and the path
/// language got it verbatim. JSONPath happens to spell the marker the same way, so JSON and YAML
/// worked and XML did not — <c>@.label</c> reached XPath as an attribute test with an illegal
/// name, and the command warned and wrote nothing.
/// </para>
/// <para>
/// The sweep did not catch it: it wrote <c>sourcePath</c>, a key the converter does not read, so
/// <c>resolve</c> wrote nothing in all three formats and the three agreed on that.
/// </para>
/// </summary>
[TestFixture]
public class ResolveValueParityTests
{
    /// <summary>
    /// One lookup, spelled for one path language. The settings are identical in all three —
    /// only the two absolute paths differ, because those address the document.
    /// </summary>
    private static string Script(string target, string references) => $$"""
        [
          { "command": "resolve", "path": "{{target}}",
            "resolveSettings": [
              { "referencesCollectionPath": "{{references}}",
                "resolveKeys": [ { "keyPath": "@.code", "referenceKeyPath": "@.code" } ],
                "values": [
                  { "targetPath": "@.label",     "value": "@.label" },
                  { "targetPath": "@.nested",    "value": "@.detail.tier" },
                  { "targetPath": "@.sourceKind", "value": "lookup" }
                ] } ] }
        ]
        """;

    private const string JsonDocument = """
        { "ref": { "code": "B" },
          "table": [
            { "code": "A", "label": "Alpha", "detail": { "tier": "one" } },
            { "code": "B", "label": "Beta",  "detail": { "tier": "two" } }
          ] }
        """;

    private const string XmlDocument =
        "<root>" +
          "<ref><code>B</code></ref>" +
          "<table>" +
            "<item><code>A</code><label>Alpha</label><detail><tier>one</tier></detail></item>" +
            "<item><code>B</code><label>Beta</label><detail><tier>two</tier></detail></item>" +
          "</table>" +
        "</root>";

    private const string YamlDocument = """
        ref:
          code: B
        table:
          - code: A
            label: Alpha
            detail:
              tier: one
          - code: B
            label: Beta
            detail:
              tier: two
        """;

    [Test]
    public void Json_WritesTheResolvedValue()
    {
        var options = FormatRunners.Options<JToken>();
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var context = JsonExecutionContext.CreateDefault();

        var result = engine.Execute(Script("$.ref", "$.table[*]"), JToken.Parse(JsonDocument), context);

        Assert.That(result.Success, Is.True);
        AssertResolved(result.Data["ref"]!["label"]?.ToString(),
                       result.Data["ref"]!["nested"]?.ToString(),
                       result.Data["ref"]!["sourceKind"]?.ToString());
    }

    [Test]
    public void Xml_WritesTheResolvedValue()
    {
        var options = FormatRunners.Options<XElement>();
        var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
        var context = XmlExecutionContext.CreateWithNativeXPath();
        var adapter = new XmlNodeAdapter();

        var result = engine.Execute(Script("/root/ref", "/root/table/item"), adapter.Parse(XmlDocument), context);

        Assert.That(result.Success, Is.True,
            string.Join("; ", context.GetLogEntries().Select(e => e.Message)));

        var reference = result.Data.Element("ref")!;
        AssertResolved(reference.Element("label")?.Value,
                       reference.Element("nested")?.Value,
                       reference.Element("sourceKind")?.Value);
    }

    [Test]
    public void Yaml_WritesTheResolvedValue()
    {
        var options = FormatRunners.Options<YamlNode>();
        var engine  = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
        var context = YamlExecutionContext.CreateDefault();

        var result = engine.Execute(Script("$.ref", "$.table[*]"),
            context.NodeAdapter.Parse(YamlDocument), context);

        Assert.That(result.Success, Is.True);

        var reference = (YamlMappingNode)((YamlMappingNode)result.Data)["ref"];
        AssertResolved(Scalar(reference, "label"), Scalar(reference, "nested"), Scalar(reference, "sourceKind"));
    }

    private static string? Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var v) ? ((YamlScalarNode)v).Value : null;

    /// <summary>
    /// The three answers every format has to give: a value copied off the matched entry, one
    /// reached through a nested property of it, and a literal that is not a path at all.
    /// </summary>
    private static void AssertResolved(string? label, string? nested, string? sourceKind) =>
        Assert.Multiple(() =>
        {
            Assert.That(label, Is.EqualTo("Beta"), "@.label reads the matched entry, not the first one");
            Assert.That(nested, Is.EqualTo("two"), "@.detail.tier walks into the matched entry");
            Assert.That(sourceKind, Is.EqualTo("lookup"), "a literal value is still written as a literal");
        });
}
