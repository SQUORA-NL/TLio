using System.Globalization;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.ETL;
using TLio.Extensions.Looping;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Parity.Tests;

/// <summary>The outcome of running one fixture against one format.</summary>
public sealed record RunOutcome(bool Success, string Actual, string Expected, IReadOnlyList<string> Log)
{
    public bool Matches => Actual == Expected;

    public string Report(string format) =>
        $"""
         {format} result does not match the expected document.
           expected: {Expected}
           actual:   {Actual}
           log:
             {string.Join("\n    ", Log)}
         """;
}

/// <summary>
/// Runs a parity fixture through one format, in that format's own script notation, and renders
/// both the actual and the expected document in a single comparable form.
/// </summary>
public static class FormatRunners
{
    /// <summary>
    /// The same command and function set for every format — every optional pack included, so a
    /// fixture that calls =concat(), =sum() or =maxDate() exercises them on all three adapters
    /// rather than only on the one whose test project happens to reference the pack.
    /// </summary>
    public static ParseOptions<TNode> Options<TNode>()
    {
        var options = ParseOptions<TNode>.CreateDefault();
        options.FunctionsProvider.RegisterText<TNode>();
        options.FunctionsProvider.RegisterMath<TNode>();
        options.FunctionsProvider.RegisterTimeDate<TNode>();
        options.CommandsProvider.RegisterETL<TNode>();
        options.CommandsProvider.RegisterLooping<TNode>();
        return options;
    }

    public static RunOutcome RunJson(ParityFixture fixture)
    {
        var options = Options<JToken>();
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var context = JsonExecutionContext.CreateDefault();

        var result = engine.Execute(fixture.Script, fixture.Input.DeepClone(), context);

        return new RunOutcome(
            result.Success,
            Render(result.Data),
            Render(fixture.Expected("json")),
            Log(context));
    }

    public static RunOutcome RunXml(ParityFixture fixture)
    {
        var options = Options<XElement>();
        var adapter = new XmlNodeAdapter();
        var parser  = new XmlScriptParser<XElement>(
            options.CommandsProvider, options.FunctionsProvider, adapter);
        var context = XmlExecutionContext.CreateWithSlashPaths();

        var data   = adapter.Parse(CanonicalShape.ToXmlDocument(fixture.Input));
        var script = parser.ParseScript(CanonicalShape.ToXmlScript(fixture.Script));
        var result = script.Execute(data, context);

        var expected = adapter.Parse(CanonicalShape.ToXmlDocument(fixture.Expected("xml")));

        return new RunOutcome(
            result.Success,
            RenderXml(result.Data),
            RenderXml(expected),
            Log(context));
    }

    public static RunOutcome RunYaml(ParityFixture fixture)
    {
        var options = Options<YamlNode>();
        var context = YamlExecutionContext.CreateDefault();
        var adapter = context.NodeAdapter;
        var parser  = new YamlScriptParser<YamlNode>(
            options.CommandsProvider, options.FunctionsProvider, adapter);

        // The fixture's script text unchanged — JSON is a subset of YAML, and the two share a
        // path language, so there is nothing to translate.
        var data   = adapter.Parse(CanonicalShape.ToYamlDocument(fixture.Input));
        var script = parser.ParseScript(fixture.Script);
        var result = script.Execute(data, context);

        var expected = adapter.Parse(CanonicalShape.ToYamlDocument(fixture.Expected("yaml")));

        return new RunOutcome(
            result.Success,
            RenderYaml(result.Data),
            RenderYaml(expected),
            Log(context));
    }

    // ── Rendering ─────────────────────────────────────────────────────────────
    //
    // Both sides of a comparison go through the same renderer, so a match means the documents
    // agree on structure and text — not on how a particular library chose to print them.

    public static string RenderForComparison(JToken token) => Render(token);
    public static string RenderXmlForComparison(XElement e) => RenderXml(e);
    public static string RenderYamlForComparison(YamlNode n) => RenderYaml(n);

    private static string Render(JToken token) => Normalise(token).ToString(Newtonsoft.Json.Formatting.None);

    /// <summary>
    /// Collapses the distinctions XML and YAML cannot carry, so one expectation can be written
    /// for all three: every scalar is compared as its text, and null / "" / {} / [] — which XML
    /// writes identically as an empty element — compare equal.
    /// </summary>
    private static JToken Normalise(JToken token) => token switch
    {
        JObject o when o.Properties().Any() =>
            new JObject(o.Properties().Select(p => new JProperty(p.Name, Normalise(p.Value)))),

        JArray a when a.Count > 0 => new JArray(a.Select(Normalise)),

        // Empty object, empty array, empty string and null are one value here.
        JObject or JArray => JValue.CreateNull(),
        _ when token.Type == JTokenType.Null => JValue.CreateNull(),
        _ when token.Type == JTokenType.Boolean => new JValue((bool)token! ? "true" : "false"),
        _ when ScalarText(token).Length == 0 => JValue.CreateNull(),
        _ => new JValue(ScalarText(token))
    };

    /// <summary>
    /// A scalar's text, culture-invariantly. <see cref="JValue.ToString()"/> with no arguments
    /// formats a double via <see cref="CultureInfo.CurrentCulture"/>, so on a machine whose
    /// region uses a comma decimal separator the sweep comparison rendered "2,5" — matching
    /// nothing the fixtures ever recorded, which are all invariant.
    /// </summary>
    private static string ScalarText(JToken token) =>
        ((JValue)token).ToString(null, CultureInfo.InvariantCulture);

    private static string RenderXml(XElement element) => Render(XmlToJson(element));

    /// <summary>Reads an XML document back into the JSON shape it stands for.</summary>
    private static JToken XmlToJson(XElement element)
    {
        if (!element.HasElements)
            return element.Value.Length == 0 ? JValue.CreateNull() : new JValue(element.Value);

        var children = element.Elements().ToList();
        var names = children.Select(c => c.Name.LocalName).Distinct().ToList();

        // Same rule the adapter uses, so the test reads the document the way TLio does.
        var isArray = names.Count == 1 &&
                      (children.Count > 1 || names[0] == XmlNodeAdapter.DefaultItemName);

        if (isArray)
            return new JArray(children.Select(XmlToJson));

        var obj = new JObject();
        foreach (var child in children)
            obj[child.Name.LocalName] = XmlToJson(child);
        return obj;
    }

    private static string RenderYaml(YamlNode node) => Render(YamlToJson(node));

    private static JToken YamlToJson(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode map:
            {
                var obj = new JObject();
                foreach (var (key, value) in map.Children)
                    obj[((YamlScalarNode)key).Value!] = YamlToJson(value);
                return obj;
            }

            case YamlSequenceNode seq:
                return new JArray(seq.Children.Select(YamlToJson));

            case YamlScalarNode scalar:
                // Mirrors YamlNodeAdapter.IsNull.
                return scalar.Value is null or "" or "~" or "null"
                    ? JValue.CreateNull()
                    : new JValue(scalar.Value);

            default:
                return JValue.CreateNull();
        }
    }

    private static List<string> Log<TNode>(IExecutionContext<TNode> context) =>
        context.GetLogEntries().Select(e => $"[{e.Level}] {e.Group}: {e.Message}").ToList();
}
