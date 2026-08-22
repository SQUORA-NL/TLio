using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TLio.FormatConverter.Core;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using NUnit.Framework;

namespace TLio.FormatConverter.Tests.Integration;

[TestFixture]
[Category("Performance")]
public sealed class PerformanceTests
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
    public void LargeJson_ToXml_CompletesWithinTwoSeconds()
    {
        var json = BuildLargeJson(targetSizeBytes: 1_000_000);

        var sw = Stopwatch.StartNew();
        var xml = _converter.Convert("json", json, "xml", ConversionSettings.Empty);
        sw.Stop();

        Assert.That(xml, Is.Not.Empty);
        Assert.That(sw.Elapsed.TotalSeconds, Is.LessThan(2.0),
            $"Conversion took {sw.Elapsed.TotalSeconds:F2}s — must be < 2s");
    }

    [Test]
    public void LargeJson_RoundTrip_CompletesWithinTwoSeconds()
    {
        var json = BuildLargeJson(targetSizeBytes: 500_000);

        var sw = Stopwatch.StartNew();
        var xml = _converter.Convert("json", json, "xml", ConversionSettings.Empty);
        var result = _converter.Convert("xml", xml, "json", ConversionSettings.Empty);
        sw.Stop();

        Assert.That(result, Is.Not.Empty);
        Assert.That(sw.Elapsed.TotalSeconds, Is.LessThan(2.0),
            $"Round-trip took {sw.Elapsed.TotalSeconds:F2}s — must be < 2s");
    }

    private static string BuildLargeJson(int targetSizeBytes)
    {
        var sb = new StringBuilder();
        using var writer = new Utf8JsonWriter(new MemoryStream());

        // Build a flat object with many string properties until we reach size
        var items = new List<(string key, string value)>();
        var size = 2; // for "[]"
        var index = 0;
        while (size < targetSizeBytes)
        {
            var key = $"prop_{index}";
            var value = $"value_{index}_some_longer_text";
            size += key.Length + value.Length + 6; // quotes, colon, comma
            items.Add((key, value));
            index++;
        }

        using var stream = new MemoryStream();
        using var jsonWriter = new Utf8JsonWriter(stream);
        jsonWriter.WriteStartObject();
        jsonWriter.WriteStartArray("items");
        foreach (var (key, value) in items)
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString(key, value);
            jsonWriter.WriteEndObject();
        }
        jsonWriter.WriteEndArray();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
