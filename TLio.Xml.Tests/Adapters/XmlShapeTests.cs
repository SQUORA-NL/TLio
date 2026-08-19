using System.Xml.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;

namespace TLio.Xml.Tests.Adapters;

/// <summary>
/// The rules that decide which JSON kind an element stands for, and the operations that depend
/// on them. Commands are written against the JSON data model, so these answers are what make an
/// XML document behave the way the same document behaves in JSON — every one of them is a case
/// that used to be answered differently and produced a different result than JSON.
/// </summary>
[TestFixture]
public class XmlShapeTests
{
    private XmlNodeAdapter _adapter = null!;

    /// <summary>
    /// The same adapter through the interface. GetNodeKind is a default interface method — the
    /// tie-breaker that turns the individual predicates into one answer — so it is only
    /// reachable this way.
    /// </summary>
    private INodeAdapter<XElement> _api = null!;

    [SetUp]
    public void SetUp() => _api = _adapter = new XmlNodeAdapter();

    private static XElement El(string xml) => XElement.Parse(xml);

    // ── Object vs array ───────────────────────────────────────────────────────

    [Test]
    public void AnObjectWithOneProperty_IsAnObjectNotAOneElementArray()
    {
        // "all children share a name" is true of any single-child element, so this used to read
        // as an array — which made compare report a spurious array difference for every nested
        // object with one field.
        var el = El("<address><city>Amsterdam</city></address>");

        Assert.That(_adapter.IsObject(el), Is.True);
        Assert.That(_adapter.IsArray(el), Is.False);
        Assert.That(_api.GetNodeKind(el), Is.EqualTo(NodeKind.Object));
    }

    [Test]
    public void ASingleItemElement_IsAOneElementArray()
    {
        // The canonical item name is what distinguishes an array of one from an object with one
        // property, which is otherwise the same XML.
        var el = El("<items><item>a</item></items>");

        Assert.That(_adapter.IsArray(el), Is.True);
        Assert.That(_adapter.IsObject(el), Is.False);
        Assert.That(_api.GetNodeKind(el), Is.EqualTo(NodeKind.Array));
    }

    [Test]
    public void RepeatedChildrenOfAnyName_AreAnArray()
    {
        var el = El("<orders><order>a</order><order>b</order></orders>");

        Assert.That(_adapter.IsArray(el), Is.True);
        Assert.That(_adapter.IsObject(el), Is.False);
    }

    [Test]
    public void AnArrayIsNotAlsoAnObject()
    {
        // These were both true before, and the answer a caller got depended on which predicate
        // it happened to test first — flatten, toCsv and the type predicates disagreed.
        var el = El("<items><item>a</item><item>b</item></items>");

        Assert.That(_adapter.IsArray(el) && _adapter.IsObject(el), Is.False);
    }

    // ── The empty element ─────────────────────────────────────────────────────

