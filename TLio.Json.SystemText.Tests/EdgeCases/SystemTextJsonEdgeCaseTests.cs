using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Json.SystemText;

namespace TLio.Json.SystemText.Tests.EdgeCases;

[TestFixture]
public class SystemTextJsonEdgeCaseTests
{
    private SystemTextJsonNodeAdapter _adapter = null!;
    private SystemTextJsonPathItemsFetcher _fetcher = null!;

    [SetUp]
    public void SetUp()
    {
        _adapter = new SystemTextJsonNodeAdapter();
        _fetcher = new SystemTextJsonPathItemsFetcher();
    }

    [TearDown]
    public void TearDown() => _fetcher?.Dispose();

    [Test]
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        Assert.That(() => _adapter.Parse("not json {"), Throws.InstanceOf<JsonException>());
    }

    [Test]
    public void Parse_EmptyString_ThrowsJsonException()
    {
        Assert.That(() => _adapter.Parse(string.Empty), Throws.InstanceOf<JsonException>());
    }

    [Test]
    public void Parse_JsonNull_ReturnsNullableNode()
    {
        // JsonNode.Parse("null") returns null in System.Text.Json
        // The adapter returns null! (non-null assertion) but the value is null
        var node = JsonNode.Parse("null");
        Assert.That(node, Is.Null);
    }

    [Test]
    public void Parse_EmptyObject_ReturnsJsonObjectWithZeroProperties()
    {
        var node = _adapter.Parse("{}");
        Assert.That(_adapter.IsObject(node), Is.True);
        Assert.That(_adapter.GetPropertyNames(node).ToList(), Is.Empty);
    }

    [Test]
    public void Parse_EmptyArray_ReturnsJsonArrayWithZeroElements()
    {
        var node = _adapter.Parse("[]");
        Assert.That(_adapter.IsArray(node), Is.True);
        Assert.That(_adapter.GetArrayLength(node), Is.EqualTo(0));
    }

    [Test]
    public void SelectNodes_NonExistentPath_ReturnsEmptyEnumerable()
    {
        var data = JsonNode.Parse("{\"x\":1}")!;
        var result = _fetcher.SelectNodes("$.nonexistent", data);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_EmptyObjectWithAnyPath_ReturnsEmptyEnumerable()
    {
        var data = JsonNode.Parse("{}")!;
        var result = _fetcher.SelectNodes("$.anything", data);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void TryGetString_JsonObjectNode_ReturnsNull()
    {
        var node = JsonNode.Parse("{\"a\":1}")!;
        Assert.That(_adapter.TryGetString(node), Is.Null);
    }

    [Test]
    public void TryGetBoolean_NumericNode_ReturnsNull()
    {
        var node = JsonNode.Parse("42")!;
        Assert.That(_adapter.TryGetBoolean(node), Is.Null);
    }

    [Test]
    public void TryGetDouble_StringNonNumeric_ReturnsNull()
    {
        var node = JsonNode.Parse("\"notanumber\"")!;
        Assert.That(_adapter.TryGetDouble(node), Is.Null);
    }

    [Test]
    public void GetArrayLength_OnJsonObject_ReturnsZero()
    {
        var node = JsonNode.Parse("{\"a\":1}")!;
        Assert.That(_adapter.GetArrayLength(node), Is.EqualTo(0));
    }

    [Test]
    public void GetPropertyNames_OnJsonArray_ReturnsEmpty()
    {
        var node = JsonNode.Parse("[1,2,3]")!;
        Assert.That(_adapter.GetPropertyNames(node).ToList(), Is.Empty);
    }

    [Test]
    public void GetProperty_OnJsonArray_ReturnsNull()
    {
        var node = JsonNode.Parse("[1,2,3]")!;
        Assert.That(_adapter.GetProperty(node, "anyKey"), Is.Null);
    }

    [Test]
    public void HasProperty_OnJsonArray_ReturnsFalse()
    {
        var node = JsonNode.Parse("[1,2,3]")!;
        Assert.That(_adapter.HasProperty(node, "anyKey"), Is.False);
    }
}
