using FormatConverter.Core;
using FormatConverter.Core.Exceptions;
using FormatConverter.Core.Model;
using FormatConverter.Yaml;
using NUnit.Framework;

namespace FormatConverter.Tests.Adapters;

[TestFixture]
public sealed class YamlAdapterTests
{
    private YamlFormatAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new YamlFormatAdapter();

    [Test]
    public void FormatId_IsYaml()
    {
        Assert.That(_adapter.FormatId, Is.EqualTo("yaml"));
    }

    // ── Fixture-based round-trip tests ───────────────────────────────────────

    private static IEnumerable<string[]> YamlFixtureDirectories()
    {
        var fixtureRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Yaml");
        foreach (var dir in Directory.GetDirectories(fixtureRoot))
        {
            var input = Path.Combine(dir, "input.yaml");
            var expected = Path.Combine(dir, "expected-roundtrip.yaml");
            if (File.Exists(input) && File.Exists(expected))
                yield return new[] { dir };
        }
    }

    [TestCaseSource(nameof(YamlFixtureDirectories))]
    public void YamlRoundTrip_FixtureMatchesExpected(string fixtureDir)
    {
        var input = File.ReadAllText(Path.Combine(fixtureDir, "input.yaml"));
        var expected = File.ReadAllText(Path.Combine(fixtureDir, "expected-roundtrip.yaml")).TrimEnd();

        var im = _adapter.ToIM(input, ConversionSettings.Empty);
        var output = _adapter.FromIM(im, ConversionSettings.Empty).TrimEnd();

        YamlAssert.Equivalent(output, expected, $"round trip of {Path.GetFileName(fixtureDir)}");
    }

    // ── Emitter correctness ──────────────────────────────────────────────────

    [Test]
    public void FromIM_ArrayItemWithSeveralKeys_ReParses()
    {
        // The regression: the second and later keys of an array item were written one indent
        // level too deep, producing YAML that YAML could not read back.
        const string input = """
            lines:
              - sku: A1
                qty: '2'
                note: first
              - sku: B2
                qty: '1'
                note: second
            """;

        var output = _adapter.FromIM(_adapter.ToIM(input, ConversionSettings.Empty), ConversionSettings.Empty);

        YamlAssert.Equivalent(output, input, "an array of multi-key objects");
    }

    [Test]
    public void FromIM_StringThatLooksLikeAScalar_SurvivesInferTypes()
    {
        // Quoting is what stops "42" coming back as the number 42 on the next hop.
        var im = new ObjectNode
        {
            Children =
            {
                new ScalarNode(ScalarType.String, "42") { Name = "code" },
                new ScalarNode(ScalarType.String, "true") { Name = "flag" },
                new ScalarNode(ScalarType.String, "null") { Name = "word" },
                new ScalarNode(ScalarType.Integer, "42") { Name = "count" },
            },
        };

        var yaml = _adapter.FromIM(im, ConversionSettings.Empty);
        var reread = (ObjectNode)_adapter.ToIM(yaml, new ConversionSettings { InferTypes = true });

        Assert.Multiple(() =>
        {
            Assert.That(Child(reread, "code").Type, Is.EqualTo(ScalarType.String));
            Assert.That(Child(reread, "flag").Type, Is.EqualTo(ScalarType.String));
            Assert.That(Child(reread, "word").Type, Is.EqualTo(ScalarType.String));
            Assert.That(Child(reread, "count").Type, Is.EqualTo(ScalarType.Integer));
        });
    }

    [Test]
    public void FromIM_MetadataKeys_AreEmittedAndReadBack()
    {
        const string xmlish = """
            item:
              '@id': '42'
              label: Test
            """;

        var im = (ObjectNode)_adapter.ToIM(xmlish, ConversionSettings.Empty);
        var item = (ObjectNode)im.Children.Single();

        Assert.That(item.Metadata, Does.ContainKey("@id"), "an @-prefixed key is metadata, not a child");
        Assert.That(item.Children.Select(c => c.Name), Is.EqualTo(new[] { "label" }));

        YamlAssert.Equivalent(_adapter.FromIM(im, ConversionSettings.Empty), xmlish, "metadata round trip");
    }

    private static ScalarNode Child(ObjectNode node, string name) =>
        (ScalarNode)node.Children.Single(c => c.Name == name);

    // ── InferTypes tests ─────────────────────────────────────────────────────

    [Test]
    public void ToIM_InferTypes_True_IntegerKey_ProducesIntegerScalar()
    {
        var yaml = "count: 42\n";
        var settings = new ConversionSettings { InferTypes = true };
        var im = _adapter.ToIM(yaml, settings);

        var obj = (ObjectNode)im;
        var child = (ScalarNode)obj.Children[0];
        Assert.That(child.Type, Is.EqualTo(ScalarType.Integer));
        Assert.That(child.RawValue, Is.EqualTo("42"));
    }

    [Test]
    public void ToIM_InferTypes_True_BooleanKey_ProducesBooleanScalar()
    {
        var yaml = "active: true\n";
        var settings = new ConversionSettings { InferTypes = true };
        var im = _adapter.ToIM(yaml, settings);

        var obj = (ObjectNode)im;
        var child = (ScalarNode)obj.Children[0];
        Assert.That(child.Type, Is.EqualTo(ScalarType.Boolean));
    }

    [Test]
    public void ToIM_InferTypes_False_AllStrings()
    {
        var yaml = "count: 42\n";
        var im = _adapter.ToIM(yaml, ConversionSettings.Empty);

        var obj = (ObjectNode)im;
        var child = (ScalarNode)obj.Children[0];
        Assert.That(child.Type, Is.EqualTo(ScalarType.String));
    }

    [Test]
    public void ToIM_EmptyDocument_ProducesObjectNode()
    {
        var im = _adapter.ToIM("{}", ConversionSettings.Empty);
        Assert.That(im, Is.InstanceOf<ObjectNode>());
    }

    [Test]
    public void ToIM_MalformedYaml_ThrowsFormatParseException()
    {
        var ex = Assert.Throws<FormatParseException>(() => _adapter.ToIM("key: [unclosed", ConversionSettings.Empty));
        Assert.That(ex.FormatId, Is.EqualTo("yaml"));
        Assert.That(ex.Operation, Is.EqualTo("ToIM"));
    }

    [Test]
    public void FromIM_ObjectNode_WritesYamlMapping()
    {
        var obj = new ObjectNode
        {
            Children = { new ScalarNode(ScalarType.String, "Alice") { Name = "name" } }
        };
        var output = _adapter.FromIM(obj, ConversionSettings.Empty);
        Assert.That(output, Contains.Substring("name: Alice"));
    }

    [Test]
    public void FromIM_NullScalar_WritesYamlNull()
    {
        var node = new ObjectNode
        {
            Children = { new ScalarNode(ScalarType.Null, null) { Name = "val" } }
        };
        var output = _adapter.FromIM(node, ConversionSettings.Empty);
        YamlAssert.Equivalent(output, "val: null", "an explicit null scalar");
    }
}