    [Test]
    public void AnEmptyElement_IsAContainerAPropertyCanBeWrittenInto()
    {
        // This is what makes "add /order/address/city" work: the <address/> that building the
        // path just created has to read as an object, or the value is dropped and the document
        // keeps an empty element where the city should be.
        var el = El("<address/>");

        Assert.That(_adapter.IsObject(el), Is.True);

        _adapter.SetProperty(el, "city", _adapter.CreateString("Amsterdam"));

        Assert.That(el.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<address><city>Amsterdam</city></address>"));
    }

    [Test]
    public void AnEmptyElement_IsAlsoNull_BecauseXmlCannotTellTheTwoApart()
    {
        // null, "", {} and [] are one shape in XML. Each predicate answers its own question;
        // GetNodeKind settles it as null so =isNull() still reports a null-valued property.
        var el = El("<value/>");

        Assert.That(_adapter.IsNull(el), Is.True);
        Assert.That(_adapter.IsPrimitive(el), Is.True);
        Assert.That(_adapter.IsArray(el), Is.False);
        Assert.That(_api.GetNodeKind(el), Is.EqualTo(NodeKind.Null));
    }

    // ── Array item naming ─────────────────────────────────────────────────────

    [Test]
    public void AppendingToAnArray_AdoptsTheItemNameAlreadyInUse()
    {
        // A value node arrives named after whatever produced it. Added verbatim it gives the
        // wrapper children of mixed names, and the array stops being recognised as one.
        var array = El("<orders><order>a</order><order>b</order></orders>");

        _adapter.AppendToArray(array, _adapter.CreateString("c"));

        Assert.That(array.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<orders><order>a</order><order>b</order><order>c</order></orders>"));
        Assert.That(_adapter.IsArray(array), Is.True);
    }

    [Test]
    public void AppendingToAnEmptyArray_UsesTheCanonicalItemName()
    {
        var array = _adapter.CreateArray();

        _adapter.AppendToArray(array, _adapter.CreateString("a"));

        Assert.That(array.Elements().Single().Name.LocalName,
            Is.EqualTo(XmlNodeAdapter.DefaultItemName));
    }

    [Test]
    public void InsertingIntoAnArray_AlsoAdoptsTheItemName()
    {
        var array = El("<orders><order>a</order><order>c</order></orders>");

        _adapter.InsertIntoArray(array, 1, _adapter.CreateString("b"));

        Assert.That(array.Elements().Select(e => e.Value), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(array.Elements().Select(e => e.Name.LocalName).Distinct().Single(),
            Is.EqualTo("order"));
    }

    // ── Writing values ────────────────────────────────────────────────────────

    [Test]
    public void SettingAProperty_RehangsTheValueUnderThePropertyName()
    {
        var target = El("<root/>");

        _adapter.SetProperty(target, "count", _adapter.CreateNumber(42));

        Assert.That(target.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<root><count>42</count></root>"));
    }

    [Test]
    public void SettingAnObjectValue_KeepsItsProperties()
    {
        var target = El("<root/>");
        var value = El("<value><x>1</x><y>2</y></value>");

        _adapter.SetProperty(target, "point", value);

        Assert.That(target.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<root><point><x>1</x><y>2</y></point></root>"));
    }

    [Test]
    public void ReplacingANode_KeepsItsNameAndTakesTheNewValue()
    {
        // In XML the element name is the property name, so replacing a value must not rename
        // the property to whatever the incoming node happened to be called.
        var root = El("<root><city>Amsterdam</city></root>");

        _adapter.Replace(root.Element("city")!, _adapter.CreateString("Rotterdam"));

        Assert.That(root.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<root><city>Rotterdam</city></root>"));
    }

    [Test]
    public void CreateBoolean_WritesTheJsonSpelling()
    {
        Assert.That(_adapter.CreateBoolean(true).Value, Is.EqualTo("true"));
        Assert.That(_adapter.CreateValue(false).Value, Is.EqualTo("false"));
    }

    // ── Merging ───────────────────────────────────────────────────────────────

    [Test]
    public void MergingTwoArrays_ConcatenatesThem()
    {
        // Matching array items by element name folded every item onto the first one, so two
        // arrays of two merged into an array of two with overwritten values.
        var target = El("<items><item>a</item><item>b</item></items>");
        var source = El("<items><item>c</item></items>");

        _adapter.DeepMergeInto(source, target);

        Assert.That(target.Elements().Select(e => e.Value), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void MergingTwoArrays_InReplaceMode_TakesTheSource()
    {
        var target = El("<items><item>a</item><item>b</item></items>");
        var source = El("<items><item>c</item></items>");

        _adapter.DeepMergeInto(source, target, ArrayMergeMode.Replace);

        Assert.That(target.Elements().Select(e => e.Value), Is.EqualTo(new[] { "c" }));
    }

    [Test]
    public void MergingObjects_CombinesPropertiesRecursively()
    {
        var target = El("<t><a><x>1</x></a></t>");
        var source = El("<s><a><y>2</y></a><b>3</b></s>");

        _adapter.DeepMergeInto(source, target);

        Assert.That(target.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<t><a><x>1</x><y>2</y></a><b>3</b></t>"));
    }

    [Test]
    public void MergingAScalarOverAnObject_ReplacesIt()
    {
        var target = El("<a><x>1</x></a>");
        var source = El("<a>flat</a>");

        _adapter.DeepMergeInto(source, target);

        Assert.That(target.ToString(SaveOptions.DisableFormatting), Is.EqualTo("<a>flat</a>"));
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Test]
    public void DeepEquals_IgnoresInsignificantWhitespaceAndEmptyElementSpelling()
    {
        Assert.That(_adapter.DeepEquals(El("<a><b>1</b></a>"), El("<a>\n  <b>1</b>\n</a>")), Is.True);
        Assert.That(_adapter.DeepEquals(El("<a/>"), El("<a></a>")), Is.True);
    }

    [Test]
    public void DeepEquals_SeparatesDifferentValues()
    {
        Assert.That(_adapter.DeepEquals(El("<a><b>1</b></a>"), El("<a><b>2</b></a>")), Is.False);
    }

    // ── Parent naming ─────────────────────────────────────────────────────────

    [Test]
    public void AnArrayItem_HasNoPropertyName()
    {
        // An array element is addressed by position, the same answer a JSON array element gives.
        var array = El("<items><item>a</item></items>");

        Assert.That(_adapter.GetParentPropertyName(array.Elements().First()), Is.Null);
    }

    [Test]
    public void AnObjectProperty_IsNamedByItsElement()
    {
        var obj = El("<address><city>Amsterdam</city></address>");

        Assert.That(_adapter.GetParentPropertyName(obj.Element("city")!), Is.EqualTo("city"));
    }
}
