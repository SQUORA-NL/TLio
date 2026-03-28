using NUnit.Framework;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.UnitTests.AdapterTests;

/// <summary>
/// Tests for YamlPathItemsFetcher — all IItemsFetcher&lt;YamlNode&gt; members.
/// </summary>
[TestFixture]
public class YamlPathFetcherTests
{
    private YamlPathItemsFetcher _fetcher = null!;
    private YamlNodeAdapter _adapter = null!;
    private YamlParentTracker _tracker = null!;

    [SetUp]
    public void SetUp()
    {
        _tracker = new YamlParentTracker();
        _fetcher = new YamlPathItemsFetcher(_tracker);
        _adapter = new YamlNodeAdapter(_tracker);
    }

    private static YamlMappingNode ParseYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Test]
    public void RootPathIndicator_IsDollarSign() => Assert.That(_fetcher.RootPathIndicator, Is.EqualTo("$"));

    [Test]
    public void PathDelimiter_IsDot() => Assert.That(_fetcher.PathDelimiter, Is.EqualTo("."));

    [Test]
    public void CurrentItemPathIndicator_IsAt() => Assert.That(_fetcher.CurrentItemPathIndicator, Is.EqualTo("@"));

    [Test]
    public void ParentPathIndicator_IsArrowLeft() => Assert.That(_fetcher.ParentPathIndicator, Is.EqualTo("<--"));

    // ── SelectNodes ───────────────────────────────────────────────────────────

    [Test]
    public void SelectNodes_RootPath_ReturnsSingleRootNode()
    {
        var data = ParseYaml("a: 1");
        var result = _fetcher.SelectNodes("$", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0], Is.SameAs(data));
    }

    [Test]
    public void SelectNodes_ChildKey_ReturnsMatchedNode()
    {
        var data = ParseYaml("name: Alice");
        var result = _fetcher.SelectNodes("$.name", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That((result[0] as YamlScalarNode)?.Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void SelectNodes_NestedKey_ReturnsDeepNode()
    {
        var data = ParseYaml("person:\n  name: Bob");
        var result = _fetcher.SelectNodes("$.person.name", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That((result[0] as YamlScalarNode)?.Value, Is.EqualTo("Bob"));
    }

    [Test]
    public void SelectNodes_ArrayIndex_ReturnsElement()
    {
        var data = ParseYaml("items:\n  - 10\n  - 20\n  - 30");
        var result = _fetcher.SelectNodes("$.items[1]", data);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That((result[0] as YamlScalarNode)?.Value, Is.EqualTo("20"));
    }

    [Test]
    public void SelectNodes_Wildcard_ReturnsAllElements()
    {
        var data = ParseYaml("items:\n  - 1\n  - 2\n  - 3");
        var result = _fetcher.SelectNodes("$.items[*]", data);
        Assert.That(result.Count, Is.EqualTo(3));
    }

    [Test]
    public void SelectNodes_NoMatch_ReturnsEmptyList()
    {
        var data = ParseYaml("a: 1");
        var result = _fetcher.SelectNodes("$.nonexistent", data);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    // ── SelectNode ────────────────────────────────────────────────────────────

    [Test]
    public void SelectNode_ReturnsFirstMatch()
    {
        var data = ParseYaml("x: 42");
        var node = _fetcher.SelectNode("$.x", data);
        Assert.That((node as YamlScalarNode)?.Value, Is.EqualTo("42"));
    }

    [Test]
    public void SelectNode_ReturnsNullForNoMatch()
    {
        var data = ParseYaml("x: 42");
        Assert.That(_fetcher.SelectNode("$.missing", data), Is.Null);
    }

    // ── GetPath ───────────────────────────────────────────────────────────────

    [Test]
    public void GetPath_UntrackedNode_ReturnsDollar()
    {
        var data = ParseYaml("a: 1");
        // root node is not tracked (no parent)
        Assert.That(_fetcher.GetPath(data), Is.EqualTo("$"));
    }

    [Test]
    public void GetPath_TrackedChildNode_ReturnsFullPath()
    {
        var data = ParseYaml("person:\n  name: Bob");
        var node = _fetcher.SelectNode("$.person.name", data)!;
        Assert.That(_fetcher.GetPath(node), Is.EqualTo("$.person.name"));
    }

    // ── GetParent ─────────────────────────────────────────────────────────────

    [Test]
    public void GetParent_OneLevelUp_ReturnsImmediateParent()
    {
        var data = ParseYaml("parent:\n  child: 1");
        var child = _fetcher.SelectNode("$.parent.child", data)!;
        var parent = _fetcher.GetParent(child, 1)!;
        Assert.That(_adapter.HasProperty(parent, "child"), Is.True);
    }

    [Test]
    public void GetParent_TwoLevelsUp_ReturnsGrandparent()
    {
        var data = ParseYaml("a:\n  b:\n    c: 1");
        var node = _fetcher.SelectNode("$.a.b.c", data)!;
        var grandparent = _fetcher.GetParent(node, 2)!;
        Assert.That(_adapter.HasProperty(grandparent, "b"), Is.True);
    }

    [Test]
    public void GetParent_UntrackedNode_ReturnsNull()
    {
        var node = new YamlScalarNode("orphan");
        Assert.That(_fetcher.GetParent(node, 1), Is.Null);
    }

    // ── ResolveRelativePath ───────────────────────────────────────────────────

    [Test]
    public void ResolveRelativePath_AtOnly_ReturnsCurrentNodePath()
    {
        var data = ParseYaml("x:\n  y: 1");
        var node = _fetcher.SelectNode("$.x.y", data)!;
        var resolved = _fetcher.ResolveRelativePath("@", node, data);
        Assert.That(resolved, Is.EqualTo("$.x.y"));
    }

    [Test]
    public void ResolveRelativePath_AtDotProp_AppendsToCurrentPath()
    {
        var data = ParseYaml("x:\n  y: 1");
        var node = _fetcher.SelectNode("$.x", data)!;
        var resolved = _fetcher.ResolveRelativePath("@.y", node, data);
        Assert.That(resolved, Is.EqualTo("$.x.y"));
    }

    [Test]
    public void ResolveRelativePath_AbsolutePath_ReturnedAsIs()
    {
        var data = ParseYaml("a: 1");
        var node = _fetcher.SelectNode("$.a", data)!;
        var resolved = _fetcher.ResolveRelativePath("$.other.path", node, data);
        Assert.That(resolved, Is.EqualTo("$.other.path"));
    }

    // ── EnsurePath ────────────────────────────────────────────────────────────

    [Test]
    public void EnsurePath_CreatesIntermediateMappings()
    {
        var root = new YamlMappingNode();
        _fetcher.EnsurePath("$.a.b.c", root, _adapter);
        Assert.That(_adapter.HasProperty(root, "a"), Is.True);
        var a = _adapter.GetProperty(root, "a") as YamlMappingNode;
        Assert.That(_adapter.HasProperty(a!, "b"), Is.True);
    }

    [Test]
    public void EnsurePath_DoesNotOverwriteExistingKeys()
    {
        var root = new YamlMappingNode();
        root.Children[new YamlScalarNode("a")] = new YamlMappingNode();
        ((YamlMappingNode)root.Children[new YamlScalarNode("a")])
            .Children[new YamlScalarNode("existing")] = new YamlScalarNode("99");

        _fetcher.EnsurePath("$.a.b", root, _adapter);

        var a = (YamlMappingNode)root.Children[new YamlScalarNode("a")];
        Assert.That((a.Children[new YamlScalarNode("existing")] as YamlScalarNode)?.Value, Is.EqualTo("99"));
    }

    // ── SplitParentAndLeaf ────────────────────────────────────────────────────

    [Test]
    public void SplitParentAndLeaf_ThreeLevelPath()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("$.a.b.c");
        Assert.That(leaf, Is.EqualTo("c"));
        Assert.That(parent, Does.Contain("a").And.Contains("b"));
    }

    [Test]
    public void SplitParentAndLeaf_TwoLevelPath()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("$.person.name");
        Assert.That(leaf, Is.EqualTo("name"));
        Assert.That(parent, Is.EqualTo("$.person"));
    }

    [Test]
    public void SplitParentAndLeaf_SingleSegment_ReturnsRootAndSegment()
    {
        var (parent, leaf) = _fetcher.SplitParentAndLeaf("$.name");
        Assert.That(parent, Is.EqualTo("$"));
        Assert.That(leaf, Is.EqualTo("name"));
    }

    // ── ProcessIndirectPath ───────────────────────────────────────────────────

    [Test]
    public void ProcessIndirectPath_NoIndirect_ReturnsOriginalPath()
    {
        var data = new YamlMappingNode();
        Assert.That(_fetcher.ProcessIndirectPath("$.a.b", data), Is.EqualTo("$.a.b"));
    }

    [Test]
    public void ProcessIndirectPath_WithIndirect_ReturnsNull()
    {
        var data = new YamlMappingNode();
        Assert.That(_fetcher.ProcessIndirectPath("=indirect($.ref)", data), Is.Null);
    }
}
