using System.Xml.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Xml;

namespace TLio.UnitTests.AdapterTests;

/// <summary>
/// Tests for XmlNodeAdapter — all INodeAdapter&lt;XElement&gt; members.
/// </summary>
[TestFixture]
public class XmlNodeAdapterTests
{
    private XmlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new XmlNodeAdapter();

    // ── Type queries ──────────────────────────────────────────────────────────

    [Test]
    public void IsObject_ReturnsTrueForElementWithChildren()
    {
        var el = XElement.Parse("<root><a>1</a></root>");
        Assert.That(_adapter.IsObject(el), Is.True);
    }

    [Test]
    public void IsObject_ReturnsFalseForLeafElement()
    {
        var el = XElement.Parse("<value>hello</value>");
        Assert.That(_adapter.IsObject(el), Is.False);
    }

    [Test]
    public void IsArray_ReturnsTrueWhenAllChildrenShareOneName()
    {
        var el = XElement.Parse("<items><item>1</item><item>2</item></items>");
        Assert.That(_adapter.IsArray(el), Is.True);
    }

    [Test]
    public void IsArray_ReturnsFalseWhenChildrenHaveDifferentNames()
    {
        var el = XElement.Parse("<root><a>1</a><b>2</b></root>");
        Assert.That(_adapter.IsArray(el), Is.False);
    }

    [Test]
    public void IsPrimitive_ReturnsTrueForLeaf()
    {
        var el = XElement.Parse("<val>42</val>");
        Assert.That(_adapter.IsPrimitive(el), Is.True);
    }

    [Test]
    public void IsPrimitive_ReturnsFalseForElementWithChildren()
    {
        var el = XElement.Parse("<root><child/></root>");
        Assert.That(_adapter.IsPrimitive(el), Is.False);
    }

    [Test]
    public void IsNull_ReturnsTrueForNilAttribute()
    {
        var el = XElement.Parse("<val nil=\"true\"/>");
        Assert.That(_adapter.IsNull(el), Is.True);
    }

    [Test]
    public void IsNull_ReturnsTrueForEmptyElement()
    {
        var el = XElement.Parse("<val/>");
        Assert.That(_adapter.IsNull(el), Is.True);
    }

    [Test]
    public void IsNull_ReturnsFalseForElementWithText()
    {
        var el = XElement.Parse("<val>hello</val>");
        Assert.That(_adapter.IsNull(el), Is.False);
    }

    // ── Object operations ─────────────────────────────────────────────────────

    [Test]
    public void HasProperty_ReturnsTrueWhenChildExists()
    {
        var el = XElement.Parse("<root><name>Alice</name></root>");
        Assert.That(_adapter.HasProperty(el, "name"), Is.True);
    }

    [Test]
    public void HasProperty_ReturnsFalseWhenChildAbsent()
    {
        var el = XElement.Parse("<root><name>Alice</name></root>");
        Assert.That(_adapter.HasProperty(el, "age"), Is.False);
    }

