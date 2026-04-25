using FormatConverter.Core;
using FormatConverter.Json;
using FormatConverter.Tests.Stubs;
using Converter = global::FormatConverter.Core.FormatConverter;
using FormatConverter.Xml;
using NUnit.Framework;

namespace FormatConverter.Tests.Integration;

[TestFixture]
public sealed class ExtensibilityTests
{
    private Converter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = new Converter();
        _converter.Register(new JsonFormatAdapter());
        _converter.Register(new XmlFormatAdapter());
    }

    [Test]
    public void EchoAdapter_RegisteredAlongsideExisting_WorksAsSourceAndTarget()
    {
        _converter.Register(new EchoFormatAdapter());

        // Echo as source: wraps input in ScalarNode
        var im = _converter.ToIM("echo", "hello world", ConversionSettings.Empty);
        var scalar = im as global::FormatConverter.Core.Model.ScalarNode;
        Assert.That(scalar, Is.Not.Null);
        Assert.That(scalar!.RawValue, Is.EqualTo("hello world"));

        // Echo as target: returns the scalar value unchanged
        var output = _converter.FromIM("echo", im, ConversionSettings.Empty);
        Assert.That(output, Is.EqualTo("hello world"));
    }

    [Test]
    public void EchoAdapter_AddedWithZeroChanges_ToExistingAdapters()
    {
        // Verify existing adapters still work after registering echo
        _converter.Register(new EchoFormatAdapter());

        Assert.DoesNotThrow(() =>
        {
            var jsonIm = _converter.ToIM("json", "{\"key\":\"val\"}", ConversionSettings.Empty);
            _converter.FromIM("json", jsonIm, ConversionSettings.Empty);
        });
    }

    [Test]
    public void ExistingAdapterTests_StillPass_AfterEchoRegistered()
    {
        _converter.Register(new EchoFormatAdapter());

        // JSON round-trip still works
        var json = "{\"name\":\"Alice\"}";
        var output = _converter.Convert("json", json, "json", ConversionSettings.Empty);
        Assert.That(output, Is.EqualTo(json));
    }

    [Test]
    public void EchoAdapter_IgnoresAllSettings()
    {
        _converter.Register(new EchoFormatAdapter());

        var customSettings = new ConversionSettings
        {
            AttributePrefix = "custom_",
            InferTypes = true,
            FlattenAnchors = false,
        };

        var im = _converter.ToIM("echo", "test", customSettings);
        var output = _converter.FromIM("echo", im, customSettings);
        Assert.That(output, Is.EqualTo("test"));
    }
}
