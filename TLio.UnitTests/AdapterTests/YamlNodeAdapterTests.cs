using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.UnitTests.AdapterTests;

/// <summary>
/// Tests for YamlNodeAdapter — all INodeAdapter&lt;YamlNode&gt; members.
/// </summary>
[TestFixture]
public class YamlNodeAdapterTests
{
    private YamlNodeAdapter _adapter = null!;
    private YamlParentTracker _tracker = null!;

    [SetUp]
    public void SetUp()
    {
        _tracker = new YamlParentTracker();
        _adapter = new YamlNodeAdapter(_tracker);
    }

    private static YamlMappingNode Map(params (string key, string val)[] pairs)
    {
        var node = new YamlMappingNode();
        foreach (var (k, v) in pairs)
            node.Children[new YamlScalarNode(k)] = new YamlScalarNode(v);
        return node;
    }

    private static YamlSequenceNode Seq(params string[] values)
    {
        var node = new YamlSequenceNode();
        foreach (var v in values) node.Add(new YamlScalarNode(v));
        return node;
    }

    private static YamlScalarNode Scalar(string? val) => new(val);

    // ── Type queries ──────────────────────────────────────────────────────────

    [Test]
    public void IsObject_TrueForMappingNode() => Assert.That(_adapter.IsObject(new YamlMappingNode()), Is.True);

    [Test]
    public void IsObject_FalseForSequence() => Assert.That(_adapter.IsObject(new YamlSequenceNode()), Is.False);

    [Test]
    public void IsArray_TrueForSequenceNode() => Assert.That(_adapter.IsArray(new YamlSequenceNode()), Is.True);

    [Test]
    public void IsArray_FalseForMapping() => Assert.That(_adapter.IsArray(new YamlMappingNode()), Is.False);

    [Test]
    public void IsPrimitive_TrueForScalarNode() => Assert.That(_adapter.IsPrimitive(Scalar("hello")), Is.True);

    [Test]
    public void IsPrimitive_FalseForMapping() => Assert.That(_adapter.IsPrimitive(new YamlMappingNode()), Is.False);

    [Test]
    public void IsNull_TrueForNullScalar() => Assert.That(_adapter.IsNull(Scalar("null")), Is.True);

    [Test]
    public void IsNull_TrueForTildeScalar() => Assert.That(_adapter.IsNull(Scalar("~")), Is.True);

    [Test]
    public void IsNull_FalseForNonNullScalar() => Assert.That(_adapter.IsNull(Scalar("hello")), Is.False);

    // ── Object operations ─────────────────────────────────────────────────────

    [Test]
    public void HasProperty_TrueWhenKeyPresent()
    {
        var map = Map(("name", "Alice"));
        Assert.That(_adapter.HasProperty(map, "name"), Is.True);
    }

    [Test]
    public void HasProperty_FalseWhenKeyAbsent()
    {
        var map = Map(("name", "Alice"));
        Assert.That(_adapter.HasProperty(map, "age"), Is.False);
    }

