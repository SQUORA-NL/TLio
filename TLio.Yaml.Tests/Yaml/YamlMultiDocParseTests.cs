using NUnit.Framework;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

/// <summary>
/// Unit-level tests for <see cref="YamlNodeAdapter.Parse"/> multi-document support.
/// Inline TestCase is appropriate here because these test the Parse() method
/// directly, not full script execution (constitution §VI).
/// </summary>
[TestFixture]
public class YamlMultiDocParseTests
{
    private YamlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new YamlNodeAdapter(new YamlParentTracker());

    [Test]
    public void Parse_SingleDocument_ReturnsRootNodeDirectly()
    {
        const string yaml = "name: Alice\nage: 30";
        var result = _adapter.Parse(yaml);
        Assert.That(result, Is.InstanceOf<YamlMappingNode>());
    }

    [Test]
    public void Parse_ZeroDocuments_ReturnsEmptySequence()
    {
        // An empty string or only comments produces no YAML documents.
        const string yaml = "# just a comment";
        var result = _adapter.Parse(yaml);
        Assert.That(result, Is.InstanceOf<YamlSequenceNode>());
        Assert.That(((YamlSequenceNode)result).Children, Is.Empty);
    }

    [Test]
    public void Parse_TwoDocuments_ReturnsSequenceOfTwoRoots()
    {
        const string yaml = "name: Alice\nage: 30\n---\nname: Bob\nage: 25";
        var result = _adapter.Parse(yaml);
        Assert.That(result, Is.InstanceOf<YamlSequenceNode>());
        var seq = (YamlSequenceNode)result;
        Assert.That(seq.Children.Count, Is.EqualTo(2));
        Assert.That(seq.Children[0], Is.InstanceOf<YamlMappingNode>());
        Assert.That(seq.Children[1], Is.InstanceOf<YamlMappingNode>());
    }

    [Test]
    public void Parse_ThreeDocuments_ReturnsSequenceOfThreeRoots()
    {
        const string yaml = "a: 1\n---\nb: 2\n---\nc: 3";
        var result = _adapter.Parse(yaml);
        Assert.That(result, Is.InstanceOf<YamlSequenceNode>());
        Assert.That(((YamlSequenceNode)result).Children.Count, Is.EqualTo(3));
    }

    [Test]
    public void Parse_MultiDoc_FirstElementHasCorrectContent()
    {
        const string yaml = "name: Alice\nage: 30\n---\nname: Bob\nage: 25";
        var result = (YamlSequenceNode)_adapter.Parse(yaml);

        var first = (YamlMappingNode)result.Children[0];
        var nameKey = new YamlScalarNode("name");
        Assert.That(first.Children.ContainsKey(nameKey), Is.True);
        Assert.That(((YamlScalarNode)first.Children[nameKey]).Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void Parse_MultiDoc_SecondElementHasCorrectContent()
    {
        const string yaml = "name: Alice\nage: 30\n---\nname: Bob\nage: 25";
        var result = (YamlSequenceNode)_adapter.Parse(yaml);

        var second = (YamlMappingNode)result.Children[1];
        var nameKey = new YamlScalarNode("name");
        Assert.That(second.Children.ContainsKey(nameKey), Is.True);
        Assert.That(((YamlScalarNode)second.Children[nameKey]).Value, Is.EqualTo("Bob"));
    }
}
