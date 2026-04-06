using NUnit.Framework;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

/// <summary>
/// Lock-in tests for <see cref="YamlNodeAdapter.Serialize"/> when the root is a
/// <see cref="YamlSequenceNode"/> produced by multi-document YAML parsing.
/// No code change is expected — these tests assert the existing behaviour (US2).
/// </summary>
[TestFixture]
public class YamlSerializeRoundTripTests
{
    private YamlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new YamlNodeAdapter(new YamlParentTracker());

    [Test]
    public void Serialize_SequenceRoot_ProducesValidYaml()
    {
        const string yaml = "name: Alice\nage: 30\n---\nname: Bob\nage: 25";
        var parsed = _adapter.Parse(yaml);

        var serialized = _adapter.Serialize(parsed);

        Assert.That(serialized, Is.Not.Null.And.Not.Empty);
        // Must be re-parseable
        Assert.DoesNotThrow(() => _adapter.Parse(serialized));
    }

    [Test]
    public void Serialize_SequenceRoot_RoundTripPreservesStructure()
    {
        const string yaml = "name: Alice\nage: 30\n---\nname: Bob\nage: 25";
        var original = (YamlSequenceNode)_adapter.Parse(yaml);

        var serialized = _adapter.Serialize(original);
        var reparsed = _adapter.Parse(serialized);

        // After round-trip, root is still a sequence with 2 elements
        // (serialized as YAML sequence in a single document)
        Assert.That(reparsed, Is.InstanceOf<YamlSequenceNode>());
        var seq = (YamlSequenceNode)reparsed;
        Assert.That(seq.Children.Count, Is.EqualTo(2));
    }

    [Test]
    public void Serialize_SequenceRoot_PreservesFieldValues()
    {
        const string yaml = "name: Alice\nage: 30\n---\nname: Bob\nage: 25";
        var original = (YamlSequenceNode)_adapter.Parse(yaml);

        var serialized = _adapter.Serialize(original);
        var reparsed = (YamlSequenceNode)_adapter.Parse(serialized);

        var first  = (YamlMappingNode)reparsed.Children[0];
        var second = (YamlMappingNode)reparsed.Children[1];
        var nameKey = new YamlScalarNode("name");

        Assert.That(((YamlScalarNode)first.Children[nameKey]).Value,  Is.EqualTo("Alice"));
        Assert.That(((YamlScalarNode)second.Children[nameKey]).Value, Is.EqualTo("Bob"));
    }

    [Test]
    public void Serialize_EmptySequenceRoot_ProducesValidYaml()
    {
        var empty = new YamlSequenceNode();
        var serialized = _adapter.Serialize(empty);
        Assert.That(serialized, Is.Not.Null);
        Assert.DoesNotThrow(() => _adapter.Parse(serialized));
    }
}
