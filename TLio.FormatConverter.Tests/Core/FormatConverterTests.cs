using TLio.FormatConverter.Core;
using TLio.FormatConverter.Core.Exceptions;
using TLio.FormatConverter.Core.Model;
using NUnit.Framework;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.FormatConverter.Tests.Core;

[TestFixture]
public sealed class FormatConverterTests
{
    private Converter _converter = null!;

    [SetUp]
    public void SetUp() => _converter = new Converter();

    [Test]
    public void ToIM_ThrowsFormatNotRegistered_WhenUnknownFormat()
    {
        var ex = Assert.Throws<FormatNotRegisteredException>(
            () => _converter.ToIM("unknown", "{}", ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("unknown"));
    }

    [Test]
    public void FromIM_ThrowsFormatNotRegistered_WhenUnknownFormat()
    {
        var im = new ScalarNode(ScalarType.Null, null);
        var ex = Assert.Throws<FormatNotRegisteredException>(
            () => _converter.FromIM("unknown", im, ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("unknown"));
    }

    [Test]
    public void Register_SecondAdapter_ReplacesFirst()
    {
        var first = new StubAdapter("echo", im => "first");
        var second = new StubAdapter("echo", im => "second");
        _converter.Register(first);
        _converter.Register(second);

        var result = _converter.FromIM("echo", new ScalarNode(ScalarType.String, "x"), ConversionSettings.Empty);
        Assert.That(result, Is.EqualTo("second"));
    }

    [Test]
    public void Convert_PassesSettingsToBothAdapters()
    {
        ConversionSettings? capturedToIM = null;
        ConversionSettings? capturedFromIM = null;

        var src = new StubAdapter("src",
            fromIM: im => "out",
            toIM: (s, settings) => { capturedToIM = settings; return new ScalarNode(ScalarType.String, "x"); });
        var tgt = new StubAdapter("tgt",
            fromIMWithSettings: (im, settings) => { capturedFromIM = settings; return "done"; });

        _converter.Register(src);
        _converter.Register(tgt);

        var customSettings = new ConversionSettings { InferTypes = true };
        _converter.Convert("src", "input", "tgt", customSettings);

        Assert.That(capturedToIM, Is.SameAs(customSettings));
        Assert.That(capturedFromIM, Is.SameAs(customSettings));
    }

    [Test]
    public void FormatId_Lookup_IsCaseInsensitive()
    {
        _converter.Register(new StubAdapter("JSON"));

        Assert.DoesNotThrow(() => _converter.ToIM("json", "{}", ConversionSettings.Empty));
        Assert.DoesNotThrow(() => _converter.ToIM("JSON", "{}", ConversionSettings.Empty));
        Assert.DoesNotThrow(() => _converter.ToIM("Json", "{}", ConversionSettings.Empty));
    }

    [Test]
    public void RegisteredFormats_ReflectsRegisteredAdapters()
    {
        _converter.Register(new StubAdapter("json"));
        _converter.Register(new StubAdapter("xml"));

        Assert.That(_converter.RegisteredFormats, Is.EquivalentTo(new[] { "json", "xml" }));
    }

    [Test]
    public void FormatNotRegisteredException_ListsRegisteredIds()
    {
        _converter.Register(new StubAdapter("json"));
        _converter.Register(new StubAdapter("xml"));

        var ex = Assert.Throws<FormatNotRegisteredException>(
            () => _converter.ToIM("yaml", "", ConversionSettings.Empty));
        Assert.That(ex.RegisteredIds, Is.EquivalentTo(new[] { "json", "xml" }));
    }

    // ── helper ───────────────────────────────────────────────────────────────

    private sealed class StubAdapter : IFormatAdapter
    {
        private readonly Func<string, ConversionSettings, IntermediateNode> _toIM;
        private readonly Func<IntermediateNode, ConversionSettings, string> _fromIM;

        public string FormatId { get; }

        public StubAdapter(string formatId,
            Func<IntermediateNode, string>? fromIM = null,
            Func<string, ConversionSettings, IntermediateNode>? toIM = null,
            Func<IntermediateNode, ConversionSettings, string>? fromIMWithSettings = null)
        {
            FormatId = formatId;
            _toIM = toIM ?? ((s, _) => new ScalarNode(ScalarType.String, s));
            _fromIM = fromIMWithSettings ?? ((im, _) => fromIM?.Invoke(im) ?? string.Empty);
        }

        public IntermediateNode ToIM(string source, ConversionSettings settings) => _toIM(source, settings);
        public string FromIM(IntermediateNode root, ConversionSettings settings) => _fromIM(root, settings);
    }
}
