using NUnit.Framework;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Adapters;

[TestFixture]
public class YamlNodeAdapterTests
{
    private YamlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new YamlNodeAdapter(new YamlParentTracker());

    // ── Type queries ──────────────────────────────────────────────────────────

    [Test]
    public void IsObject_MappingNode_ReturnsTrue()
    {
        var node = new YamlMappingNode();
        Assert.That(_adapter.IsObject(node), Is.True);
    }

    [Test]
    public void IsObject_ScalarNode_ReturnsFalse()
    {
        var node = new YamlScalarNode("hello");
        Assert.That(_adapter.IsObject(node), Is.False);
    }

    [Test]
    public void IsArray_SequenceNode_ReturnsTrue()
    {
        var node = new YamlSequenceNode();
        Assert.That(_adapter.IsArray(node), Is.True);
    }

    [Test]
    public void IsArray_MappingNode_ReturnsFalse()
    {
        var node = new YamlMappingNode();
        Assert.That(_adapter.IsArray(node), Is.False);
    }

    [Test]
    public void IsPrimitive_ScalarNode_ReturnsTrue()
    {
        var node = new YamlScalarNode("42");
        Assert.That(_adapter.IsPrimitive(node), Is.True);
    }

    [Test]
    public void IsNull_NullScalar_ReturnsTrue()
    {
        var node = new YamlScalarNode("null");
        Assert.That(_adapter.IsNull(node), Is.True);
    }

    [Test]
    public void IsNull_TildeScalar_ReturnsTrue()
    {
        var node = new YamlScalarNode("~");
        Assert.That(_adapter.IsNull(node), Is.True);
    }

    [Test]
    public void IsNull_NonNullScalar_ReturnsFalse()
    {
        var node = new YamlScalarNode("hello");
        Assert.That(_adapter.IsNull(node), Is.False);
    }

    // ── Property access ───────────────────────────────────────────────────────

    [Test]
    public void GetProperty_ExistingKey_ReturnsValue()
    {
        var map = new YamlMappingNode
        {
            { "city", new YamlScalarNode("Amsterdam") }
        };
        var result = _adapter.GetProperty(map, "city");
        Assert.That(result, Is.Not.Null);
        Assert.That(((YamlScalarNode)result!).Value, Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void GetProperty_MissingKey_ReturnsNull()
    {
        var map = new YamlMappingNode { { "city", new YamlScalarNode("Amsterdam") } };
        Assert.That(_adapter.GetProperty(map, "country"), Is.Null);
    }

    [Test]
    public void HasProperty_ExistingKey_ReturnsTrue()
    {
        var map = new YamlMappingNode { { "name", new YamlScalarNode("Alice") } };
        Assert.That(_adapter.HasProperty(map, "name"), Is.True);
    }

    [Test]
    public void HasProperty_MissingKey_ReturnsFalse()
    {
        var map = new YamlMappingNode { { "name", new YamlScalarNode("Alice") } };
        Assert.That(_adapter.HasProperty(map, "age"), Is.False);
    }

    [Test]
    public void GetPropertyNames_ReturnsAllKeys()
    {
        var map = new YamlMappingNode
        {
            { "a", new YamlScalarNode("1") },
            { "b", new YamlScalarNode("2") },
            { "c", new YamlScalarNode("3") }
        };
        var names = _adapter.GetPropertyNames(map).ToList();
        Assert.That(names, Is.EquivalentTo(new[] { "a", "b", "c" }));
    }

    // ── Value access ──────────────────────────────────────────────────────────

    [Test]
    public void TryGetString_ScalarNode_ReturnsValue()
    {
        var node = new YamlScalarNode("hello");
        Assert.That(_adapter.TryGetString(node), Is.EqualTo("hello"));
    }

    [Test]
    public void TryGetBoolean_TrueScalar_ReturnsTrue()
    {
        var node = new YamlScalarNode("true");
        Assert.That(_adapter.TryGetBoolean(node), Is.True);
    }

    [Test]
    public void TryGetBoolean_YesScalar_ReturnsTrue()
    {
        var node = new YamlScalarNode("yes");
        Assert.That(_adapter.TryGetBoolean(node), Is.True);
    }

    [Test]
    public void TryGetBoolean_FalseScalar_ReturnsFalse()
    {
        var node = new YamlScalarNode("false");
        Assert.That(_adapter.TryGetBoolean(node), Is.False);
    }

    [Test]
    public void TryGetDouble_NumericScalar_ReturnsParsedValue()
    {
        var node = new YamlScalarNode("3.14");
        Assert.That(_adapter.TryGetDouble(node), Is.EqualTo(3.14).Within(0.0001));
    }

    [Test]
    public void TryGetDouble_NonNumericScalar_ReturnsNull()
    {
        var node = new YamlScalarNode("not-a-number");
        Assert.That(_adapter.TryGetDouble(node), Is.Null);
    }

    // ── Array operations ──────────────────────────────────────────────────────

    [Test]
    public void GetArrayLength_SequenceNode_ReturnsCount()
    {
        var seq = new YamlSequenceNode(
            new YamlScalarNode("a"),
            new YamlScalarNode("b"),
            new YamlScalarNode("c"));
        Assert.That(_adapter.GetArrayLength(seq), Is.EqualTo(3));
    }

    [Test]
    public void GetArrayElement_ReturnsCorrectElement()
    {
        var seq = new YamlSequenceNode(
            new YamlScalarNode("first"),
            new YamlScalarNode("second"));
        var el = _adapter.GetArrayElement(seq, 1);
        Assert.That(((YamlScalarNode)el).Value, Is.EqualTo("second"));
    }

    // ── Clone & equality ──────────────────────────────────────────────────────

    [Test]
    public void DeepClone_ProducesEqualButDistinctNode()
    {
        var original = new YamlMappingNode { { "name", new YamlScalarNode("Alice") } };
        var clone = _adapter.DeepClone(original);
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(_adapter.DeepEquals(original, clone), Is.True);
    }

    [Test]
    public void DeepClone_MutatingCloneDoesNotAffectOriginal()
    {
        var original = new YamlMappingNode { { "name", new YamlScalarNode("Alice") } };
        var clone = (YamlMappingNode)_adapter.DeepClone(original);
        clone.Children[new YamlScalarNode("name")] = new YamlScalarNode("Bob");
        var originalName = ((YamlScalarNode)original.Children[new YamlScalarNode("name")]).Value;
        Assert.That(originalName, Is.EqualTo("Alice"));
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    [Test]
    public void Parse_ValidYaml_ReturnsMappingNode()
    {
        var node = _adapter.Parse("name: Alice\nage: 30");
        Assert.That(_adapter.IsObject(node), Is.True);
    }

    [Test]
    public void Parse_EmptyString_ReturnsEmptySequence()
    {
        var node = _adapter.Parse(string.Empty);
        Assert.That(_adapter.IsArray(node), Is.True);
        Assert.That(_adapter.GetArrayLength(node), Is.EqualTo(0));
    }
}
