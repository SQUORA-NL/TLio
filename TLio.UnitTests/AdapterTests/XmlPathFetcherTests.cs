using System.Xml.Linq;
using NUnit.Framework;
using TLio.Xml;

namespace TLio.UnitTests.AdapterTests;

/// <summary>
/// Tests for XPathItemsFetcher — all IItemsFetcher&lt;XElement&gt; members.
/// </summary>
[TestFixture]
public class XmlPathFetcherTests
{
    private XPathItemsFetcher _fetcher = null!;
    private XmlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _fetcher = new XPathItemsFetcher();
        _adapter = new XmlNodeAdapter();
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Test]
    public void RootPathIndicator_IsSlash() => Assert.That(_fetcher.RootPathIndicator, Is.EqualTo("/"));

    [Test]
    public void PathDelimiter_IsSlash() => Assert.That(_fetcher.PathDelimiter, Is.EqualTo("/"));

    [Test]
    public void CurrentItemPathIndicator_IsDot() => Assert.That(_fetcher.CurrentItemPathIndicator, Is.EqualTo("."));

    [Test]
    public void ParentPathIndicator_IsDotDot() => Assert.That(_fetcher.ParentPathIndicator, Is.EqualTo(".."));

    // ── SelectNodes ───────────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_RootPath_ReturnsSingleRootNode()
    {
        var data = XElement.Parse("<root><a>1</a></root>");
        var result = _fetcher.SelectNodes("/", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0], Is.SameAs(data));
    }

    [Test]
    public void SelectNodes_ChildPath_ReturnsMatchedChild()
    {
        var data = XElement.Parse("<root><name>Alice</name></root>");
        var result = _fetcher.SelectNodes("/name", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectNodes_NestedPath_ReturnsDeepChild()
    {
        var data = XElement.Parse("<root><person><name>Bob</name></person></root>");
        var result = _fetcher.SelectNodes("/person/name", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Value, Is.EqualTo("Bob"));
    }

    [Test]
    public void SelectNodes_NoMatch_ReturnsEmptyList()
    {
        var data = XElement.Parse("<root/>");
        var result = _fetcher.SelectNodes("/missing", data);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void SelectNodes_Wildcard_ReturnsAllMatchingChildren()
    {
        var data = XElement.Parse("<root><item>1</item><item>2</item><item>3</item></root>");
        var result = _fetcher.SelectNodes("/item", data);
        Assert.That(result.Count, Is.EqualTo(3));
    }

    // ── SelectNode ────────────────────────────────────────────────────────────

    [Test]
    public void SelectNode_ReturnsFirstMatch()
    {
        var data = XElement.Parse("<root><x>42</x></root>");
        var node = _fetcher.SelectNode("/x", data);
        Assert.That(node?.Value, Is.EqualTo("42"));
    }

    [Test]
    public void SelectNode_RootPath_ReturnsRoot()
    {
        var data = XElement.Parse("<root/>");
        Assert.That(_fetcher.SelectNode("/", data), Is.SameAs(data));
    }

    [Test]
    public void SelectNode_ReturnsNullForNoMatch()
    {
        var data = XElement.Parse("<root/>");
        Assert.That(_fetcher.SelectNode("/missing", data), Is.Null);
    }

    // ── GetPath ───────────────────────────────────────────────────────────────

    [Test]
    public void GetPath_RootNodeReturnsSlash()
    {
        var data = XElement.Parse("<root/>");
        Assert.That(_fetcher.GetPath(data), Is.EqualTo("/"));
    }

    [Test]
    public void GetPath_ChildNodeReturnsPath()
    {
        var data = XElement.Parse("<root><person><name>Bob</name></person></root>");
        var node = _fetcher.SelectNode("/person/name", data)!;
        Assert.That(_fetcher.GetPath(node), Is.EqualTo("/person/name"));
    }

    [Test]
    public void GetPath_DirectChildReturnsSlashName()
    {
        var data = XElement.Parse("<root><item>1</item></root>");
        var node = _fetcher.SelectNode("/item", data)!;
        Assert.That(_fetcher.GetPath(node), Is.EqualTo("/item"));
    }

    // ── GetParent ─────────────────────────────────────────────────────────────

    [Test]
    public void GetParent_OneLevelUp_ReturnsImmediateParent()
    {
        var data = XElement.Parse("<root><parent><child>1</child></parent></root>");
        var child = _fetcher.SelectNode("/parent/child", data)!;
        var parent = _fetcher.GetParent(child, 1);
        Assert.That(parent?.Name.LocalName, Is.EqualTo("parent"));
    }

    [Test]
    public void GetParent_TwoLevelsUp_ReturnsGrandparent()
    {
        var data = XElement.Parse("<root><a><b><c>1</c></b></a></root>");
        var node = _fetcher.SelectNode("/a/b/c", data)!;
        var grandparent = _fetcher.GetParent(node, 2);
        Assert.That(grandparent?.Name.LocalName, Is.EqualTo("a"));
    }

    [Test]
    public void GetParent_RootNode_ReturnsNull()
    {
        var data = XElement.Parse("<root/>");
        Assert.That(_fetcher.GetParent(data, 1), Is.Null);
    }

    // ── ResolveRelativePath ───────────────────────────────────────────────────

    [Test]
    public void ResolveRelativePath_DotOnly_ReturnsCurrentNodePath()
    {
        var data = XElement.Parse("<root><a><b>1</b></a></root>");
        var node = _fetcher.SelectNode("/a/b", data)!;
        var resolved = _fetcher.ResolveRelativePath(".", node, data);
        Assert.That(resolved, Is.EqualTo("/a/b"));
    }

    [Test]
    public void ResolveRelativePath_DotSlashSuffix_AppendsToCurrentPath()
    {
        var data = XElement.Parse("<root><a><b>1</b></a></root>");
        var node = _fetcher.SelectNode("/a", data)!;
        var resolved = _fetcher.ResolveRelativePath("./b", node, data);
        Assert.That(resolved, Is.EqualTo("/a/b"));
    }

    [Test]
    public void ResolveRelativePath_AbsolutePath_ReturnedAsIs()
    {
        var data = XElement.Parse("<root><a>1</a></root>");
        var node = _fetcher.SelectNode("/a", data)!;
        var resolved = _fetcher.ResolveRelativePath("/other/path", node, data);
        Assert.That(resolved, Is.EqualTo("/other/path"));
    }

    // ── EnsurePath ────────────────────────────────────────────────────────────

    [Test]
    public void EnsurePath_CreatesIntermediateElements()
    {
        var root = XElement.Parse("<root/>");
        _fetcher.EnsurePath("/a/b/c", root, _adapter);
        Assert.That(root.Element("a"), Is.Not.Null);
        Assert.That(root.Element("a")!.Element("b"), Is.Not.Null);
    }

    [Test]
    public void EnsurePath_DoesNotOverwriteExistingElements()
    {
        var root = XElement.Parse("<root><a><existing>99</existing></a></root>");
        _fetcher.EnsurePath("/a/b", root, _adapter);
        Assert.That(root.Element("a")!.Element("existing")!.Value, Is.EqualTo("99"));
    }

    // ── SplitParentAndLeaf ────────────────────────────────────────────────────

    [Test]
    public void SplitParentAndLeaf_ThreeLevelPath()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("/a/b/c");
        Assert.That(leaf, Is.EqualTo("c"));
        Assert.That(parent, Is.EqualTo("/a/b"));
    }

    [Test]
    public void SplitParentAndLeaf_TwoLevelPath()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("/person/name");
        Assert.That(leaf, Is.EqualTo("name"));
        Assert.That(parent, Is.EqualTo("/person"));
    }

    [Test]
    public void SplitParentAndLeaf_SingleSegment_ReturnsRootAndName()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("/name");
        Assert.That(parent, Is.EqualTo("/"));
        Assert.That(leaf, Is.EqualTo("name"));
    }

    [Test]
    public void SplitParentAndLeaf_RootPath_ReturnsEmptyLeaf()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("/");
        Assert.That(parent, Is.EqualTo("/"));
        Assert.That(leaf, Is.EqualTo(string.Empty));
    }

    // ── ProcessIndirectPath ───────────────────────────────────────────────────

    [Test]
    public void ProcessIndirectPath_NoIndirect_ReturnsOriginalPath()
    {
        var data = XElement.Parse("<root/>");
        Assert.That(_fetcher.ProcessIndirectPath("/a/b", data), Is.EqualTo("/a/b"));
    }

    [Test]
    public void ProcessIndirectPath_WithIndirect_ReturnsNull()
    {
        var data = XElement.Parse("<root/>");
        Assert.That(_fetcher.ProcessIndirectPath("=indirect(/ref)", data), Is.Null);
    }
}
