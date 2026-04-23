using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Json;

namespace TLio.Json.Tests.EdgeCases;

[TestFixture]
public class JsonEdgeCaseTests
{
    private JsonNodeAdapter _adapter = null!;
    private JsonPathItemsFetcher _fetcher = null!;

    [SetUp]
    public void SetUp()
    {
        _adapter = new JsonNodeAdapter();
        _fetcher = new JsonPathItemsFetcher();
    }

    [Test]
    public void Parse_InvalidJson_ThrowsJsonReaderException()
    {
        Assert.Throws<JsonReaderException>(() => _adapter.Parse("not json {"));
    }

    [Test]
    public void Parse_EmptyString_ReturnsNullOrThrows()
    {
        // Newtonsoft JToken.Parse("") returns null — adapter wraps it as JValue.CreateNull()
        // Either a null return or a thrown exception is acceptable behavior
        JToken? result = null;
        try { result = _adapter.Parse(string.Empty); }
        catch { return; }
        Assert.That(result == null || result.Type == JTokenType.Null, Is.True,
            "Expected null/JNull from empty string parse");
    }

    [Test]
    public void Parse_JsonNull_ReturnsJValueWithNullType()
    {
        var node = _adapter.Parse("null");
        Assert.That(node.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void Parse_EmptyObject_ReturnsJObjectWithZeroProperties()
    {
        var node = _adapter.Parse("{}");
        Assert.That(_adapter.IsObject(node), Is.True);
        Assert.That(_adapter.GetPropertyNames(node).ToList(), Is.Empty);
    }

    [Test]
    public void Parse_EmptyArray_ReturnsJArrayWithZeroElements()
    {
        var node = _adapter.Parse("[]");
        Assert.That(_adapter.IsArray(node), Is.True);
        Assert.That(_adapter.GetArrayLength(node), Is.EqualTo(0));
    }

    [Test]
    public void SelectNodes_NonExistentPath_ReturnsEmptyEnumerable()
    {
        var data = JObject.Parse("{\"x\":1}");
        var result = _fetcher.SelectNodes("$.nonexistent", data);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_EmptyObjectAnyPath_ReturnsEmptyEnumerable()
    {
        var data = JObject.Parse("{}");
        var result = _fetcher.SelectNodes("$.anything", data);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void TryGetString_JObjectNode_ReturnsNull()
    {
        var node = JObject.Parse("{\"a\":1}");
        Assert.That(_adapter.TryGetString(node), Is.Null);
    }

    [Test]
    public void TryGetBoolean_NumericToken_ReturnsNull()
    {
        var node = JValue.CreateString("42");
        Assert.That(_adapter.TryGetBoolean(node), Is.Null);
    }

    [Test]
    public void TryGetDouble_NonNumericString_ReturnsNull()
    {
        var node = new JValue("notanumber");
        Assert.That(_adapter.TryGetDouble(node), Is.Null);
    }

    [Test]
    public void GetArrayLength_OnJObject_ReturnsZero()
    {
        var node = JObject.Parse("{\"a\":1}");
        Assert.That(_adapter.GetArrayLength(node), Is.EqualTo(0));
    }

    [Test]
    public void GetPropertyNames_OnJArray_ReturnsEmpty()
    {
        var node = JArray.Parse("[1,2,3]");
        Assert.That(_adapter.GetPropertyNames(node).ToList(), Is.Empty);
    }

    [Test]
    public void GetProperty_OnJArray_ReturnsNull()
    {
        var node = JArray.Parse("[1,2,3]");
        Assert.That(_adapter.GetProperty(node, "anyKey"), Is.Null);
    }

    [Test]
    public void HasProperty_OnJArray_ReturnsFalse()
    {
        var node = JArray.Parse("[1,2,3]");
        Assert.That(_adapter.HasProperty(node, "anyKey"), Is.False);
    }
}
