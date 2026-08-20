using System.Text.Json;
using FormatConverter.Core;
using FormatConverter.Json;
using FormatConverter.Xml;
using FormatConverter.Yaml;
using NUnit.Framework;
using Converter = global::FormatConverter.Core.FormatConverter;

namespace FormatConverter.Tests.Integration;

/// <summary>
/// What XML carries that JSON and YAML have no room for — attributes, namespace declarations, and
/// text sitting alongside either — and the settings that name how they are spelled elsewhere.
/// </summary>
/// <remarks>
/// <c>textProperty</c> and <c>namespacePrefix</c> were declared, documented and tested for their
/// default values, but no adapter read them. An element with both an attribute and text lost the
/// attribute; a namespace declaration did not survive a round trip at all.
/// </remarks>
[TestFixture]
public sealed class AttributeCarriageTests
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

    // ── textProperty ─────────────────────────────────────────────────────────

    [Test]
    public void AttributeBesideText_SurvivesJson()
    {
        const string xml = """<price currency="EUR">9.99</price>""";

        var json = _converter.Convert("xml", xml, "json", ConversionSettings.Empty);

        Assert.That(NormJson(json), Is.EqualTo(NormJson("""{"price":{"@currency":"EUR","#text":"9.99"}}""")));
        Assert.That(_converter.Convert("json", json, "xml", ConversionSettings.Empty), Is.EqualTo(xml));
    }

    [Test]
    public void AttributeBesideText_SurvivesYaml()
    {
        const string xml = """<price currency="EUR">9.99</price>""";

        var yaml = _converter.Convert("xml", xml, "yaml", ConversionSettings.Empty);

        YamlAssert.Equivalent(yaml, "price:\n  '@currency': EUR\n  '#text': '9.99'");
        Assert.That(_converter.Convert("yaml", yaml, "xml", ConversionSettings.Empty), Is.EqualTo(xml));
    }

    [Test]
    public void TextProperty_CustomName_IsUsedOnBothSides()
    {
        var settings = new ConversionSettings { TextProperty = "$value" };
        const string xml = """<price currency="EUR">9.99</price>""";

        var json = _converter.Convert("xml", xml, "json", settings);

        Assert.That(NormJson(json), Is.EqualTo(NormJson("""{"price":{"@currency":"EUR","$value":"9.99"}}""")));
        Assert.That(_converter.Convert("json", json, "xml", settings), Is.EqualTo(xml));
    }

    [Test]
    public void PlainScalarElement_NeedsNoTextWrapper()
    {
        // Only an element that also carries attributes has to spend a key on its own value.
        var json = _converter.Convert("xml", "<price>9.99</price>", "json", ConversionSettings.Empty);

        Assert.That(NormJson(json), Is.EqualTo(NormJson("""{"price":"9.99"}""")));
    }

    [Test]
    public void NamedScalarRoot_KeepsItsName()
    {
        // A scalar document element used to serialise as a bare "9.99", losing the name with it.
        Assert.Multiple(() =>
        {
            Assert.That(NormJson(_converter.Convert("xml", "<price>9.99</price>", "json", ConversionSettings.Empty)),
                Is.EqualTo(NormJson("""{"price":"9.99"}""")));
            Assert.That(_converter.Convert("xml", "<price>9.99</price>", "xml", ConversionSettings.Empty),
                Is.EqualTo("<price>9.99</price>"));
        });
    }

    // ── namespacePrefix ──────────────────────────────────────────────────────

    [Test]
    public void NamespaceDeclaration_SurvivesAJsonRoundTrip()
    {
        // This used to throw on the way back: the declaration was dropped in JSON, so the
        // prefixed element name had no namespace to resolve against.
        const string xml = """<o xmlns:n="urn:x"><n:c>1</n:c></o>""";

        var json = _converter.Convert("xml", xml, "json", ConversionSettings.Empty);

        Assert.That(NormJson(json), Is.EqualTo(NormJson("""{"o":{"xmlns:n":"urn:x","n:c":"1"}}""")));
        Assert.That(_converter.Convert("json", json, "xml", ConversionSettings.Empty), Is.EqualTo(xml));
    }

    [Test]
    public void NamespaceDeclaration_SurvivesAYamlRoundTrip()
    {
        const string xml = """<o xmlns:n="urn:x"><n:c>1</n:c></o>""";

        var yaml = _converter.Convert("xml", xml, "yaml", ConversionSettings.Empty);

        Assert.That(_converter.Convert("yaml", yaml, "xml", ConversionSettings.Empty), Is.EqualTo(xml));
    }

    [Test]
    public void DefaultNamespace_SurvivesAJsonRoundTrip()
    {
        const string xml = """<o xmlns="urn:d"><c>1</c></o>""";

        var json = _converter.Convert("xml", xml, "json", ConversionSettings.Empty);

        Assert.That(NormJson(json), Is.EqualTo(NormJson("""{"o":{"xmlns":"urn:d","c":"1"}}""")));
        Assert.That(_converter.Convert("json", json, "xml", ConversionSettings.Empty),
            Does.Contain("xmlns=\"urn:d\""));
    }

    [Test]
    public void NamespacePrefix_CustomName_IsUsedOnBothSides()
    {
        var settings = new ConversionSettings { NamespacePrefix = "ns:" };
        const string xml = """<o xmlns:n="urn:x"><n:c>1</n:c></o>""";

        var json = _converter.Convert("xml", xml, "json", settings);

        Assert.That(NormJson(json), Is.EqualTo(NormJson("""{"o":{"ns:n":"urn:x","n:c":"1"}}""")),
            "the metadata key takes the configured prefix");
        Assert.That(_converter.Convert("json", json, "xml", settings), Is.EqualTo(xml),
            "the XML attribute is always xmlns: — that is the format, not a convention");
    }

    // ── the two prefixes together ────────────────────────────────────────────

    [Test]
    public void AttributePrefixThatCollidesWithNamespaces_StillWritesTheDeclaration()
    {
        // With attributePrefix "x", the key "xmlns:n" also matches the attribute rule. Namespaces
        // are recognised first so the declaration is not written out as an ordinary attribute.
        var settings = new ConversionSettings { AttributePrefix = "x" };
        const string xml = """<o xmlns:n="urn:x"><n:c>1</n:c></o>""";

        var json = _converter.Convert("xml", xml, "json", settings);

        Assert.That(_converter.Convert("json", json, "xml", settings), Is.EqualTo(xml));
    }

    private static string NormJson(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
}
