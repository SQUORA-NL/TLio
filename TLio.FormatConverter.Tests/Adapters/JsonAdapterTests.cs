using System.Text.Json;
using TLio.FormatConverter.Core;
using TLio.FormatConverter.Core.Exceptions;
using TLio.FormatConverter.Json;
using NUnit.Framework;

namespace TLio.FormatConverter.Tests.Adapters;

[TestFixture]
public sealed class JsonAdapterTests
{
    private JsonFormatAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new JsonFormatAdapter();

    [Test]
    public void FormatId_IsJson()
    {
        Assert.That(_adapter.FormatId, Is.EqualTo("json"));
    }

    // ── Fixture round-trip tests ─────────────────────────────────────────────

    private static IEnumerable<string> JsonFixtureDirectories()
    {
        var fixtureRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Json");
        foreach (var dir in Directory.GetDirectories(fixtureRoot))
        {
            var input = Path.Combine(dir, "input.json");
            var expected = Path.Combine(dir, "expected-roundtrip.json");
            if (File.Exists(input) && File.Exists(expected))
                yield return dir;
        }
    }

    [TestCaseSource(nameof(JsonFixtureDirectories))]
    public void JsonRoundTrip_FixtureMatchesExpected(string fixtureDir)
    {
        var input = File.ReadAllText(Path.Combine(fixtureDir, "input.json"));
        var expected = File.ReadAllText(Path.Combine(fixtureDir, "expected-roundtrip.json"));

        var im = _adapter.ToIM(input, ConversionSettings.Empty);
        var output = _adapter.FromIM(im, ConversionSettings.Empty);

        Assert.That(NormaliseJson(output), Is.EqualTo(NormaliseJson(expected)));
    }

    // ── Type mapping tests ───────────────────────────────────────────────────

    [Test]
    public void ToIM_JsonNull_ProducesScalarNull()
    {
        var im = _adapter.ToIM("null", ConversionSettings.Empty);
        var scalar = (TLio.FormatConverter.Core.Model.ScalarNode)im;
        Assert.That(scalar.Type, Is.EqualTo(TLio.FormatConverter.Core.Model.ScalarType.Null));
        Assert.That(scalar.RawValue, Is.Null);
    }

    [Test]
    public void ToIM_JsonInteger_ProducesScalarInteger()
    {
        var im = _adapter.ToIM("{\"v\":42}", ConversionSettings.Empty);
        var obj = (TLio.FormatConverter.Core.Model.ObjectNode)im;
        var scalar = (TLio.FormatConverter.Core.Model.ScalarNode)obj.Children[0];
        Assert.That(scalar.Type, Is.EqualTo(TLio.FormatConverter.Core.Model.ScalarType.Integer));
        Assert.That(scalar.RawValue, Is.EqualTo("42"));
    }

    [Test]
    public void ToIM_JsonDecimal_ProducesScalarDecimal()
    {
        var im = _adapter.ToIM("{\"v\":3.14}", ConversionSettings.Empty);
        var obj = (TLio.FormatConverter.Core.Model.ObjectNode)im;
        var scalar = (TLio.FormatConverter.Core.Model.ScalarNode)obj.Children[0];
        Assert.That(scalar.Type, Is.EqualTo(TLio.FormatConverter.Core.Model.ScalarType.Decimal));
    }

    [Test]
    public void ToIM_JsonBoolean_ProducesScalarBoolean()
    {
        var im = _adapter.ToIM("{\"flag\":true}", ConversionSettings.Empty);
        var obj = (TLio.FormatConverter.Core.Model.ObjectNode)im;
        var scalar = (TLio.FormatConverter.Core.Model.ScalarNode)obj.Children[0];
        Assert.That(scalar.Type, Is.EqualTo(TLio.FormatConverter.Core.Model.ScalarType.Boolean));
        Assert.That(scalar.RawValue, Is.EqualTo("true"));
    }

    [Test]
    public void ToIM_MalformedJson_ThrowsFormatParseException()
    {
        var ex = Assert.Throws<FormatParseException>(() => _adapter.ToIM("{bad json", ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("json"));
        Assert.That(ex.Operation, Is.EqualTo("ToIM"));
    }

    [Test]
    public void FromIM_NullScalar_WritesJsonNull()
    {
        var im = new TLio.FormatConverter.Core.Model.ScalarNode(TLio.FormatConverter.Core.Model.ScalarType.Null, null);
        var output = _adapter.FromIM(im, ConversionSettings.Empty);
        Assert.That(output, Is.EqualTo("null"));
    }

    [Test]
    public void FromIM_BooleanFalse_WritesJsonFalse()
    {
        var im = new TLio.FormatConverter.Core.Model.ScalarNode(TLio.FormatConverter.Core.Model.ScalarType.Boolean, "false");
        var output = _adapter.FromIM(im, ConversionSettings.Empty);
        Assert.That(output, Is.EqualTo("false"));
    }

    private static string NormaliseJson(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
}
