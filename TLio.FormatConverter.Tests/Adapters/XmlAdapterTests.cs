using System.Xml;
using TLio.FormatConverter.Core;
using TLio.FormatConverter.Core.Exceptions;
using TLio.FormatConverter.Core.Model;
using TLio.FormatConverter.Xml;
using NUnit.Framework;

namespace TLio.FormatConverter.Tests.Adapters;

[TestFixture]
public sealed class XmlAdapterTests
{
    private XmlFormatAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new XmlFormatAdapter();

    [Test]
    public void FormatId_IsXml()
    {
        Assert.That(_adapter.FormatId, Is.EqualTo("xml"));
    }

    // ── Fixture round-trip tests ─────────────────────────────────────────────

    private static IEnumerable<string> XmlFixtureDirectories()
    {
        var fixtureRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Xml");
        foreach (var dir in Directory.GetDirectories(fixtureRoot))
        {
            var input = Path.Combine(dir, "input.xml");
            var expected = Path.Combine(dir, "expected-roundtrip.xml");
            if (File.Exists(input) && File.Exists(expected))
                yield return dir;
        }
    }

    [TestCaseSource(nameof(XmlFixtureDirectories))]
    public void XmlRoundTrip_FixtureMatchesExpected(string fixtureDir)
    {
        var input = File.ReadAllText(Path.Combine(fixtureDir, "input.xml"));
        var expected = File.ReadAllText(Path.Combine(fixtureDir, "expected-roundtrip.xml"));

        var im = _adapter.ToIM(input, ConversionSettings.Empty);
        var output = _adapter.FromIM(im, ConversionSettings.Empty);

        Assert.That(NormaliseXml(output), Is.EqualTo(NormaliseXml(expected)));
    }

    // ── Settings tests ───────────────────────────────────────────────────────

    [Test]
    public void XmlAttributes_SurviveJsonRoundTrip_WithDefaultPrefix()
    {
        var xml = "<item id=\"42\" />";
        var im = _adapter.ToIM(xml, ConversionSettings.Empty);

        Assert.That(im.Metadata.ContainsKey("@id"), Is.True, "Attribute should be in metadata with '@' prefix");
        Assert.That(im.Metadata["@id"], Is.EqualTo("42"));
    }

    [Test]
    public void AttributePrefix_Override_AppliedPerCommand()
    {
        var xml = "<item id=\"42\" />";
        var settings = new ConversionSettings { AttributePrefix = "attr_" };
        var im = _adapter.ToIM(xml, settings);

        Assert.That(im.Metadata.ContainsKey("attr_id"), Is.True);
        Assert.That(im.Metadata["attr_id"], Is.EqualTo("42"));
    }

    [Test]
    public void NamespaceDeclarations_PreservedInMetadata()
    {
        var xml = "<root xmlns:ns=\"http://example.com/ns\"><ns:child>value</ns:child></root>";
        var im = _adapter.ToIM(xml, ConversionSettings.Empty);
        Assert.That(im.Metadata.ContainsKey("xmlns:ns"), Is.True);
        Assert.That(im.Metadata["xmlns:ns"], Is.EqualTo("http://example.com/ns"));
    }

    [Test]
    public void InferTypes_True_CoercesIntegerString()
    {
        var xml = "<data><count>42</count></data>";
        var settings = new ConversionSettings { InferTypes = true };
        var im = _adapter.ToIM(xml, settings);

        var obj = (ObjectNode)im;
        var child = (ScalarNode)obj.Children[0];
        Assert.That(child.Type, Is.EqualTo(ScalarType.Integer));
    }

    [Test]
    public void InferTypes_True_CoercesBoolean()
    {
        var xml = "<data><flag>true</flag></data>";
        var settings = new ConversionSettings { InferTypes = true };
        var im = _adapter.ToIM(xml, settings);

        var obj = (ObjectNode)im;
        var child = (ScalarNode)obj.Children[0];
        Assert.That(child.Type, Is.EqualTo(ScalarType.Boolean));
    }

    [Test]
    public void CdataSection_PreservesMetadata_WhenCdataAsTextFalse()
    {
        var xml = "<data><![CDATA[Some content]]></data>";
        var im = _adapter.ToIM(xml, ConversionSettings.Empty);

        Assert.That(im.Metadata.ContainsKey(NodeMetadata.CdataKey), Is.True);
        Assert.That(im.Metadata[NodeMetadata.CdataKey], Is.EqualTo("true"));
    }

    [Test]
    public void CdataSection_NoCdataMetadata_WhenCdataAsTextTrue()
    {
        var xml = "<data><![CDATA[Some content]]></data>";
        var settings = new ConversionSettings { CdataAsText = true };
        var im = _adapter.ToIM(xml, settings);

        Assert.That(im.Metadata.ContainsKey(NodeMetadata.CdataKey), Is.False);
    }

    [Test]
    public void MixedContent_ProducesMixedContentNode()
    {
        var xml = "<p>Hello <b>world</b> end</p>";
        var im = _adapter.ToIM(xml, ConversionSettings.Empty);

        Assert.That(im, Is.InstanceOf<MixedContentNode>());
    }

    [Test]
    public void ToIM_MalformedXml_ThrowsFormatParseException()
    {
        var ex = Assert.Throws<FormatParseException>(() => _adapter.ToIM("<broken>", ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("xml"));
        Assert.That(ex.Operation, Is.EqualTo("ToIM"));
    }

    private static string NormaliseXml(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc.OuterXml;
    }
}
