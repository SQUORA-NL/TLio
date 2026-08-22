using System.Text.Json;
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
public sealed class ErrorHandlingTests
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

    [Test]
    public void ToIM_MalformedJson_ThrowsFormatParseException()
    {
        var ex = Assert.Throws<FormatParseException>(
            () => _converter.ToIM("json", "{invalid}", ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("json"));
        Assert.That(ex.Operation, Is.EqualTo("ToIM"));
    }

    [Test]
    public void ToIM_MalformedXml_ThrowsFormatParseException()
    {
        var ex = Assert.Throws<FormatParseException>(
            () => _converter.ToIM("xml", "<unclosed", ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("xml"));
        Assert.That(ex.Operation, Is.EqualTo("ToIM"));
    }

    [Test]
    public void ToIM_MalformedYaml_ThrowsFormatParseException()
    {
        var ex = Assert.Throws<FormatParseException>(
            () => _converter.ToIM("yaml", "key: [unclosed", ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("yaml"));
        Assert.That(ex.Operation, Is.EqualTo("ToIM"));
    }

    [Test]
    public void FormatParseException_IncludesFormatIdAndOperation()
    {
        var ex = Assert.Throws<FormatParseException>(
            () => _converter.ToIM("json", "!!!invalid!!!", ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("json"));
        Assert.That(ex.Operation, Is.EqualTo("ToIM"));
        Assert.That(ex.Message, Does.Contain("json"));
    }

    [Test]
    public void FormatNotRegisteredException_ListsRegisteredIds()
    {
        var ex = Assert.Throws<FormatNotRegisteredException>(
            () => _converter.ToIM("csv", "", ConversionSettings.Empty));
        Assert.That(ex.RegisteredIds, Is.Not.Empty);
        Assert.That(ex.RegisteredIds, Contains.Item("json"));
    }

    [Test]
    public void ConvertCommand_UnknownSettingKey_DoesNotThrow()
    {
        // Unknown keys in settings should be silently ignored, not throw
        var script = "[{\"command\":\"convert\",\"to\":\"json\",\"settings\":{\"unknownKey\":\"someValue\"}}]";
        var runner = new MultiFormatScriptRunner(_converter);

        Assert.DoesNotThrow(() => runner.Execute("xml", "<root />", script));
    }

    [Test]
    public void ScalarNode_NullValue_RequiresNullType()
    {
        Assert.Throws<ArgumentException>(
            () => new global::TLio.FormatConverter.Core.Model.ScalarNode(global::TLio.FormatConverter.Core.Model.ScalarType.String, null));
    }

    [Test]
    public void ScalarNode_NullType_AllowsNullValue()
    {
        Assert.DoesNotThrow(
            () => new global::TLio.FormatConverter.Core.Model.ScalarNode(global::TLio.FormatConverter.Core.Model.ScalarType.Null, null));
    }
}
