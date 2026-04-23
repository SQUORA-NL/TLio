using NUnit.Framework;
using TLio.Yaml;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.EdgeCases;

[TestFixture]
public class YamlMalformedInputTests
{
    private YamlNodeAdapter _adapter = null!;
    private YamlPathItemsFetcher _fetcher = null!;

    [SetUp]
    public void SetUp()
    {
        var tracker = new YamlParentTracker();
        _adapter = new YamlNodeAdapter(tracker);
        _fetcher = new YamlPathItemsFetcher(tracker);
    }

    [Test]
    public void Parse_UndefinedAlias_ThrowsYamlException()
    {
        // Referencing an anchor that was never defined is definitively invalid YAML
        const string invalidYaml = "key: *undefined_anchor";
        Assert.That(() => _adapter.Parse(invalidYaml), Throws.InstanceOf<YamlException>());
    }

    [Test]
    public void Parse_EmptyString_ReturnsEmptySequence()
    {
        // YamlNodeAdapter explicitly returns empty YamlSequenceNode for empty input
        var node = _adapter.Parse(string.Empty);
        Assert.That(_adapter.IsArray(node), Is.True);
        Assert.That(_adapter.GetArrayLength(node), Is.EqualTo(0));
    }

    [Test]
    public void Parse_NullString_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _adapter.Parse(null!));
    }

    [Test]
    public void Parse_DuplicateMappingKey_ThrowsYamlException()
    {
        // YamlDotNet throws on duplicate mapping keys
        const string yaml = "key: first\nkey: second";
        Assert.That(() => _adapter.Parse(yaml), Throws.InstanceOf<YamlException>());
    }

    [Test]
    public void SelectNodes_NullOrEmptyPathOnValidDocument_ReturnsRoot()
    {
        var doc = new YamlMappingNode { { "x", new YamlScalarNode("1") } };
        var result = _fetcher.SelectNodes(string.Empty, doc);
        Assert.That(result.ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public void SelectNodes_PathOnEmptyMapping_ReturnsEmptyCollection()
    {
        var doc = new YamlMappingNode();
        var result = _fetcher.SelectNodes("$.anything", doc);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void SelectNodes_ArrayIndexOutOfRange_ReturnsEmptyCollection()
    {
        var seq = new YamlSequenceNode(new YamlScalarNode("only"));
        var doc = new YamlMappingNode { { "items", seq } };
        var result = _fetcher.SelectNodes("$.items[99]", doc);
        Assert.That(result.ToList(), Is.Empty);
    }

    [Test]
    public void GetProperty_OnSequenceNode_ReturnsNull()
    {
        var seq = new YamlSequenceNode(new YamlScalarNode("x"));
        var result = _adapter.GetProperty(seq, "anyKey");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryGetString_NullScalar_ReturnsNull()
    {
        var node = new YamlScalarNode("null");
        Assert.That(_adapter.TryGetString(node), Is.Null);
    }

    [Test]
    public void TryGetBoolean_NonBoolScalar_ReturnsNull()
    {
        var node = new YamlScalarNode("maybe");
        Assert.That(_adapter.TryGetBoolean(node), Is.Null);
    }

    [Test]
    public void IsNull_EmptyScalar_ReturnsTrue()
    {
        // An empty scalar value is equivalent to null
        var node = new YamlScalarNode(string.Empty) { Value = null };
        Assert.That(_adapter.IsNull(node), Is.True);
    }

    [Test]
    public void GetPropertyNames_OnSequenceNode_ReturnsEmpty()
    {
        var seq = new YamlSequenceNode();
        Assert.That(_adapter.GetPropertyNames(seq).ToList(), Is.Empty);
    }
}
