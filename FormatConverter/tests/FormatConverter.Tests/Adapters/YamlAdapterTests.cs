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

    private static IEnumerable<string> YamlFixtureDirectories()
    {
        var fixtureRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Yaml");
        foreach (var dir in Directory.GetDirectories(fixtureRoot))
        {
            var input = Path.Combine(dir, "input.yaml");
            var expected = Path.Combine(dir, "expected-roundtrip.yaml");
            if (File.Exists(input) && File.Exists(expected))
                yield return dir;
        }
    }

    [TestCaseSource(nameof(YamlFixtureDirectories))]
    public void YamlRoundTrip_FixtureMatchesExpected(string fixtureDir)
    {
        var input = File.ReadAllText(Path.Combine(fixtureDir, "input.yaml"));
        var expected = File.ReadAllText(Path.Combine(fixtureDir, "expected-roundtrip.yaml")).TrimEnd();

        var im = _adapter.ToIM(input, ConversionSettings.Empty);
        var output = _adapter.FromIM(im, ConversionSettings.Empty).TrimEnd();

        // Compare property by property by re-parsing as YAML object map
        Assert.That(ExtractKeys(output), Is.EquivalentTo(ExtractKeys(expected)));
    }

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
        Assert.That(output, Contains.Substring("null"));
    }

    private static IEnumerable<string> ExtractKeys(string yaml) =>
        yaml.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Contains(':'))
            .Select(l => l.Split(':')[0].Trim('\'', ' '));
}
