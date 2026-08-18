using NUnit.Framework;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Contracts;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Commands;

/// <summary>
/// Merge command against the YAML adapter (issue #30) — key-based array
/// matching, deduplication, strategies and match settings.
/// </summary>
[TestFixture]
public class YamlMergeTests
{
    private IExecutionContext<YamlNode> _context = null!;

    [SetUp]
    public void SetUp() => _context = YamlExecutionContext.CreateDefault();

    private YamlNode Parse(string yaml) => _context.NodeAdapter.Parse(yaml);

    private static MergeSettings ArrayKeys(string arrayPath, params string[] keys) =>
        new()
        {
            ArraySettings =
            {
                new MergeArraySettings { ArrayPath = arrayPath, KeyPaths = keys.ToList() }
            }
        };

    private YamlNode Select(YamlNode data, string path) =>
        _context.ItemsFetcher.SelectNode(path, data)!;

    private string Scalar(YamlNode data, string path) =>
        ((YamlScalarNode)Select(data, path)).Value!;

    [Test]
    public void MergeObjects_AddsMissingKeys()
    {
        var data = Parse("""
            source:
              name: Alice
            target:
              id: 1
            """);

        var result = new Merge<YamlNode>("$.source", "$.target").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(Scalar(data, "$.target.name"), Is.EqualTo("Alice"));
        Assert.That(Scalar(data, "$.target.id"), Is.EqualTo("1"));
    }

    [Test]
    public void KeyPaths_MergeMatchingElementsAndAppendNewOnes()
    {
        var data = Parse("""
            source:
              items:
                - id: 1
                  name: one-updated
                - id: 3
                  name: three
            target:
              items:
                - id: 1
                  name: one
                - id: 2
                  name: two
            """);

        var result = new Merge<YamlNode>("$.source", "$.target", ArrayKeys("$.target.items", "id"))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var items = (YamlSequenceNode)Select(data, "$.target.items");
        Assert.That(items.Children.Count, Is.EqualTo(3));
        Assert.That(Scalar(data, "$.target.items[0].name"), Is.EqualTo("one-updated"));
        Assert.That(Scalar(data, "$.target.items[1].name"), Is.EqualTo("two"));
        Assert.That(Scalar(data, "$.target.items[2].id"), Is.EqualTo("3"));
    }

    [Test]
    public void KeyPaths_NestedKeyPathWithAtNotation()
    {
        var data = Parse("""
            source:
              rows:
                - key:
                    id: x
                  value: 20
            target:
              rows:
                - key:
                    id: x
                  value: 10
            """);

        new Merge<YamlNode>("$.source", "$.target", ArrayKeys("$.target.rows", "@.key.id"))
            .Execute(data, _context);

        var rows = (YamlSequenceNode)Select(data, "$.target.rows");
        Assert.That(rows.Children.Count, Is.EqualTo(1));
        Assert.That(Scalar(data, "$.target.rows[0].value"), Is.EqualTo("20"));
    }

    [Test]
    public void WithoutSettings_SequencesConcat()
    {
        var data = Parse("""
            source:
              tags: [b]
            target:
              tags: [a]
            """);

        new Merge<YamlNode>("$.source", "$.target").Execute(data, _context);

        var tags = (YamlSequenceNode)Select(data, "$.target.tags");
        Assert.That(tags.Children.Select(c => ((YamlScalarNode)c).Value),
            Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void ReplaceMode_ReplacesSequences()
    {
        var data = Parse("""
            source:
              tags: [b, c]
            target:
              tags: [a]
            """);

        new Merge<YamlNode>("$.source", "$.target", ArrayMergeMode.Replace).Execute(data, _context);

        var tags = (YamlSequenceNode)Select(data, "$.target.tags");
        Assert.That(tags.Children.Select(c => ((YamlScalarNode)c).Value),
            Is.EqualTo(new[] { "b", "c" }));
    }

    [Test]
    public void UniqueItemsWithoutKeys_SkipsDuplicates()
    {
        var data = Parse("""
            source:
              tags: [a, b]
            target:
              tags: [a]
            """);

        var settings = new MergeSettings
        {
            ArraySettings =
            {
                new MergeArraySettings { ArrayPath = "$.target.tags", UniqueItemsWithoutKeys = true }
            }
        };

        new Merge<YamlNode>("$.source", "$.target", settings).Execute(data, _context);

        var tags = (YamlSequenceNode)Select(data, "$.target.tags");
        Assert.That(tags.Children.Select(c => ((YamlScalarNode)c).Value),
            Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void OnlyStructure_KeepsExistingValues()
    {
        var data = Parse("""
            source:
              a: from-source
              c: new
            target:
              a: keep-me
            """);

        var settings = new MergeSettings { Strategy = MergeSettings.StrategyOnlyStructure };
        new Merge<YamlNode>("$.source", "$.target", settings).Execute(data, _context);

        Assert.That(Scalar(data, "$.target.a"), Is.EqualTo("keep-me"));
        Assert.That(Scalar(data, "$.target.c"), Is.EqualTo("new"));
    }

    [Test]
    public void OnlyValues_AddsNoNewKeys()
    {
        var data = Parse("""
            source:
              a: from-source
              c: new
            target:
              a: overwrite-me
            """);

        var settings = new MergeSettings { Strategy = MergeSettings.StrategyOnlyValues };
        new Merge<YamlNode>("$.source", "$.target", settings).Execute(data, _context);

        Assert.That(Scalar(data, "$.target.a"), Is.EqualTo("from-source"));
        Assert.That(_context.ItemsFetcher.SelectNode("$.target.c", data), Is.Null);
    }

    [Test]
    public void MatchSettings_SkipMergeWhenKeysDiffer()
    {
        var data = Parse("""
            source:
              id: 1
              extra: added
            target:
              id: 2
            """);

        var settings = new MergeSettings { MatchSettings = { KeyPaths = { "id" } } };
        new Merge<YamlNode>("$.source", "$.target", settings).Execute(data, _context);

        Assert.That(_context.ItemsFetcher.SelectNode("$.target.extra", data), Is.Null);
    }

    [Test]
    public void MergeByKeyMode_DeduplicatesScalars()
    {
        var data = Parse("""
            source:
              tags: [a, b]
            target:
              tags: [a]
            """);

        new Merge<YamlNode>("$.source", "$.target", ArrayMergeMode.MergeByKey).Execute(data, _context);

        var tags = (YamlSequenceNode)Select(data, "$.target.tags");
        Assert.That(tags.Children.Select(c => ((YamlScalarNode)c).Value),
            Is.EqualTo(new[] { "a", "b" }));
    }
}
