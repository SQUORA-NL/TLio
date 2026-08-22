using System.Text.Json;
using System.Xml;
using TLio.FormatConverter.Core;
using TLio.FormatConverter.Core.Exceptions;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;
using TLio.FormatConverter.Json;
using TLio.FormatConverter;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using NUnit.Framework;

namespace TLio.FormatConverter.Tests.Integration;

[TestFixture]
public sealed class PipelineTests
{
    private Converter _converter = null!;
    private MultiFormatScriptRunner _runner = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = new Converter();
        _converter.Register(new JsonFormatAdapter());
        _converter.Register(new XmlFormatAdapter());
        _converter.Register(new YamlFormatAdapter());
        _runner = new MultiFormatScriptRunner(_converter);
    }

    // ── Fixture-driven pipeline tests ────────────────────────────────────────

    [Test]
    public void XmlToJson_Pipeline_ProducesExpectedJson()
    {
        var dir = FixtureDir("Pipeline", "xml-to-json");
        var input = File.ReadAllText(Path.Combine(dir, "input.xml"));
        var script = File.ReadAllText(Path.Combine(dir, "script.json"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected.json"));

        var output = _runner.Execute("xml", input, script);

        Assert.That(NormJson(output), Is.EqualTo(NormJson(expected)));
    }

    [Test]
    public void XmlJsonYaml_Pipeline_ProducesYaml()
    {
        var dir = FixtureDir("Pipeline", "xml-json-yaml");
        var input = File.ReadAllText(Path.Combine(dir, "input.xml"));
        var script = File.ReadAllText(Path.Combine(dir, "script.json"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected.yaml")).TrimEnd();

        var output = _runner.Execute("xml", input, script).TrimEnd();

        YamlAssert.Equivalent(output, expected, "xml → json → yaml");
    }

    [Test]
    public void JsonYamlXml_Pipeline_CarriesArraysAllTheWayThrough()
    {
        // The pipeline this feature exists for. It used to throw at the second boundary because
        // the YAML written at the first one could not be read back.
        var script = """[{"command":"convert","to":"yaml"},{"command":"convert","to":"xml"}]""";
        var input = """{"order":{"id":"7","lines":[{"sku":"A1","qty":"2"},{"sku":"B2","qty":"1"}]}}""";

        var output = _runner.Execute("json", input, script);

        var doc = new XmlDocument();
        Assert.DoesNotThrow(() => doc.LoadXml(output), "the pipeline must end in well-formed XML");
        Assert.That(doc.DocumentElement!.LocalName, Is.EqualTo("order"));
        Assert.That(doc.SelectNodes("//*[local-name()='sku']")!.Count, Is.EqualTo(2),
            "both line items survive both boundaries");
    }

    [Test]
    public void Pipeline_SettingsOverride_AppliesCustomAttributePrefix()
    {
        var dir = FixtureDir("Pipeline", "settings-override");
        var input = File.ReadAllText(Path.Combine(dir, "input.xml"));
        var script = File.ReadAllText(Path.Combine(dir, "script.json"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected.json"));

        var output = _runner.Execute("xml", input, script);

        Assert.That(NormJson(output), Is.EqualTo(NormJson(expected)));
    }

    [Test]
    public void Pipeline_InferTypes_ProducesTypedValues()
    {
        var dir = FixtureDir("Pipeline", "infer-types");
        var input = File.ReadAllText(Path.Combine(dir, "input.xml"));
        var script = File.ReadAllText(Path.Combine(dir, "script.json"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected.json"));

        var output = _runner.Execute("xml", input, script);

        Assert.That(NormJson(output), Is.EqualTo(NormJson(expected)));
    }

    // ── Inline behaviour tests ────────────────────────────────────────────────

    [Test]
    public void ConvertCommand_UnregisteredFormat_ThrowsAtBoundaryNotAtLoad()
    {
        var script = "[{\"command\":\"convert\",\"to\":\"unknown-format\"}]";
        var input = "<root />";

        Assert.Throws<FormatNotRegisteredException>(
            () => _runner.Execute("xml", input, script));
    }

    [Test]
    public void ConvertCommand_DefaultSettings_MatchDocumentedDefaults()
    {
        // A script with convert command and no settings block should use defaults
        var script = "[{\"command\":\"convert\",\"to\":\"json\"}]";
        var input = "<item id=\"1\" />";

        // Default attributePrefix is "@" — attribute key and value must survive conversion
        var output = _runner.Execute("xml", input, script);
        Assert.That(NormJson(output), Is.EqualTo(NormJson("{\"item\":{\"@id\":\"1\"}}")));
    }

    [Test]
    public void MultiFormatScriptRunner_NoConvertCommands_ExecutesAsSingleSection()
    {
        var script = "[]";
        var input = "{\"key\":\"value\"}";

        // No convert commands → document passes through unchanged
        var output = _runner.Execute("json", input, script);
        Assert.That(NormJson(output), Is.EqualTo(NormJson(input)));
    }

    [Test]
    public void MultiFormatScriptRunner_TwoConvertCommands_ProducesThreeSections()
    {
        // xml → json → yaml (three sections total)
        var script = "[{\"command\":\"convert\",\"to\":\"json\"},{\"command\":\"convert\",\"to\":\"yaml\"}]";
        var input = "<root><key>value</key></root>";

        var output = _runner.Execute("xml", input, script);

        // Final format is YAML — verify structure and values are preserved
        YamlAssert.Equivalent(output, "root:\n  key: value", "xml → json → yaml");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FixtureDir(params string[] parts) =>
        Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "Fixtures" }.Concat(parts).ToArray());

    private static string NormJson(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
}
