using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Json;

namespace TLio.Json.Tests.AdapterTests;

/// <summary>
/// Tests for JsonNodeAdapter — all INodeAdapter&lt;JToken&gt; members.
/// Written post-implementation as an Article VI correction (Phase 2A).
/// </summary>
[TestFixture]
public class JsonNodeAdapterTests
{
    private JsonNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new JsonNodeAdapter();

    // ── Type queries ──────────────────────────────────────────────────────────

    [Test]
    public void IsObject_ReturnsTrueForJObject()
        => Assert.That(_adapter.IsObject(JToken.Parse("{\"a\":1}")), Is.True);

    [Test]
    public void IsObject_ReturnsFalseForArray()
        => Assert.That(_adapter.IsObject(JToken.Parse("[1,2]")), Is.False);

    [Test]
    public void IsArray_ReturnsTrueForJArray()
        => Assert.That(_adapter.IsArray(JToken.Parse("[1,2,3]")), Is.True);

    [Test]
    public void IsArray_ReturnsFalseForObject()
        => Assert.That(_adapter.IsArray(JToken.Parse("{}")), Is.False);

    [Test]
    public void IsPrimitive_ReturnsTrueForJValue()
        => Assert.That(_adapter.IsPrimitive(new JValue("hello")), Is.True);

    [Test]
    public void IsPrimitive_ReturnsFalseForObject()
        => Assert.That(_adapter.IsPrimitive(new JObject()), Is.False);

    [Test]
    public void IsNull_ReturnsTrueForJsonNull()
        => Assert.That(_adapter.IsNull(JValue.CreateNull()), Is.True);

    [Test]
    public void IsNull_ReturnsFalseForString()
        => Assert.That(_adapter.IsNull(new JValue("text")), Is.False);

    // ── Object operations ─────────────────────────────────────────────────────

    [Test]
    public void HasProperty_ReturnsTrueWhenPresent()
    {
        var obj = JObject.Parse("{\"x\": 1}");
        Assert.That(_adapter.HasProperty(obj, "x"), Is.True);
    }

    [Test]
    public void HasProperty_ReturnsFalseWhenAbsent()
    {
        var obj = JObject.Parse("{\"x\": 1}");
        Assert.That(_adapter.HasProperty(obj, "y"), Is.False);
    }

    [Test]
    public void GetProperty_ReturnsValue()
    {
        var obj = JObject.Parse("{\"name\": \"Alice\"}");
        Assert.That(_adapter.GetProperty(obj, "name")?.Value<string>(), Is.EqualTo("Alice"));
    }

    [Test]
    public void GetProperty_ReturnsNullForMissing()
    {
        var obj = JObject.Parse("{\"name\": \"Alice\"}");
        Assert.That(_adapter.GetProperty(obj, "missing"), Is.Null);
    }