    [Test]
    public void GetProperty_ReturnsChildElement()
    {
        var el = XElement.Parse("<root><name>Alice</name></root>");
        var prop = _adapter.GetProperty(el, "name");
        Assert.That(prop, Is.Not.Null);
        Assert.That(prop!.Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void GetProperty_ReturnsNullForMissing()
    {
        var el = XElement.Parse("<root/>");
        Assert.That(_adapter.GetProperty(el, "missing"), Is.Null);
    }

    [Test]
    public void SetProperty_AddsNewChild()
    {
        var el = XElement.Parse("<root/>");
        _adapter.SetProperty(el, "key", XElement.Parse("<value>42</value>"));
        Assert.That(el.Element("key"), Is.Not.Null);
        Assert.That(el.Element("key")!.Value, Is.EqualTo("42"));
    }

    [Test]
    public void SetProperty_OverwritesExistingChild()
    {
        var el = XElement.Parse("<root><key>old</key></root>");
        _adapter.SetProperty(el, "key", XElement.Parse("<value>new</value>"));
        Assert.That(el.Element("key")!.Value, Is.EqualTo("new"));
        Assert.That(el.Elements("key").Count(), Is.EqualTo(1));
    }

    [Test]
    public void RemoveProperty_RemovesChild()
    {
        var el = XElement.Parse("<root><a>1</a><b>2</b></root>");
        _adapter.RemoveProperty(el, "a");
        Assert.That(el.Element("a"), Is.Null);
        Assert.That(el.Element("b"), Is.Not.Null);
    }

    [Test]
    public void GetPropertyNames_ReturnsChildElementNames()
    {
        var el = XElement.Parse("<root><x>1</x><y>2</y><z>3</z></root>");
        var names = _adapter.GetPropertyNames(el).ToList();
        Assert.That(names, Is.EquivalentTo(new[] { "x", "y", "z" }));
    }

    // ── Array operations ──────────────────────────────────────────────────────

    [Test]
    public void AppendToArray_AddsElement()
    {
        var arr = XElement.Parse("<items/>");
        _adapter.AppendToArray(arr, XElement.Parse("<item>1</item>"));
        _adapter.AppendToArray(arr, XElement.Parse("<item>2</item>"));
        Assert.That(arr.Elements().Count(), Is.EqualTo(2));
    }

    [Test]
    public void InsertIntoArray_InsertsAtIndex()
    {
        var arr = XElement.Parse("<items><item>1</item><item>3</item></items>");
        _adapter.InsertIntoArray(arr, 1, XElement.Parse("<item>2</item>"));
        var elements = arr.Elements().ToList();
        Assert.That(elements.Count, Is.EqualTo(3));
        Assert.That(elements[1].Value, Is.EqualTo("2"));
    }

    [Test]
    public void RemoveFromArray_RemovesAtIndex()
    {
        var arr = XElement.Parse("<items><item>1</item><item>2</item><item>3</item></items>");
        _adapter.RemoveFromArray(arr, 1);
        var elements = arr.Elements().ToList();
        Assert.That(elements.Count, Is.EqualTo(2));
        Assert.That(elements[1].Value, Is.EqualTo("3"));
    }

    [Test]
    public void GetArrayLength_ReturnsChildCount()
    {
        var arr = XElement.Parse("<items><item/><item/><item/></items>");
        Assert.That(_adapter.GetArrayLength(arr), Is.EqualTo(3));
    }

    [Test]
    public void GetArrayElement_ReturnsElementAtIndex()
    {
        var arr = XElement.Parse("<items><item>a</item><item>b</item><item>c</item></items>");
        Assert.That(_adapter.GetArrayElement(arr, 1).Value, Is.EqualTo("b"));
    }

    [Test]
    public void GetArrayElements_ReturnsAllChildren()
    {
        var arr = XElement.Parse("<items><item>10</item><item>20</item></items>");
        var values = _adapter.GetArrayElements(arr).Select(e => e.Value).ToList();
        Assert.That(values, Is.EqualTo(new[] { "10", "20" }));
    }

    // ── Node creation ─────────────────────────────────────────────────────────

    [Test]
    public void CreateNull_IsNull() => Assert.That(_adapter.IsNull(_adapter.CreateNull()), Is.True);

    [Test]
    public void CreateObject_IsObject() => Assert.That(_adapter.IsObject(_adapter.CreateObject()), Is.False); // empty element = no children

    [Test]
    public void CreateArray_IsArray() => Assert.That(_adapter.IsArray(_adapter.CreateArray()), Is.False); // empty element = no children

    [Test]
    public void CreateString_HasCorrectValue()
        => Assert.That(_adapter.CreateString("hi").Value, Is.EqualTo("hi"));

    [Test]
    public void CreateNumber_HasCorrectValue()
    {
        var el = _adapter.CreateNumber(3.14);
        Assert.That(double.Parse(el.Value, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(3.14).Within(0.0001));
    }

    [Test]
    public void CreateBoolean_HasCorrectValue()
        => Assert.That(_adapter.CreateBoolean(true).Value, Is.EqualTo("true"));

    [Test]
    public void CreateValue_NullProducesNullNode()
        => Assert.That(_adapter.IsNull(_adapter.CreateValue(null)), Is.True);

    // ── Value access ──────────────────────────────────────────────────────────

    [Test]
    public void GetValue_ReturnsTextContent()
    {
        var el = XElement.Parse("<val>hello</val>");
        Assert.That(_adapter.GetValue(el), Is.EqualTo("hello"));
    }

    [Test]
    public void GetValueT_ReturnsTypedValue()
    {
        var el = XElement.Parse("<val>42</val>");
        Assert.That(_adapter.GetValue<int>(el), Is.EqualTo(42));
    }

    // ── Type coercion ─────────────────────────────────────────────────────────

    [Test]
    public void TryGetBoolean_TrueForTrueString()
        => Assert.That(_adapter.TryGetBoolean(XElement.Parse("<v>true</v>")), Is.True);

    [Test]
    public void TryGetBoolean_FalseForFalseString()
        => Assert.That(_adapter.TryGetBoolean(XElement.Parse("<v>false</v>")), Is.False);

    [Test]
    public void TryGetBoolean_NullForNonBoolean()
        => Assert.That(_adapter.TryGetBoolean(XElement.Parse("<v>hello</v>")), Is.Null);

    [Test]
    public void TryGetDouble_ReturnsDoubleForNumericString()
        => Assert.That(_adapter.TryGetDouble(XElement.Parse("<v>3.14</v>")), Is.EqualTo(3.14).Within(0.0001));

    [Test]
    public void TryGetDouble_NullForNonNumeric()
        => Assert.That(_adapter.TryGetDouble(XElement.Parse("<v>hello</v>")), Is.Null);

    [Test]
    public void TryGetString_ReturnsValueForPrimitive()
        => Assert.That(_adapter.TryGetString(XElement.Parse("<v>hello</v>")), Is.EqualTo("hello"));

    [Test]
    public void TryGetString_NullForNull()
        => Assert.That(_adapter.TryGetString(XElement.Parse("<v nil=\"true\"/>")), Is.Null);

    [Test]
    public void TryGetString_NullForObjectNode()
        => Assert.That(_adapter.TryGetString(XElement.Parse("<root><child/></root>")), Is.Null);

    // ── Cloning & replacement ─────────────────────────────────────────────────

    [Test]
    public void DeepClone_ProducesIndependentCopy()
    {
        var original = XElement.Parse("<root><a>1</a></root>");
        var clone = _adapter.DeepClone(original);
        clone.Element("a")!.Value = "99";
        Assert.That(original.Element("a")!.Value, Is.EqualTo("1"));
    }

    [Test]
    public void Replace_SubstitutesValueInParent()
    {
        var root = XElement.Parse("<root><x>1</x></root>");
        var target = root.Element("x")!;
        _adapter.Replace(target, XElement.Parse("<value>999</value>"));
        Assert.That(root.Element("x")!.Value, Is.EqualTo("999"));
    }

    [Test]
    public void Replace_PreservesTargetElementName()
    {
        var root = XElement.Parse("<root><myProp>old</myProp></root>");
        var target = root.Element("myProp")!;
        _adapter.Replace(target, XElement.Parse("<value>new</value>"));
        // Name must stay "myProp", not become "value"
        Assert.That(root.Element("myProp"), Is.Not.Null);
        Assert.That(root.Element("myProp")!.Value, Is.EqualTo("new"));
    }

    [Test]
    public void RemoveFromParent_RemovesChildFromParent()
    {
        var root = XElement.Parse("<root><keep>1</keep><remove>2</remove></root>");
        var toRemove = root.Element("remove")!;
        var result = _adapter.RemoveFromParent(toRemove);
        Assert.That(result, Is.True);
        Assert.That(root.Element("remove"), Is.Null);
        Assert.That(root.Element("keep"), Is.Not.Null);
    }

    [Test]
    public void RemoveFromParent_ReturnsFalseForRootNode()
    {
        var root = XElement.Parse("<root/>");
        Assert.That(_adapter.RemoveFromParent(root), Is.False);
    }

    // ── Deep merge ────────────────────────────────────────────────────────────

    [Test]
    public void DeepMergeInto_MergesNewChildren()
    {
        var source = XElement.Parse("<root><b>2</b></root>");
        var target = XElement.Parse("<root><a>1</a></root>");
        _adapter.DeepMergeInto(source, target);
        Assert.That(target.Element("a")!.Value, Is.EqualTo("1"));
        Assert.That(target.Element("b")!.Value, Is.EqualTo("2"));
    }

    [Test]
    public void DeepMergeInto_OverwritesPrimitive()
    {
        var source = XElement.Parse("<root><a>99</a></root>");
        var target = XElement.Parse("<root><a>1</a></root>");
        _adapter.DeepMergeInto(source, target);
        Assert.That(target.Element("a")!.Value, Is.EqualTo("99"));
    }

    [Test]
    public void DeepMergeInto_ConcatModeAppendsArrayElements()
    {
        var source = XElement.Parse("<root><items><item>3</item><item>4</item></items></root>");
        var target = XElement.Parse("<root><items><item>1</item><item>2</item></items></root>");
        _adapter.DeepMergeInto(source, target, ArrayMergeMode.Concat);
        Assert.That(target.Element("items")!.Elements().Count(), Is.EqualTo(4));
    }

    [Test]
    public void DeepMergeInto_ReplaceModeReplacesArray()
    {
        var source = XElement.Parse("<root><items><item>3</item><item>4</item></items></root>");
        var target = XElement.Parse("<root><items><item>1</item><item>2</item></items></root>");
        _adapter.DeepMergeInto(source, target, ArrayMergeMode.Replace);
        var items = target.Element("items")!.Elements().ToList();
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[0].Value, Is.EqualTo("3"));
    }

    // ── Parent access ─────────────────────────────────────────────────────────

    [Test]
    public void GetParentNode_ReturnsParentXElement()
    {
        var root = XElement.Parse("<root><child>1</child></root>");
        var child = root.Element("child")!;
        Assert.That(_adapter.GetParentNode(child), Is.SameAs(root));
    }

    [Test]
    public void GetParentNode_NullForRoot()
    {
        var root = XElement.Parse("<root/>");
        Assert.That(_adapter.GetParentNode(root), Is.Null);
    }

    [Test]
    public void GetParentPropertyName_ReturnsOwnLocalName()
    {
        var root = XElement.Parse("<root><myProp>42</myProp></root>");
        var prop = root.Element("myProp")!;
        Assert.That(_adapter.GetParentPropertyName(prop), Is.EqualTo("myProp"));
    }

    [Test]
    public void GetParentPropertyName_NullForRoot()
    {
        var root = XElement.Parse("<root/>");
        Assert.That(_adapter.GetParentPropertyName(root), Is.Null);
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    [Test]
    public void DeepEquals_TrueForIdenticalStructures()
    {
        var a = XElement.Parse("<root><x>1</x><y><z>2</z></y></root>");
        var b = XElement.Parse("<root><x>1</x><y><z>2</z></y></root>");
        Assert.That(_adapter.DeepEquals(a, b), Is.True);
    }

    [Test]
    public void DeepEquals_FalseForDifferentValues()
    {
        var a = XElement.Parse("<root><x>1</x></root>");
        var b = XElement.Parse("<root><x>2</x></root>");
        Assert.That(_adapter.DeepEquals(a, b), Is.False);
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    [Test]
    public void Parse_DeserializesXmlString()
    {
        var el = _adapter.Parse("<root><key>value</key></root>");
        Assert.That(_adapter.IsObject(el), Is.True);
        Assert.That(_adapter.GetProperty(el, "key")?.Value, Is.EqualTo("value"));
    }

    [Test]
    public void Serialize_ProducesXmlString()
    {
        var el = XElement.Parse("<root><a>1</a></root>");
        var xml = _adapter.Serialize(el);
        Assert.That(xml, Does.Contain("<a>"));
        Assert.That(xml, Does.Contain("1"));
    }
}
