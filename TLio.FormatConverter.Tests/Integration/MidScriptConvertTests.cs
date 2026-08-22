using System.Text.Json;
using System.Xml.Linq;
using TLio.FormatConverter.Core.Exceptions;
using TLio.FormatConverter.Json;
using TLio.FormatConverter;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.FormatConverter.Tests.Integration;

/// <summary>
/// <c>convert</c> in the middle of a script: commands before it, commands after it, one run.
/// </summary>
/// <remarks>
/// The runner has always been able to split a script and convert the document at each boundary.
/// What was missing was any implementation of <see cref="IFormatSectionExecutor"/>, so the
/// commands in a section were skipped without a word — a script could report success having
/// applied none of itself. These tests exist because that is the whole point of the feature.
/// </remarks>
[TestFixture]
public sealed class MidScriptConvertTests
{
    private MultiFormatScriptRunner _runner = null!;

    [SetUp]
    public void SetUp()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());

        _runner = new MultiFormatScriptRunner(converter);
        _runner.RegisterExecutor(Executor("json", JsonExecutionContext.CreateDefault));
        _runner.RegisterExecutor(Executor("xml", XmlExecutionContext.CreateWithNativeXPath));
        _runner.RegisterExecutor(Executor("yaml", YamlExecutionContext.CreateDefault));
    }

    private static ScriptEngineSectionExecutor<TNode> Executor<TNode>(
        string formatId, Func<global::TLio.Core.Models.ExecutionContext<TNode>> contextFactory)
    {
        var options = ParseOptions<TNode>.CreateDefault();
        var engine = new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider);
        return new ScriptEngineSectionExecutor<TNode>(formatId, engine, () => contextFactory());
    }

    // ── the feature ──────────────────────────────────────────────────────────

    [Test]
    public void CommandsRunOnBothSidesOfEveryBoundary()
    {
        // json → yaml → xml, with work done in all three.
        const string script = """
            [
              { "command": "add",    "path": "$.order.status", "value": "new"      },
              { "command": "convert", "to": "yaml" },
              { "command": "add",    "path": "order.source",   "value": "portal"   },
              { "command": "convert", "to": "xml" },
              { "command": "rename", "path": "/order",         "name":  "opdracht" }
            ]
            """;

        var result = _runner.Run("json", """{"order":{"id":"7"}}""", script);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Logs.Select(l => l.Message)));
        Assert.That(result.FormatId, Is.EqualTo("xml"));

        var xml = XElement.Parse(result.Document);
        Assert.Multiple(() =>
        {
            Assert.That(xml.Name.LocalName, Is.EqualTo("opdracht"), "the XML section renamed the root");
            Assert.That(xml.Element("id")?.Value, Is.EqualTo("7"), "the original data survived both hops");
            Assert.That(xml.Element("status")?.Value, Is.EqualTo("new"), "the JSON section's write survived");
            Assert.That(xml.Element("source")?.Value, Is.EqualTo("portal"), "the YAML section's write survived");
        });
    }

    [Test]
    public void EachSectionSpeaksItsOwnPathLanguage()
    {
        // $.a.b in JSON, a.b in YAML, /root/a/b in XPath — same node, three notations.
        const string script = """
            [
              { "command": "add",     "path": "$.root.a.fromJson", "value": "1" },
              { "command": "convert", "to": "yaml" },
              { "command": "add",     "path": "root.a.fromYaml",   "value": "2" },
              { "command": "convert", "to": "xml" },
              { "command": "add",     "path": "/root/a/fromXml", "value": "3" }
            ]
            """;

        var result = _runner.Run("json", """{"root":{"a":{"seed":"0"}}}""", script);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Logs.Select(l => l.Message)));

        var a = XElement.Parse(result.Document).Element("a")!;
        Assert.That(a.Elements().Select(e => e.Name.LocalName),
            Is.EquivalentTo(new[] { "seed", "fromJson", "fromYaml", "fromXml" }));
    }

    [Test]
    public void ArraysSurviveCommandsOnEitherSideOfABoundary()
    {
        const string script = """
            [
              { "command": "add",     "path": "$.order.lines[2]", "value": "C3" },
              { "command": "convert", "to": "xml" },
              { "command": "add",     "path": "/order/note",      "value": "checked" }
            ]
            """;

        var result = _runner.Run("json", """{"order":{"lines":["A1","B2"]}}""", script);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Logs.Select(l => l.Message)));

        var order = XElement.Parse(result.Document);
        Assert.Multiple(() =>
        {
            Assert.That(order.Element("lines")!.Elements("item").Select(e => e.Value),
                Is.EqualTo(new[] { "A1", "B2", "C3" }));
            Assert.That(order.Element("note")?.Value, Is.EqualTo("checked"));
        });
    }

    [Test]
    public void ConvertingBackGivesTheSameDocumentTheCommandsBuilt()
    {
        const string script = """
            [
              { "command": "add",     "path": "$.order.status", "value": "new" },
              { "command": "convert", "to": "xml"  },
              { "command": "convert", "to": "yaml" },
              { "command": "convert", "to": "json" }
            ]
            """;

        var result = _runner.Run("json", """{"order":{"id":"7"}}""", script);

        Assert.That(Norm(result.Document), Is.EqualTo(Norm("""{"order":{"id":"7","status":"new"}}""")));
    }

    [Test]
    public void EndingInYaml_ProducesYamlThatParses()
    {
        const string script = """
            [
              { "command": "add",     "path": "$.order.lines[0].qty", "value": "2" },
              { "command": "convert", "to": "yaml" },
              { "command": "add",     "path": "order.source",         "value": "portal" }
            ]
            """;

        var result = _runner.Run("json", """{"order":{"lines":[{"sku":"A1"}]}}""", script);

        var root = (YamlMappingNode)YamlAssert.ParseOrFail(result.Document, "pipeline output");
        var order = (YamlMappingNode)root.Children[new YamlScalarNode("order")];
        Assert.That(((YamlScalarNode)order.Children[new YamlScalarNode("source")]).Value, Is.EqualTo("portal"));
    }

    // ── the failure that used to be silent ───────────────────────────────────

    [Test]
    public void SectionWithNoExecutor_SaysSo_RatherThanSkippingTheCommands()
    {
        var bare = new MultiFormatScriptRunner(Registered());
        const string script = """[{ "command": "set", "path": "$.a", "value": "1" }]""";

        var ex = Assert.Throws<SectionExecutorNotRegisteredException>(
            () => bare.Run("json", """{"a":"0"}""", script));

        Assert.That(ex!.FormatId, Is.EqualTo("json"));
        Assert.That(ex.Message, Does.Contain("RegisterExecutor"));
    }

    [Test]
    public void ConversionOnlyScript_NeedsNoExecutorAtAll()
    {
        var bare = new MultiFormatScriptRunner(Registered());

        var result = bare.Run("json", """{"a":"0"}""", """[{"command":"convert","to":"xml"}]""");

        Assert.That(result.Document, Is.EqualTo("<a>0</a>"));
        Assert.That(result.FormatId, Is.EqualTo("xml"));
    }

    [Test]
    public void FailingCommand_IsReportedWithItsLog()
    {
        // 'set' never builds a path, so this is a no-op with a warning rather than a write.
        const string script = """
            [
              { "command": "convert", "to": "xml" },
              { "command": "set", "path": "/a/missing/deep", "value": "x" }
            ]
            """;

        var result = _runner.Run("json", """{"a":{"b":"1"}}""", script);

        Assert.That(result.Logs, Is.Not.Empty, "the section's log travels back with the result");
    }

    private static Converter Registered()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        return converter;
    }

    private static string Norm(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
}
