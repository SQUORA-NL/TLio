using System.Text.Json;
using FormatConverter.Core;
using FormatConverter.Core.Exceptions;
using FormatConverter.Json;
using FormatConverter.Xml;
using FormatConverter.Yaml;
using NUnit.Framework;
using Converter = global::FormatConverter.Core.FormatConverter;

namespace FormatConverter.Tests.Integration;

/// <summary>
/// The settings that exist because XML has no single right answer: what an array item is called,
/// how a sequence is spelled, how nothing is written down, and what to do with a name XML cannot
/// hold.
/// </summary>
[TestFixture]
public sealed class ShapeSettingsTests
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

    // ── arrayItemName ────────────────────────────────────────────────────────

    [Test]
    public void ArrayItemName_Default_IsItem()
    {
        var xml = ToXml("""{"order":{"tags":["a","b"]}}""", ConversionSettings.Empty);

        Assert.That(xml, Is.EqualTo("<order><tags><item>a</item><item>b</item></tags></order>"));
    }

    [Test]
    public void ArrayItemName_Custom_NamesItemsThatArriveWithoutOne()
    {
        var settings = new ConversionSettings { ArrayItemName = "line" };

        var xml = ToXml("""{"order":{"lines":[{"sku":"A1"}]}}""", settings);

        Assert.That(xml, Is.EqualTo("<order><lines><line><sku>A1</sku></line></lines></order>"));
        Assert.That(ToJson(xml, settings), Is.EqualTo(Norm("""{"order":{"lines":[{"sku":"A1"}]}}""")),
            "the custom name is also what makes an array of one recognisable on the way back");
    }

    [Test]
    public void ItemName_AlreadyInUse_IsKeptOnAnXmlToXmlTrip()
    {
        // "Adding to an array keeps the item name already in use" — an array of <order> stays an
        // array of <order> rather than being renamed to the default.
        const string xml = "<orders><order>A</order><order>B</order></orders>";

        Assert.That(_converter.Convert("xml", xml, "xml", ConversionSettings.Empty), Is.EqualTo(xml));
    }

    [Test]
    public void ItemName_IsLostThroughJson_BecauseJsonHasNoRoomForIt()
    {
        // Documented consequence rather than a defect: an array in JSON is just an array.
        const string xml = "<orders><order>A</order><order>B</order></orders>";

        var json = _converter.Convert("xml", xml, "json", ConversionSettings.Empty);

        Assert.That(json, Is.EqualTo("""{"orders":["A","B"]}"""));
        Assert.That(_converter.Convert("json", json, "xml", ConversionSettings.Empty),
            Is.EqualTo("<orders><item>A</item><item>B</item></orders>"));
    }

    // ── arrayHandling ────────────────────────────────────────────────────────

    [Test]
    public void ArrayHandling_Repeated_ReadsAndWritesTheLegacyShape()
    {
        var settings = new ConversionSettings { ArrayHandling = ArrayHandling.Repeated };
        const string legacy = "<order><lines><sku>A1</sku></lines><lines><sku>B2</sku></lines></order>";

        var json = ToJson(legacy, settings);

        Assert.That(json, Is.EqualTo(Norm("""{"order":{"lines":[{"sku":"A1"},{"sku":"B2"}]}}""")));
        Assert.That(_converter.Convert("json", json, "xml", settings), Is.EqualTo(legacy));
    }

    [Test]
    public void ArrayHandling_Wrapped_ReadsTheLegacyShapeAsAnArrayOfTheParent()
    {
        // Why the legacy shape is not the default: read canonically, the repeated children make
        // the *parent* the array.
        const string legacy = "<order><lines><sku>A1</sku></lines><lines><sku>B2</sku></lines></order>";

        Assert.That(ToJson(legacy, ConversionSettings.Empty),
            Is.EqualTo(Norm("""{"order":[{"sku":"A1"},{"sku":"B2"}]}""")));
    }

    // ── nullRepresentation ───────────────────────────────────────────────────

    [Test]
    public void NullRepresentation_Empty_WritesAnEmptyElement()
    {
        Assert.That(ToXml("""{"order":{"note":null}}""", ConversionSettings.Empty),
            Is.EqualTo("<order><note /></order>"));
    }

    [Test]
    public void NullRepresentation_XsiNil_SaysNullAndOnlyNull()
    {
        var settings = new ConversionSettings { NullRepresentation = NullRepresentation.XsiNil };

        var xml = ToXml("""{"order":{"note":null}}""", settings);

        Assert.That(xml, Does.Contain("nil=\"true\""));
        Assert.That(xml, Does.Contain("http://www.w3.org/2001/XMLSchema-instance"));
    }

    [Test]
    public void XsiNil_IsHonouredWhenReading_WhicheverSettingIsInForce()
    {
        const string xml = """<order><note xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:nil="true">ignored</note></order>""";

        Assert.That(ToJson(xml, ConversionSettings.Empty), Is.EqualTo(Norm("""{"order":{"note":null}}""")));
    }

    // ── nameSanitization ─────────────────────────────────────────────────────

    [Test]
    public void NameSanitization_Sanitize_IsQuietAndLossy()
    {
        var xml = ToXml("""{"o":{"first name":"a","2nd":"b"}}""", ConversionSettings.Empty);

        Assert.That(xml, Is.EqualTo("<o><first_name>a</first_name><_2nd>b</_2nd></o>"));
    }

    [Test]
    public void NameSanitization_Error_NamesTheOffendingKey()
    {
        var settings = new ConversionSettings { NameSanitization = NameSanitization.Error };

        var ex = Assert.Throws<FormatParseException>(() => ToXml("""{"o":{"first name":"a"}}""", settings));

        Assert.That(ex!.Message, Does.Contain("first name"));
    }

    [Test]
    public void NameSanitization_Escape_SurvivesTheReturnTrip()
    {
        var settings = new ConversionSettings { NameSanitization = NameSanitization.Escape };
        const string json = """{"o":{"first name":"a","2nd":"b"}}""";

        var xml = ToXml(json, settings);

        Assert.That(xml, Does.Contain("first_x0020_name"));
        Assert.That(ToJson(xml, settings), Is.EqualTo(Norm(json)), "the original names come back");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private string ToXml(string json, ConversionSettings settings) =>
        _converter.Convert("json", json, "xml", settings);

    private string ToJson(string xml, ConversionSettings settings) =>
        Norm(_converter.Convert("xml", xml, "json", settings));

    private static string Norm(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
}
