using System.Text.Json;
using System.Xml;
using TLio.FormatConverter.Core;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using NUnit.Framework;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.FormatConverter.Tests.Integration;

[TestFixture]
public sealed class RoundTripTests
{
    private Converter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = new Converter();
        _converter.Register(new JsonFormatAdapter());
        _converter.Register(new XmlFormatAdapter());
        _converter.Register(new YamlFormatAdapter());
    }

    // ── Fixture-driven tests ─────────────────────────────────────────────────

    [Test]
    public void JsonXmlJson_RoundTrip_PreservesStructure()
    {
        var dir = FixtureDir("RoundTrip", "json-xml-json");
        var input = File.ReadAllText(Path.Combine(dir, "input.json"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected.json"));

        // json → xml → json
        var xml = _converter.Convert("json", input, "xml", ConversionSettings.Empty);
        var output = _converter.Convert("xml", xml, "json", ConversionSettings.Empty);

        Assert.That(NormJson(output), Is.EqualTo(NormJson(expected)));
    }

    [Test]
    public void JsonYamlJson_RoundTrip_PreservesStructure()
    {
        var dir = FixtureDir("RoundTrip", "json-yaml-json");
        var input = File.ReadAllText(Path.Combine(dir, "input.json"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected.json"));

        var yaml = _converter.Convert("json", input, "yaml", ConversionSettings.Empty);
        var output = _converter.Convert("yaml", yaml, "json", new ConversionSettings { InferTypes = true });

        Assert.That(NormJson(output), Is.EqualTo(NormJson(expected)));
    }

    [Test]
    public void XmlJsonXml_RoundTrip_PreservesAttributes()
    {
        var dir = FixtureDir("RoundTrip", "xml-json-xml");
        var input = File.ReadAllText(Path.Combine(dir, "input.xml"));

        var json = _converter.Convert("xml", input, "json", ConversionSettings.Empty);
        var output = _converter.Convert("json", json, "xml", ConversionSettings.Empty);

        // Attribute @id and element name should survive the round trip
        var outDoc = new XmlDocument();
        outDoc.LoadXml(output);
        Assert.That(outDoc.DocumentElement!.LocalName, Is.EqualTo("person"));
        Assert.That(outDoc.DocumentElement.GetAttribute("id"), Is.EqualTo("1"));
    }

    [Test]
    public void XmlYamlXml_RoundTrip_PreservesElements()
    {
        var dir = FixtureDir("RoundTrip", "xml-yaml-xml");
        var input = File.ReadAllText(Path.Combine(dir, "input.xml"));

        var yaml = _converter.Convert("xml", input, "yaml", ConversionSettings.Empty);
        var output = _converter.Convert("yaml", yaml, "xml", ConversionSettings.Empty);

        var doc = new XmlDocument();
        doc.LoadXml(output);
        Assert.That(doc.DocumentElement!.LocalName, Is.EqualTo("config"));
    }

    [Test]
    public void YamlJsonYaml_RoundTrip_PreservesKeys()
    {
        var dir = FixtureDir("RoundTrip", "yaml-json-yaml");
        var input = File.ReadAllText(Path.Combine(dir, "input.yaml"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected.yaml"));

        var json = _converter.Convert("yaml", input, "json", new ConversionSettings { InferTypes = true });
        var yaml = _converter.Convert("json", json, "yaml", ConversionSettings.Empty);

        YamlAssert.Equivalent(yaml, expected, "yaml → json → yaml");
    }

    [Test]
    public void AttributeRoundTrip_AttributesPreservedInJson()
    {
        var dir = FixtureDir("RoundTrip", "attribute-roundtrip");
        var input = File.ReadAllText(Path.Combine(dir, "input.xml"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected-json.json"));

        var json = _converter.Convert("xml", input, "json", ConversionSettings.Empty);

        Assert.That(NormJson(json), Is.EqualTo(NormJson(expected)));
    }

    // ── IM comparator ────────────────────────────────────────────────────────

    private static string FixtureDir(params string[] parts) =>
        Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "Fixtures" }.Concat(parts).ToArray());

    private static string NormJson(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
}