    [Test]
    public void SetProperty_AddsNewProperty()
    {
        var obj = new JObject();
        _adapter.SetProperty(obj, "key", new JValue(42));
        Assert.That(obj["key"]?.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void SetProperty_OverwritesExistingProperty()
    {
        var obj = JObject.Parse("{\"key\": 1}");
        _adapter.SetProperty(obj, "key", new JValue(99));
        Assert.That(obj["key"]?.Value<int>(), Is.EqualTo(99));
    }

    [Test]
    public void RemoveProperty_RemovesExistingProperty()
    {
        var obj = JObject.Parse("{\"a\": 1, \"b\": 2}");
        _adapter.RemoveProperty(obj, "a");
        Assert.That(obj.ContainsKey("a"), Is.False);
        Assert.That(obj.ContainsKey("b"), Is.True);
    }

    [Test]
    public void GetPropertyNames_ReturnsAllNames()
    {
        var obj = JObject.Parse("{\"x\": 1, \"y\": 2, \"z\": 3}");
        var names = _adapter.GetPropertyNames(obj).ToList();
        Assert.That(names, Is.EquivalentTo(new[] { "x", "y", "z" }));
    }

    // ── Array operations ──────────────────────────────────────────────────────

    [Test]
    public void AppendToArray_AddsElement()
    {
        var arr = new JArray();
        _adapter.AppendToArray(arr, new JValue(1));
        _adapter.AppendToArray(arr, new JValue(2));
        Assert.That(arr.Count, Is.EqualTo(2));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void InsertIntoArray_InsertsAtIndex()
    {
        var arr = JArray.Parse("[1, 3]");
        _adapter.InsertIntoArray(arr, 1, new JValue(2));
        Assert.That(arr.Count, Is.EqualTo(3));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void RemoveFromArray_RemovesAtIndex()
    {
        var arr = JArray.Parse("[1, 2, 3]");
        _adapter.RemoveFromArray(arr, 1);
        Assert.That(arr.Count, Is.EqualTo(2));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void GetArrayLength_ReturnsCount()
    {
        var arr = JArray.Parse("[1, 2, 3]");
        Assert.That(_adapter.GetArrayLength(arr), Is.EqualTo(3));
    }

    [Test]
    public void GetArrayElement_ReturnsElement()
    {
        var arr = JArray.Parse("[\"a\", \"b\", \"c\"]");
        Assert.That(_adapter.GetArrayElement(arr, 1).Value<string>(), Is.EqualTo("b"));
    }

    [Test]
    public void GetArrayElements_ReturnsAllElements()
    {
        var arr = JArray.Parse("[10, 20, 30]");
        var elements = _adapter.GetArrayElements(arr).Select(e => e.Value<int>()).ToList();
        Assert.That(elements, Is.EqualTo(new[] { 10, 20, 30 }));
    }

    // ── Node creation ─────────────────────────────────────────────────────────

    [Test]
    public void CreateNull_IsNullType() => Assert.That(_adapter.IsNull(_adapter.CreateNull()), Is.True);

    [Test]
    public void CreateObject_IsObject() => Assert.That(_adapter.IsObject(_adapter.CreateObject()), Is.True);

    [Test]
    public void CreateArray_IsArray() => Assert.That(_adapter.IsArray(_adapter.CreateArray()), Is.True);

    [Test]
    public void CreateString_HasCorrectValue()
        => Assert.That(_adapter.CreateString("hi").Value<string>(), Is.EqualTo("hi"));

    [Test]
    public void CreateNumber_HasCorrectValue()
        => Assert.That(_adapter.CreateNumber(3.14).Value<double>(), Is.EqualTo(3.14).Within(0.0001));

    [Test]
    public void CreateBoolean_HasCorrectValue()
        => Assert.That(_adapter.CreateBoolean(true).Value<bool>(), Is.True);

    [Test]
    public void CreateValue_NullProducesJsonNull()
        => Assert.That(_adapter.IsNull(_adapter.CreateValue(null)), Is.True);

    // ── Value access ──────────────────────────────────────────────────────────

    [Test]
    public void GetValue_ReturnsUnderlyingValue()
    {
        var token = new JValue(42);
        Assert.That(_adapter.GetValue(token), Is.EqualTo(42L)); // JValue stores int as long
    }

    [Test]
    public void GetValueT_ReturnsTypedValue()
    {
        var token = new JValue("hello");
        Assert.That(_adapter.GetValue<string>(token), Is.EqualTo("hello"));
    }

    // ── Type coercion ─────────────────────────────────────────────────────────

    [Test]
    public void TryGetBoolean_TrueForBoolToken()
        => Assert.That(_adapter.TryGetBoolean(new JValue(true)), Is.True);

    [Test]
    public void TryGetBoolean_TrueForStringTrue()
        => Assert.That(_adapter.TryGetBoolean(new JValue("true")), Is.True);

    [Test]
    public void TryGetBoolean_NullForNonBoolean()
        => Assert.That(_adapter.TryGetBoolean(new JValue(42)), Is.Null);

    [Test]
    public void TryGetBoolean_NullForObjectNode()
        => Assert.That(_adapter.TryGetBoolean(new JObject()), Is.Null);

    [Test]
    public void TryGetDouble_ReturnsDoubleForNumeric()
        => Assert.That(_adapter.TryGetDouble(new JValue(3.14)), Is.EqualTo(3.14).Within(0.0001));

    [Test]
    public void TryGetDouble_ReturnsIntAsDouble()
        => Assert.That(_adapter.TryGetDouble(new JValue(7)), Is.EqualTo(7.0));

    [Test]
    public void TryGetDouble_NullForNull()
        => Assert.That(_adapter.TryGetDouble(JValue.CreateNull()), Is.Null);

    [Test]
    public void TryGetDouble_NullForObject()
        => Assert.That(_adapter.TryGetDouble(new JObject()), Is.Null);

    [Test]
    public void TryGetString_ReturnsStringForStringToken()
        => Assert.That(_adapter.TryGetString(new JValue("hello")), Is.EqualTo("hello"));

    [Test]
    public void TryGetString_ReturnsStringForNumeric()
        => Assert.That(_adapter.TryGetString(new JValue(42)), Is.EqualTo("42"));

    [Test]
    public void TryGetString_NullForJsonNull()
        => Assert.That(_adapter.TryGetString(JValue.CreateNull()), Is.Null);

    // ── Cloning & replacement ─────────────────────────────────────────────────

    [Test]
    public void DeepClone_ProducesIndependentCopy()
    {
        var original = JObject.Parse("{\"a\": 1}");
        var clone = _adapter.DeepClone(original);
        _adapter.SetProperty(clone, "a", new JValue(99));
        Assert.That(original["a"]!.Value<int>(), Is.EqualTo(1)); // original unchanged
    }

    [Test]
    public void Replace_SubstitutesValueInParent()
    {
        var data = JObject.Parse("{\"x\": 1}");
        var target = data["x"]!;
        _adapter.Replace(target, new JValue(999));
        Assert.That(data["x"]!.Value<int>(), Is.EqualTo(999));
    }

    [Test]
    public void RemoveFromParent_RemovesPropertyFromObject()
    {
        var data = JObject.Parse("{\"keep\": 1, \"remove\": 2}");
        var toRemove = data["remove"]!;
        var result = _adapter.RemoveFromParent(toRemove);
        Assert.That(result, Is.True);
        Assert.That(data.ContainsKey("remove"), Is.False);
        Assert.That(data.ContainsKey("keep"), Is.True);
    }

    [Test]
    public void RemoveFromParent_RemovesElementFromArray()
    {
        var arr = JArray.Parse("[1, 2, 3]");
        var toRemove = arr[1];
        var result = _adapter.RemoveFromParent(toRemove);
        Assert.That(result, Is.True);
        Assert.That(arr.Count, Is.EqualTo(2));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void RemoveFromParent_ReturnsFalseForRootNode()
    {
        var root = JObject.Parse("{\"a\": 1}");
        var result = _adapter.RemoveFromParent(root);
        Assert.That(result, Is.False);
    }

    // ── Deep merge ────────────────────────────────────────────────────────────

    [Test]
    public void DeepMergeInto_MergesNewPropertiesFromSource()
    {
        var source = JObject.Parse("{\"b\": 2}");
        var target = JObject.Parse("{\"a\": 1}");
        _adapter.DeepMergeInto(source, target);
        Assert.That(target["a"]!.Value<int>(), Is.EqualTo(1));
        Assert.That(target["b"]!.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void DeepMergeInto_OverwritesPrimitiveWithSourceValue()
    {
        var source = JObject.Parse("{\"a\": 99}");
        var target = JObject.Parse("{\"a\": 1}");
        _adapter.DeepMergeInto(source, target);
        Assert.That(target["a"]!.Value<int>(), Is.EqualTo(99));
    }

    [Test]
    public void DeepMergeInto_RecursivelyMergesNestedObjects()
    {
        var source = JObject.Parse("{\"nested\": {\"b\": 2}}");
        var target = JObject.Parse("{\"nested\": {\"a\": 1}}");
        _adapter.DeepMergeInto(source, target);
        var nested = (JObject)target["nested"]!;
        Assert.That(nested["a"]!.Value<int>(), Is.EqualTo(1));
        Assert.That(nested["b"]!.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void DeepMergeInto_ConcatModeAppendArrayElements()
    {
        var source = JObject.Parse("{\"arr\": [3, 4]}");
        var target = JObject.Parse("{\"arr\": [1, 2]}");
        _adapter.DeepMergeInto(source, target, ArrayMergeMode.Concat);
        var arr = (JArray)target["arr"]!;
        Assert.That(arr.Count, Is.EqualTo(4));
    }

    [Test]
    public void DeepMergeInto_ReplaceModeReplacesArray()
    {
        var source = JObject.Parse("{\"arr\": [3, 4]}");
        var target = JObject.Parse("{\"arr\": [1, 2]}");
        _adapter.DeepMergeInto(source, target, ArrayMergeMode.Replace);
        var arr = (JArray)target["arr"]!;
        Assert.That(arr.Count, Is.EqualTo(2));
        Assert.That(arr[0].Value<int>(), Is.EqualTo(3));
    }

    // ── Parent access ─────────────────────────────────────────────────────────

    [Test]
    public void GetParentNode_ReturnsOwningObjectForProperty()
    {
        var data = JObject.Parse("{\"child\": {\"x\": 1}}");
        var child = data["child"]!;
        var parent = _adapter.GetParentNode(child);
        Assert.That(parent, Is.SameAs(data));
    }

    [Test]
    public void GetParentNode_ReturnsOwningObjectForArrayPropertyValue()
    {
        var data = JObject.Parse("{\"items\": [1, 2, 3]}");
        var item = data["items"]![0]!;
        var parent = _adapter.GetParentNode(item);
        // element in array-that-is-a-property-value → semantic parent is the containing JObject
        Assert.That(parent, Is.SameAs(data));
    }

    [Test]
    public void GetParentNode_NullForRoot()
    {
        var root = JObject.Parse("{\"a\": 1}");
        Assert.That(_adapter.GetParentNode(root), Is.Null);
    }

    [Test]
    public void GetParentPropertyName_ReturnsPropertyName()
    {
        var data = JObject.Parse("{\"myProp\": 42}");
        var value = data["myProp"]!;
        Assert.That(_adapter.GetParentPropertyName(value), Is.EqualTo("myProp"));
    }

    [Test]
    public void GetParentPropertyName_NullForArrayElement()
    {
        var arr = JArray.Parse("[1, 2, 3]");
        Assert.That(_adapter.GetParentPropertyName(arr[0]), Is.Null);
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    [Test]
    public void DeepEquals_TrueForIdenticalStructures()
    {
        var a = JObject.Parse("{\"x\": 1, \"y\": [2, 3]}");
        var b = JObject.Parse("{\"x\": 1, \"y\": [2, 3]}");
        Assert.That(_adapter.DeepEquals(a, b), Is.True);
    }

    [Test]
    public void DeepEquals_FalseForDifferentValues()
    {
        var a = JObject.Parse("{\"x\": 1}");
        var b = JObject.Parse("{\"x\": 2}");
        Assert.That(_adapter.DeepEquals(a, b), Is.False);
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    [Test]
    public void Parse_DeserializesJsonString()
    {
        var token = _adapter.Parse("{\"key\": \"value\"}");
        Assert.That(_adapter.IsObject(token), Is.True);
        Assert.That(_adapter.GetProperty(token, "key")?.Value<string>(), Is.EqualTo("value"));
    }

    [Test]
    public void Serialize_ProducesJsonString()
    {
        var obj = JObject.Parse("{\"a\":1}");
        var json = _adapter.Serialize(obj);
        Assert.That(json, Does.Contain("\"a\""));
        Assert.That(json, Does.Contain("1"));
    }
}