    [Test]
    public void GetProperty_ReturnsValue()
    {
        var map = Map(("name", "Alice"));
        var val = _adapter.GetProperty(map, "name") as YamlScalarNode;
        Assert.That(val?.Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void GetProperty_ReturnsNullForMissing()
    {
        var map = Map(("name", "Alice"));
        Assert.That(_adapter.GetProperty(map, "missing"), Is.Null);
    }

    [Test]
    public void SetProperty_AddsNewKey()
    {
        var map = new YamlMappingNode();
        _adapter.SetProperty(map, "key", Scalar("42"));
        Assert.That(_adapter.HasProperty(map, "key"), Is.True);
    }

    [Test]
    public void SetProperty_OverwritesExistingKey()
    {
        var map = Map(("key", "old"));
        _adapter.SetProperty(map, "key", Scalar("new"));
        var val = (_adapter.GetProperty(map, "key") as YamlScalarNode)?.Value;
        Assert.That(val, Is.EqualTo("new"));
    }

    [Test]
    public void RemoveProperty_RemovesKey()
    {
        var map = Map(("a", "1"), ("b", "2"));
        _adapter.RemoveProperty(map, "a");
        Assert.That(_adapter.HasProperty(map, "a"), Is.False);
        Assert.That(_adapter.HasProperty(map, "b"), Is.True);
    }

    [Test]
    public void GetPropertyNames_ReturnsAllKeys()
    {
        var map = Map(("x", "1"), ("y", "2"), ("z", "3"));
        var names = _adapter.GetPropertyNames(map).ToList();
        Assert.That(names, Is.EquivalentTo(new[] { "x", "y", "z" }));
    }

    // ── Array operations ──────────────────────────────────────────────────────

    [Test]
    public void AppendToArray_AddsElement()
    {
        var seq = new YamlSequenceNode();
        _adapter.AppendToArray(seq, Scalar("1"));
        _adapter.AppendToArray(seq, Scalar("2"));
        Assert.That(seq.Children.Count, Is.EqualTo(2));
    }

    [Test]
    public void InsertIntoArray_InsertsAtIndex()
    {
        var seq = Seq("1", "3");
        _adapter.InsertIntoArray(seq, 1, Scalar("2"));
        Assert.That(seq.Children.Count, Is.EqualTo(3));
        Assert.That((seq.Children[1] as YamlScalarNode)?.Value, Is.EqualTo("2"));
    }

    [Test]
    public void RemoveFromArray_RemovesAtIndex()
    {
        var seq = Seq("1", "2", "3");
        _adapter.RemoveFromArray(seq, 1);
        Assert.That(seq.Children.Count, Is.EqualTo(2));
        Assert.That((seq.Children[1] as YamlScalarNode)?.Value, Is.EqualTo("3"));
    }

    [Test]
    public void GetArrayLength_ReturnsCount()
    {
        var seq = Seq("a", "b", "c");
        Assert.That(_adapter.GetArrayLength(seq), Is.EqualTo(3));
    }

    [Test]
    public void GetArrayElement_ReturnsElement()
    {
        var seq = Seq("a", "b", "c");
        Assert.That((_adapter.GetArrayElement(seq, 1) as YamlScalarNode)?.Value, Is.EqualTo("b"));
    }

    [Test]
    public void GetArrayElements_ReturnsAllElements()
    {
        var seq = Seq("10", "20", "30");
        var values = _adapter.GetArrayElements(seq)
            .OfType<YamlScalarNode>().Select(s => s.Value).ToList();
        Assert.That(values, Is.EqualTo(new[] { "10", "20", "30" }));
    }

    // ── Node creation ─────────────────────────────────────────────────────────

    [Test]
    public void CreateNull_IsNull() => Assert.That(_adapter.IsNull(_adapter.CreateNull()), Is.True);

    [Test]
    public void CreateObject_IsObject() => Assert.That(_adapter.IsObject(_adapter.CreateObject()), Is.True);

    [Test]
    public void CreateArray_IsArray() => Assert.That(_adapter.IsArray(_adapter.CreateArray()), Is.True);

    [Test]
    public void CreateString_HasCorrectValue()
        => Assert.That((_adapter.CreateString("hi") as YamlScalarNode)?.Value, Is.EqualTo("hi"));

    [Test]
    public void CreateNumber_HasCorrectValue()
    {
        var node = _adapter.CreateNumber(3.14) as YamlScalarNode;
        Assert.That(double.Parse(node!.Value!, System.Globalization.CultureInfo.InvariantCulture),
            Is.EqualTo(3.14).Within(0.0001));
    }

    [Test]
    public void CreateBoolean_HasCorrectValue()
        => Assert.That((_adapter.CreateBoolean(true) as YamlScalarNode)?.Value, Is.EqualTo("true"));

    [Test]
    public void CreateValue_NullProducesNullNode()
        => Assert.That(_adapter.IsNull(_adapter.CreateValue(null)), Is.True);

    // ── Value access ──────────────────────────────────────────────────────────

    [Test]
    public void GetValue_ReturnsScalarValue() => Assert.That(_adapter.GetValue(Scalar("hello")), Is.EqualTo("hello"));

    [Test]
    public void GetValueT_ReturnsTypedValue() => Assert.That(_adapter.GetValue<int>(Scalar("42")), Is.EqualTo(42));

    // ── Type coercion ─────────────────────────────────────────────────────────

    [Test]
    public void TryGetBoolean_TrueForTrueString() => Assert.That(_adapter.TryGetBoolean(Scalar("true")), Is.True);

    [Test]
    public void TryGetBoolean_TrueForYesString() => Assert.That(_adapter.TryGetBoolean(Scalar("yes")), Is.True);

    [Test]
    public void TryGetBoolean_NullForNonBoolean() => Assert.That(_adapter.TryGetBoolean(Scalar("hello")), Is.Null);

    [Test]
    public void TryGetDouble_ReturnsDouble() => Assert.That(_adapter.TryGetDouble(Scalar("3.14")), Is.EqualTo(3.14).Within(0.0001));

    [Test]
    public void TryGetDouble_NullForNonNumeric() => Assert.That(_adapter.TryGetDouble(Scalar("hello")), Is.Null);

    [Test]
    public void TryGetString_ReturnsValue() => Assert.That(_adapter.TryGetString(Scalar("hello")), Is.EqualTo("hello"));

    [Test]
    public void TryGetString_NullForNull() => Assert.That(_adapter.TryGetString(Scalar("null")), Is.Null);

    // ── Cloning & replacement ─────────────────────────────────────────────────

    [Test]
    public void DeepClone_ProducesIndependentCopy()
    {
        var original = Map(("a", "1"));
        var clone = _adapter.DeepClone(original) as YamlMappingNode;
        clone!.Children[new YamlScalarNode("a")] = Scalar("99");
        // original must be unaffected
        Assert.That((original.Children[new YamlScalarNode("a")] as YamlScalarNode)?.Value, Is.EqualTo("1"));
    }

    [Test]
    public void Replace_SubstitutesValueInParentMapping()
    {
        var map = Map(("x", "1"));
        var child = map.Children[new YamlScalarNode("x")];
        _tracker.Track(child, map, "x");

        _adapter.Replace(child, Scalar("999"));

        var updated = (map.Children[new YamlScalarNode("x")] as YamlScalarNode)?.Value;
        Assert.That(updated, Is.EqualTo("999"));
    }

    [Test]
    public void RemoveFromParent_RemovesFromMapping()
    {
        var map = Map(("keep", "1"), ("remove", "2"));
        var child = map.Children[new YamlScalarNode("remove")];
        _tracker.Track(child, map, "remove");

        var result = _adapter.RemoveFromParent(child);
        Assert.That(result, Is.True);
        Assert.That(_adapter.HasProperty(map, "remove"), Is.False);
        Assert.That(_adapter.HasProperty(map, "keep"), Is.True);
    }

    [Test]
    public void RemoveFromParent_ReturnsFalseForUntrackedNode()
    {
        var node = Scalar("orphan");
        Assert.That(_adapter.RemoveFromParent(node), Is.False);
    }

    // ── Deep merge ────────────────────────────────────────────────────────────

    [Test]
    public void DeepMergeInto_MergesNewKeys()
    {
        var source = Map(("b", "2"));
        var target = Map(("a", "1"));
        _adapter.DeepMergeInto(source, target);
        Assert.That((_adapter.GetProperty(target, "a") as YamlScalarNode)?.Value, Is.EqualTo("1"));
        Assert.That((_adapter.GetProperty(target, "b") as YamlScalarNode)?.Value, Is.EqualTo("2"));
    }

    [Test]
    public void DeepMergeInto_OverwritesExistingPrimitive()
    {
        var source = Map(("a", "99"));
        var target = Map(("a", "1"));
        _adapter.DeepMergeInto(source, target);
        Assert.That((_adapter.GetProperty(target, "a") as YamlScalarNode)?.Value, Is.EqualTo("99"));
    }

    [Test]
    public void DeepMergeInto_ConcatModeAppendsArrayElements()
    {
        var source = new YamlMappingNode();
        source.Children[new YamlScalarNode("arr")] = Seq("3", "4");

        var target = new YamlMappingNode();
        target.Children[new YamlScalarNode("arr")] = Seq("1", "2");

        _adapter.DeepMergeInto(source, target, ArrayMergeMode.Concat);

        var arr = (YamlSequenceNode)target.Children[new YamlScalarNode("arr")];
        Assert.That(arr.Children.Count, Is.EqualTo(4));
    }

    [Test]
    public void DeepMergeInto_ReplaceModeReplacesArray()
    {
        var source = new YamlMappingNode();
        source.Children[new YamlScalarNode("arr")] = Seq("3", "4");

        var target = new YamlMappingNode();
        target.Children[new YamlScalarNode("arr")] = Seq("1", "2");

        _adapter.DeepMergeInto(source, target, ArrayMergeMode.Replace);

        var arr = (YamlSequenceNode)target.Children[new YamlScalarNode("arr")];
        Assert.That(arr.Children.Count, Is.EqualTo(2));
        Assert.That((arr.Children[0] as YamlScalarNode)?.Value, Is.EqualTo("3"));
    }

    // ── Parent access ─────────────────────────────────────────────────────────

    [Test]
    public void GetParentNode_ReturnsTrackedParent()
    {
        var map = Map(("child", "1"));
        var child = map.Children[new YamlScalarNode("child")];
        _tracker.Track(child, map, "child");

        Assert.That(_adapter.GetParentNode(child), Is.SameAs(map));
    }

    [Test]
    public void GetParentNode_NullForUntrackedNode()
    {
        var node = Scalar("orphan");
        Assert.That(_adapter.GetParentNode(node), Is.Null);
    }

    [Test]
    public void GetParentPropertyName_ReturnsKey()
    {
        var map = Map(("myProp", "42"));
        var child = map.Children[new YamlScalarNode("myProp")];
        _tracker.Track(child, map, "myProp");

        Assert.That(_adapter.GetParentPropertyName(child), Is.EqualTo("myProp"));
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    [Test]
    public void DeepEquals_TrueForIdenticalMappings()
    {
        var a = Map(("x", "1"), ("y", "2"));
        var b = Map(("x", "1"), ("y", "2"));
        Assert.That(_adapter.DeepEquals(a, b), Is.True);
    }

    [Test]
    public void DeepEquals_FalseForDifferentValues()
    {
        var a = Map(("x", "1"));
        var b = Map(("x", "2"));
        Assert.That(_adapter.DeepEquals(a, b), Is.False);
    }

    [Test]
    public void DeepEquals_TrueForIdenticalScalars()
    {
        Assert.That(_adapter.DeepEquals(Scalar("hello"), Scalar("hello")), Is.True);
    }

    [Test]
    public void DeepEquals_FalseForDifferentScalars()
    {
        Assert.That(_adapter.DeepEquals(Scalar("hello"), Scalar("world")), Is.False);
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    [Test]
    public void Parse_DeserializesYamlString()
    {
        var node = _adapter.Parse("name: Alice\nage: 30");
        Assert.That(_adapter.IsObject(node), Is.True);
        Assert.That((_adapter.GetProperty(node, "name") as YamlScalarNode)?.Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void Serialize_ProducesYamlString()
    {
        var map = Map(("a", "1"));
        var yaml = _adapter.Serialize(map);
        Assert.That(yaml, Does.Contain("a"));
        Assert.That(yaml, Does.Contain("1"));
    }
}
