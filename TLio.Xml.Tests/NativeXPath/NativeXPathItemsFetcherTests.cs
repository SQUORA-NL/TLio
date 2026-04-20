using System.Xml.Linq;
using NUnit.Framework;

namespace TLio.Xml.Tests.NativeXPath;

[TestFixture]
public class NativeXPathItemsFetcherTests
{
    private NativeXPathItemsFetcher _fetcher = null!;
    private XmlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _fetcher = new NativeXPathItemsFetcher();
        _adapter = new XmlNodeAdapter();
    }

    // ── SelectNodes ──────────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_DotPath_ReturnsRoot()
    {
        var root = XElement.Parse("<root><name>Alice</name></root>");
        var result = _fetcher.SelectNodes(".", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.First(), Is.SameAs(root));
    }

    [Test]
    public void SelectNodes_ChildName_ReturnsNameElement()
    {
        var root = XElement.Parse("<root><name>Alice</name></root>");
        var result = _fetcher.SelectNodes("name", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.First().Name.LocalName, Is.EqualTo("name"));
        Assert.That(result.First().Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectNodes_NestedPath_ReturnsCityElement()
    {
        var root = XElement.Parse("<root><address><city>Amsterdam</city></address></root>");
        var result = _fetcher.SelectNodes("address/city", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.First().Name.LocalName, Is.EqualTo("city"));
        Assert.That(result.First().Value, Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void SelectNodes_RecursiveDescent_ReturnsAllNameElements()
    {
        var root = XElement.Parse("<root><name>Alice</name><nested><name>Bob</name></nested></root>");
        var result = _fetcher.SelectNodes("//name", root);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.All(e => e.Name.LocalName == "name"), Is.True);
    }

    // ── GetPath ───────────────────────────────────────────────────────────────

    [Test]
    public void GetPath_NestedElement_ReturnsRelativePath()
    {
        var root = XElement.Parse("<root><address><city>Amsterdam</city></address></root>");
        var cityEl = root.Element("address")!.Element("city")!;
        var path = _fetcher.GetPath(cityEl);
        Assert.That(path, Is.EqualTo("address/city"));
    }

    [Test]
    public void GetPath_RootElement_ReturnsDot()
    {
        var root = XElement.Parse("<root><name>Alice</name></root>");
        var path = _fetcher.GetPath(root);
        Assert.That(path, Is.EqualTo("."));
    }

    // ── SplitParentAndLeaf ────────────────────────────────────────────────────

    [Test]
    public void SplitParentAndLeaf_NestedPath_ReturnsParentAndLeaf()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("address/city");
        Assert.That(parent, Is.EqualTo("address"));
        Assert.That(leaf, Is.EqualTo("city"));
    }

    [Test]
    public void SplitParentAndLeaf_SingleSegment_ReturnsDotParent()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("name");
        Assert.That(parent, Is.EqualTo("."));
        Assert.That(leaf, Is.EqualTo("name"));
    }

    [Test]
    public void SplitParentAndLeaf_PathWithPredicate_SplitsAtLastUnbracketedSlash()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("items/item[1]");
        Assert.That(parent, Is.EqualTo("items"));
        Assert.That(leaf, Is.EqualTo("item[1]"));
    }

    // ── EnsurePath ────────────────────────────────────────────────────────────

    [Test]
    public void EnsurePath_MissingIntermediateElements_CreatesFullPath()
    {
        var root = XElement.Parse("<root/>");
        _fetcher.EnsurePath("contact/email", root, _adapter);
        var contact = root.Element("contact");
        Assert.That(contact, Is.Not.Null, "contact element should have been created");
        var email = contact!.Element("email");
        Assert.That(email, Is.Not.Null, "email element should have been created");
    }

    [Test]
    public void EnsurePath_ExistingPath_DoesNotDuplicate()
    {
        var root = XElement.Parse("<root><contact><email>existing@example.com</email></contact></root>");
        _fetcher.EnsurePath("contact/email", root, _adapter);
        var contacts = root.Elements("contact").ToList();
        Assert.That(contacts.Count, Is.EqualTo(1), "should not create duplicate contact element");
        Assert.That(contacts[0].Element("email")!.Value, Is.EqualTo("existing@example.com"),
            "existing email value should be preserved");
    }

    // ── XPath special characters: @ (attribute) and = (predicate) ────────────

    [Test]
    public void SelectNodes_AttributePredicate_WithAt_ReturnsMatchingElement()
    {
        var root = XElement.Parse("<root><item id='1'>first</item><item id='2'>second</item></root>");
        var result = _fetcher.SelectNodes("item[@id='1']", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.First().Value, Is.EqualTo("first"));
    }

    [Test]
    public void SelectNodes_AttributePredicate_EqualsInPredicate_ReturnsMatchingElement()
    {
        var root = XElement.Parse("<root><user type='admin'>Alice</user><user type='guest'>Bob</user></root>");
        var result = _fetcher.SelectNodes("user[@type='admin']", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.First().Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectNodes_AttributePredicate_MultipleConditions_ReturnsCorrectSubset()
    {
        // @ in XPath predicates works fine — the attribute axis (/@name) returns XAttribute
        // nodes which are not XElement and therefore out of scope for IItemsFetcher<XElement>.
        var root = XElement.Parse("<root><item id='1' active='true'>first</item><item id='2' active='false'>second</item></root>");
        var result = _fetcher.SelectNodes("item[@active='true']", root);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.First().Value, Is.EqualTo("first"));
    }
}
