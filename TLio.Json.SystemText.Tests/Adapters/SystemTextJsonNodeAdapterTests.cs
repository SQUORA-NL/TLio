using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Json.SystemText;

namespace TLio.Json.SystemText.Tests.Adapters;

[TestFixture]
public class SystemTextJsonNodeAdapterTests
{
    private SystemTextJsonNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new SystemTextJsonNodeAdapter();

    // ── Type queries ──────────────────────────────────────────────────────────

    [Test]
    public void IsObject_JsonObject_ReturnsTrue()
    {
        var node = JsonNode.Parse("{\"a\":1}")!;
        Assert.That(_adapter.IsObject(node), Is.True);
    }

    [Test]
    public void IsObject_JsonArray_ReturnsFalse()
    {
        var node = JsonNode.Parse("[1,2]")!;
        Assert.That(_adapter.IsObject(node), Is.False);
    }

    [Test]
    public void IsArray_JsonArray_ReturnsTrue()
    {
        var node = JsonNode.Parse("[1,2,3]")!;
        Assert.That(_adapter.IsArray(node), Is.True);
    }

    [Test]
    public void IsArray_JsonObject_ReturnsFalse()
    {
        var node = JsonNode.Parse("{\"x\":1}")!;
        Assert.That(_adapter.IsArray(node), Is.False);
    }

    [Test]
    public void IsPrimitive_StringValue_ReturnsTrue()
    {
        var node = JsonNode.Parse("\"hello\"")!;
        Assert.That(_adapter.IsPrimitive(node), Is.True);
    }

    [Test]
    public void IsPrimitive_JsonObject_ReturnsFalse()
    {
        var node = JsonNode.Parse("{}")!;
        Assert.That(_adapter.IsPrimitive(node), Is.False);
    }

    // ── Property access ───────────────────────────────────────────────────────

    [Test]
    public void GetProperty_ExistingKey_ReturnsNode()
    {
        var node = JsonNode.Parse("{\"city\":\"Amsterdam\"}")!;
        var city = _adapter.GetProperty(node, "city");
        Assert.That(city, Is.Not.Null);
        Assert.That(city!.GetValue<string>(), Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void GetProperty_MissingKey_ReturnsNull()
    {
        var node = JsonNode.Parse("{\"city\":\"Amsterdam\"}")!;
        var result = _adapter.GetProperty(node, "country");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HasProperty_ExistingKey_ReturnsTrue()
    {
        var node = JsonNode.Parse("{\"name\":\"Alice\"}")!;
        Assert.That(_adapter.HasProperty(node, "name"), Is.True);
    }

    [Test]
    public void HasProperty_MissingKey_ReturnsFalse()
    {
        var node = JsonNode.Parse("{\"name\":\"Alice\"}")!;
        Assert.That(_adapter.HasProperty(node, "age"), Is.False);
    }

    [Test]
    public void GetPropertyNames_ReturnsAllKeys()
    {
        var node = JsonNode.Parse("{\"a\":1,\"b\":2,\"c\":3}")!;
        var names = _adapter.GetPropertyNames(node).ToList();
        Assert.That(names, Is.EquivalentTo(new[] { "a", "b", "c" }));
    }

    // ── Value access ──────────────────────────────────────────────────────────

    [Test]
    public void TryGetString_StringValue_ReturnsString()
    {
        var node = JsonNode.Parse("\"hello\"")!;
        Assert.That(_adapter.TryGetString(node), Is.EqualTo("hello"));
    }

    [Test]
    public void TryGetBoolean_TrueValue_ReturnsTrue()
    {
        var node = JsonNode.Parse("true")!;
        Assert.That(_adapter.TryGetBoolean(node), Is.True);
    }

    [Test]
    public void TryGetBoolean_FalseValue_ReturnsFalse()
    {
        var node = JsonNode.Parse("false")!;
        Assert.That(_adapter.TryGetBoolean(node), Is.False);
    }

    [Test]
    public void TryGetDouble_NumericValue_ReturnsParsedDouble()
    {
        var node = JsonNode.Parse("3.14")!;
        Assert.That(_adapter.TryGetDouble(node), Is.EqualTo(3.14).Within(0.0001));
    }

    // ── Array operations ──────────────────────────────────────────────────────

    [Test]
    public void GetArrayLength_ReturnsElementCount()
    {
        var node = JsonNode.Parse("[1,2,3,4]")!;
        Assert.That(_adapter.GetArrayLength(node), Is.EqualTo(4));
    }

    [Test]
    public void GetArrayElement_ReturnsCorrectElement()
    {
        var node = JsonNode.Parse("[\"first\",\"second\",\"third\"]")!;
        var second = _adapter.GetArrayElement(node, 1);
        Assert.That(second.GetValue<string>(), Is.EqualTo("second"));
    }

    [Test]
    public void GetArrayElements_ReturnsAllElements()
    {
        var node = JsonNode.Parse("[10,20,30]")!;
        var elements = _adapter.GetArrayElements(node).ToList();
        Assert.That(elements, Has.Count.EqualTo(3));
    }

    // ── Clone & equality ──────────────────────────────────────────────────────

    [Test]
    public void DeepClone_ProducesEqualButDistinctNode()
    {
        var original = JsonNode.Parse("{\"name\":\"Alice\",\"age\":30}")!;
        var clone = _adapter.DeepClone(original);
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(_adapter.DeepEquals(original, clone), Is.True);
    }

    [Test]
    public void DeepClone_MutatingCloneDoesNotAffectOriginal()
    {
        var original = JsonNode.Parse("{\"name\":\"Alice\"}")!;
        var clone = _adapter.DeepClone(original);
        clone.AsObject()["name"] = "Bob";
        Assert.That(original["name"]!.GetValue<string>(), Is.EqualTo("Alice"));
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    [Test]
    public void Parse_ValidJson_ReturnsParsedNode()
    {
        var node = _adapter.Parse("{\"city\":\"Paris\"}");
        Assert.That(_adapter.IsObject(node), Is.True);
        Assert.That(_adapter.GetProperty(node, "city")!.GetValue<string>(), Is.EqualTo("Paris"));
    }

    [Test]
    public void Parse_EmptyObject_ReturnsEmptyJsonObject()
    {
        var node = _adapter.Parse("{}");
        Assert.That(_adapter.IsObject(node), Is.True);
        Assert.That(_adapter.GetPropertyNames(node).ToList(), Is.Empty);
    }

    [Test]
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        Assert.That(() => _adapter.Parse("not json"), Throws.InstanceOf<System.Text.Json.JsonException>());
    }
}
