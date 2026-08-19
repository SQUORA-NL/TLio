using System.Xml.Linq;
using NUnit.Framework;

namespace TLio.Xml.Tests.NativeXPath;

/// <summary>
/// Paths are anchored on the document node, exactly as XPath defines it:
/// <c>/</c> is the document node, <c>/root</c> the document element, and
/// <c>/root/address/city</c> a descendant. A bare step such as <c>city</c> is a child
/// of the document node and therefore matches nothing.
/// </summary>
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

    /// <summary>Parses through the adapter so the element keeps the document node above it.</summary>
    private XElement Doc(string xml) => _adapter.Parse(xml);

    // ── SelectNodes ──────────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_RootPath_SelectsNothingBecauseTheDocumentNodeIsNotAnElement()
    {
        var root = Doc("<root><name>Alice</name></root>");

        // "/" names the document node. It has no element representation, so it selects
        // nothing — which is also what stops "/name" reaching <root>'s children.
        Assert.That(_fetcher.SelectNodes("/", root).ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_DocumentElementPath_ReturnsRoot()
    {
        var root = Doc("<root><name>Alice</name></root>");
        var result = _fetcher.SelectNodes("/root", root);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
        Assert.That(result.First().Name.LocalName, Is.EqualTo("root"));
    }

    [Test]
    public void SelectNodes_ChildName_ReturnsNameElement()
    {
        var root = Doc("<root><name>Alice</name></root>");
        var result = _fetcher.SelectNodes("/root/name", root);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
        Assert.That(result.First().Name.LocalName, Is.EqualTo("name"));
        Assert.That(result.First().Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectNodes_NestedPath_ReturnsCityElement()
    {
        var root = Doc("<root><address><city>Amsterdam</city></address></root>");
        var result = _fetcher.SelectNodes("/root/address/city", root);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
        Assert.That(result.First().Name.LocalName, Is.EqualTo("city"));
        Assert.That(result.First().Value, Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void SelectNodes_BareChildStep_SkipsNothingAndMatchesNothing()
    {
        var root = Doc("<root><name>Alice</name></root>");

        // 'name' is child::name of the document node, whose only element child is <root>.
        // Nothing in XPath lets a relative step skip the document element.
        var result = _fetcher.SelectNodes("name", root);

        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_RecursiveDescent_ReturnsAllNameElements()
    {
        var root = Doc("<root><name>Alice</name><nested><name>Bob</name></nested></root>");
        var result = _fetcher.SelectNodes("//name", root);
        Assert.That(result.ToList(), Has.Count.EqualTo(2));
    }

    // ── GetPath ───────────────────────────────────────────────────────────────

    [Test]
    public void GetPath_NestedElement_IncludesTheDocumentElement()
    {
        var root = Doc("<root><address><city>Amsterdam</city></address></root>");
        var cityEl = root.Element("address")!.Element("city")!;
        Assert.That(_fetcher.GetPath(cityEl), Is.EqualTo("/root/address/city"));
    }

    [Test]
    public void GetPath_RootElement_ReturnsItsOwnName()
    {
        var root = Doc("<root><name>Alice</name></root>");
        Assert.That(_fetcher.GetPath(root), Is.EqualTo("/root"));
    }

    // ── SplitParentAndLeaf ────────────────────────────────────────────────────

    [Test]
    public void SplitParentAndLeaf_NestedPath_ReturnsParentAndLeaf()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("/root/address/city");
        Assert.That(parent, Is.EqualTo("/root/address"));
        Assert.That(leaf, Is.EqualTo("city"));
    }

    [Test]
    public void SplitParentAndLeaf_DocumentElement_ReturnsRootAsParent()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("/root");
        Assert.That(parent, Is.EqualTo("/"));
        Assert.That(leaf, Is.EqualTo("root"));
    }

    [Test]
    public void SplitParentAndLeaf_PathWithPredicate_SplitsAtLastUnbracketedSlash()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("/root/items/item[1]");
        Assert.That(parent, Is.EqualTo("/root/items"));
        Assert.That(leaf, Is.EqualTo("item[1]"));
    }

    // ── EnsurePath ────────────────────────────────────────────────────────────

    [Test]
    public void EnsurePath_MissingIntermediateElements_CreatesFullPath()
    {
        var root = Doc("<root/>");
        _fetcher.EnsurePath("/root/contact/email", root, _adapter);

        Assert.That(root.Element("contact"), Is.Not.Null);
        Assert.That(root.Element("contact")!.Element("email"), Is.Not.Null);
    }

    [Test]
    public void EnsurePath_ExistingPath_DoesNotDuplicate()
    {
        var root = Doc("<root><contact><email>existing@example.com</email></contact></root>");
        _fetcher.EnsurePath("/root/contact/email", root, _adapter);

        var contacts = root.Elements("contact").ToList();
        Assert.That(contacts, Has.Count.EqualTo(1));
        Assert.That(contacts[0].Element("email")!.Value, Is.EqualTo("existing@example.com"),
            "an existing leaf keeps its value");
    }

    [Test]
    public void EnsurePath_PathRootedAtADifferentElement_BuildsNothing()
    {
        var root = Doc("<root/>");

        // XML has exactly one document element, so '/other/...' is unreachable rather than
        // something to scaffold — creating <other> underneath <root> would be a lie.
        _fetcher.EnsurePath("/other/contact", root, _adapter);

        Assert.That(root.Elements().ToList(), Is.Empty);
    }

    // ── Predicates ────────────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_AttributePredicate_WithAt_ReturnsMatchingElement()
    {
        var root = Doc("<root><item id='1'>first</item><item id='2'>second</item></root>");
        var result = _fetcher.SelectNodes("/root/item[@id='1']", root);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
        Assert.That(result.First().Value, Is.EqualTo("first"));
    }

    [Test]
    public void SelectNodes_AttributePredicate_EqualsInPredicate_ReturnsMatchingElement()
    {
        var root = Doc("<root><user type='admin'>Alice</user><user type='guest'>Bob</user></root>");
        var result = _fetcher.SelectNodes("/root/user[@type='admin']", root);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
        Assert.That(result.First().Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectNodes_AttributePredicate_MultipleConditions_ReturnsCorrectSubset()
    {
        var root = Doc("<root><item id='1' active='true'>first</item><item id='2' active='false'>second</item></root>");
        var result = _fetcher.SelectNodes("/root/item[@active='true']", root);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
        Assert.That(result.First().Value, Is.EqualTo("first"));
    }
}
