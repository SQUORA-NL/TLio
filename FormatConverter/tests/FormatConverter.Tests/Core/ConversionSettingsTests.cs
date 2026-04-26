using FormatConverter.Core;
using NUnit.Framework;

namespace FormatConverter.Tests.Core;

[TestFixture]
public sealed class ConversionSettingsTests
{
    [Test]
    public void Empty_HasAllDefaults()
    {
        var s = ConversionSettings.Empty;
        Assert.That(s.TextProperty, Is.EqualTo("#text"));
        Assert.That(s.AttributePrefix, Is.EqualTo("@"));
        Assert.That(s.NamespacePrefix, Is.EqualTo("xmlns:"));
        Assert.That(s.InferTypes, Is.False);
        Assert.That(s.CdataAsText, Is.False);
        Assert.That(s.FlattenAnchors, Is.True);
    }

    [Test]
    public void PartialOverride_PreservesUnspecifiedDefaults()
    {
        var s = new ConversionSettings { AttributePrefix = "attr_" };
        Assert.That(s.AttributePrefix, Is.EqualTo("attr_"));
        Assert.That(s.TextProperty, Is.EqualTo("#text"));
        Assert.That(s.NamespacePrefix, Is.EqualTo("xmlns:"));
        Assert.That(s.InferTypes, Is.False);
        Assert.That(s.CdataAsText, Is.False);
        Assert.That(s.FlattenAnchors, Is.True);
    }

    [Test]
    public void Empty_IsSingleton_AlwaysReturnsSameInstance()
    {
        var first = ConversionSettings.Empty;
        var second = ConversionSettings.Empty;
        Assert.That(first, Is.SameAs(second));
    }

    [Test]
    public void AllProperties_CanBeOverridden()
    {
        var s = new ConversionSettings
        {
            TextProperty = "text",
            AttributePrefix = "a:",
            NamespacePrefix = "ns:",
            InferTypes = true,
            CdataAsText = true,
            FlattenAnchors = false,
        };
        Assert.That(s.TextProperty, Is.EqualTo("text"));
        Assert.That(s.AttributePrefix, Is.EqualTo("a:"));
        Assert.That(s.NamespacePrefix, Is.EqualTo("ns:"));
        Assert.That(s.InferTypes, Is.True);
        Assert.That(s.CdataAsText, Is.True);
        Assert.That(s.FlattenAnchors, Is.False);
    }
}
