using System.Xml.Linq;
using TLio.FormatConverter.Json;
using TLio.FormatConverter;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using NUnit.Framework;
using TLio.Client;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.FormatConverter.Tests.Integration;

/// <summary>
/// <c>convert</c> is a command like any other in all three notations.
/// </summary>
/// <remarks>
/// <para>
/// The boundary split used to parse every script as JSON, so an XML or YAML script containing
/// <c>convert</c> was never seen to cross a boundary at all. It went to a plain engine instead,
/// where the command can only report that it converted nothing — the notation a script is
/// written in decided whether a feature existed.
/// </para>
/// <para>
/// The split now reads the script in its own notation and hands each section back in that same
/// notation, so nothing is translated. That puts a requirement on the host: the engine behind a
/// section executor has to know the notation, which is what the last test here is about.
/// </para>
/// </remarks>
[TestFixture]
public sealed class ConvertNotationTests
{
    private static MultiFormatScriptRunner Runner(bool registerNotations = true)
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());

        var runner = new MultiFormatScriptRunner(converter);
        runner.RegisterExecutor(Executor("json", JsonExecutionContext.CreateDefault, registerNotations));
        runner.RegisterExecutor(Executor("xml", XmlExecutionContext.CreateWithNativeXPath, registerNotations));
        runner.RegisterExecutor(Executor("yaml", YamlExecutionContext.CreateDefault, registerNotations));
        return runner;
    }

    private static ScriptEngineSectionExecutor<TNode> Executor<TNode>(
        string formatId,
        Func<global::TLio.Core.Models.ExecutionContext<TNode>> contextFactory,
        bool registerNotations)
    {
        var options = ParseOptions<TNode>.CreateDefault();
        var engine = new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider);
        if (registerNotations) engine.UseXmlScripts().UseYamlScripts();
        return new ScriptEngineSectionExecutor<TNode>(formatId, engine, () => contextFactory());
    }

    // The same script three ways. Paths follow the *document*, so they change at the boundary
    // in all three — JSONPath before it, XPath after — while the notation stays put.
    private const string JsonScript = """
        [
          { "command": "add",    "path": "$.order.status", "value": "new"      },
          { "command": "convert", "to": "xml" },
          { "command": "rename", "path": "/order",         "name":  "opdracht" }
        ]
        """;

    private const string XmlScript = """
        <script>
          <add path="$.order.status">new</add>
          <convert to="xml"/>
          <rename path="/order" name="opdracht"/>
        </script>
        """;

    private const string YamlScript = """
        - command: add
          path: $.order.status
          value: new
        - command: convert
          to: xml
        - command: rename
          path: /order
          name: opdracht
        """;

    [TestCase(JsonScript, TestName = "Boundary is found in the JSON notation")]
    [TestCase(XmlScript, TestName = "Boundary is found in the XML notation")]
    [TestCase(YamlScript, TestName = "Boundary is found in the YAML notation")]
    public void CrossesAFormatBoundary_FindsConvertInEveryNotation(string script) =>
        Assert.That(MultiFormatScriptRunner.CrossesAFormatBoundary(script), Is.True);

    [TestCase(JsonScript, TestName = "JSON notation crosses the boundary")]
    [TestCase(XmlScript, TestName = "XML notation crosses the boundary")]
    [TestCase(YamlScript, TestName = "YAML notation crosses the boundary")]
    public void EveryNotation_RunsCommandsOnBothSides(string script)
    {
        var result = Runner().Run("json", """{"order":{"id":"7"}}""", script);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Logs.Select(l => l.Message)));
        Assert.That(result.FormatId, Is.EqualTo("xml"));

        var xml = XElement.Parse(result.Document);
        Assert.Multiple(() =>
        {
            Assert.That(xml.Name.LocalName, Is.EqualTo("opdracht"), "the command after the boundary ran");
            Assert.That(xml.Element("status")?.Value, Is.EqualTo("new"), "the command before it ran");
            Assert.That(xml.Element("id")?.Value, Is.EqualTo("7"), "the original data survived the hop");
        });
    }

    [Test]
    public void CrossesAFormatBoundary_IsFalseForAScriptWithoutConvert() =>
        Assert.Multiple(() =>
        {
            Assert.That(MultiFormatScriptRunner.CrossesAFormatBoundary(
                """<script><add path="/a/b">1</add></script>"""), Is.False);
            Assert.That(MultiFormatScriptRunner.CrossesAFormatBoundary(
                "- command: add\n  path: $.a\n  value: 1"), Is.False);
        });

    [TestCase("not-a-script!@#", TestName = "bare scalar")]
    [TestCase("{\"a\":1}", TestName = "JSON object rather than an array")]
    [TestCase("<html><body/></html>", TestName = "XML that is not a script")]
    [TestCase("", TestName = "empty")]
    public void CrossesAFormatBoundary_IsFalseRatherThanThrowing_ForTextThatIsNotAScript(string text) =>
        Assert.That(MultiFormatScriptRunner.CrossesAFormatBoundary(text), Is.False);

    [Test]
    public void SettingsAreReadFromEveryNotation()
    {
        // inferTypes is a JSON bool, an XML child element and a YAML scalar. One reader takes all
        // three, so a setting cannot work in one notation and be silently ignored in another.
        const string xmlScript = """
            <script>
              <convert to="json"><settings><inferTypes>true</inferTypes></settings></convert>
            </script>
            """;
        const string yamlScript = """
            - command: convert
              to: json
              settings:
                inferTypes: true
            """;

        Assert.Multiple(() =>
        {
            Assert.That(Runner().Run("xml", "<order><id>7</id></order>", xmlScript).Document,
                Does.Contain("\"id\":7"), "XML notation honoured inferTypes");
            Assert.That(Runner().Run("xml", "<order><id>7</id></order>", yamlScript).Document,
                Does.Contain("\"id\":7"), "YAML notation honoured inferTypes");
        });
    }

    [Test]
    public void ASectionWhoseNotationTheEngineCannotRead_FailsLoudlyInsteadOfPassingThrough()
    {
        // The host built the section engines without UseXmlScripts(), so the XML section parses
        // to nothing. Reporting success here would apply none of the script while saying it
        // worked — the same silent-skip this whole seam exists to prevent.
        var result = Runner(registerNotations: false).Run("json", """{"order":{"id":"7"}}""", XmlScript);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(string.Join(" ", result.Logs.Select(l => l.Message)),
                Does.Contain("UseXmlScripts"), "the log says how to fix it");
        });
    }
}
