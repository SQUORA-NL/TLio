using System.Text.Json;
using FormatConverter.Core;
using FormatConverter.Core.Exceptions;
using Converter = global::FormatConverter.Core.FormatConverter;
using FormatConverter.Json;
using FormatConverter.TLio;
using FormatConverter.Xml;
using FormatConverter.Yaml;
using NUnit.Framework;

namespace FormatConverter.Tests.Integration;

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

        var output = _runner.Execute("xml", input, script);

        // Result should be valid YAML (contains key: value)
        Assert.That(output, Contains.Substring("name"));
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

        // Default attributePrefix is "@" — attribute should appear as "@id"
        var output = _runner.Execute("xml", input, script);
        Assert.That(output, Contains.Substring("\"@id\""));
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

        // Final format is YAML — should contain YAML-style key: value
        Assert.That(output, Contains.Substring("key:"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FixtureDir(params string[] parts) =>
        Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "Fixtures" }.Concat(parts).ToArray());

    private static string NormJson(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
}
