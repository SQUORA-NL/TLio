using NUnit.Framework;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Models;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Contracts;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

/// <summary>
/// The Compare command drives the whole diff through INodeAdapter / IItemsFetcher,
/// so the structural diff must behave the same over YAML documents as over JSON.
/// </summary>
[TestFixture]
public class YamlCompareTests
{
    private ExecutionContext<YamlNode> _context = null!;
    private YamlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _context = YamlExecutionContext.CreateDefault();
        _adapter = (YamlNodeAdapter)_context.NodeAdapter;
    }

    private YamlNode Parse(string yaml) => _adapter.Parse(yaml);

    private List<YamlMappingNode> Results(YamlNode data)
    {
        var node = _context.ItemsFetcher.SelectNode("$.result", data);
        Assert.That(node, Is.InstanceOf<YamlSequenceNode>(),
            $"Expected a structured result sequence but got: {node}");
        return ((YamlSequenceNode)node!).Children.Cast<YamlMappingNode>().ToList();
    }

    private static string? Field(YamlMappingNode entry, string name) =>
        entry.Children.TryGetValue(new YamlScalarNode(name), out var v) ? ((YamlScalarNode)v).Value : null;

    // ── Scalar back-compat ────────────────────────────────────────────────────

    [Test]
    public void Primitives_WriteScalarResult()
    {
        var data = Parse("a: 2\nb: 1\n");

        var result = new Compare<YamlNode>("$.a", "$.b", "$.result").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var scalar = _context.ItemsFetcher.SelectNode("$.result", data) as YamlScalarNode;
        Assert.That(scalar?.Value, Is.EqualTo("greater"));
    }

    // ── Structural diff ───────────────────────────────────────────────────────

    [Test]
    public void ObjectDiff_ReportsValueDifferenceWithYamlPaths()
    {
        var data = Parse("first:\n  a: 1\n  b: x\nsecond:\n  a: 2\n  b: x\n");

        var result = new Compare<YamlNode>("$.first", "$.second", "$.result").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var valueDiffs = Results(data).Where(e => Field(e, "differenceType") == "valueDifference").ToList();
        Assert.That(valueDiffs, Has.Count.EqualTo(1));
        Assert.That(Field(valueDiffs[0], "firstPath"), Is.EqualTo("$.first.a"));
        Assert.That(Field(valueDiffs[0], "secondPath"), Is.EqualTo("$.second.a"));
        Assert.That(Field(valueDiffs[0], "differenceSubType"), Is.EqualTo("lessThan"));
    }

    [Test]
    public void ObjectDiff_EqualMappings_ReportNoDifference()
    {
        var data = Parse("first:\n  a: 1\n  b: x\nsecond:\n  a: 1\n  b: x\n");

        new Compare<YamlNode>("$.first", "$.second", "$.result").Execute(data, _context);

        Assert.That(Results(data).All(e => Field(e, "foundDifference") == "false"), Is.True);
    }

    [Test]
    public void ObjectDiff_KeyOnOneSideOnly_ReportsStructureDifference()
    {
        var data = Parse("first:\n  a: 1\n  only: y\nsecond:\n  a: 1\n  b: x\n");

        new Compare<YamlNode>("$.first", "$.second", "$.result").Execute(data, _context);

        var structureDiffs = Results(data)
            .Where(e => Field(e, "differenceType") == "structureDifference").ToList();
        Assert.That(structureDiffs, Has.Count.EqualTo(2));
        Assert.That(structureDiffs.Select(e => Field(e, "firstPath")),
            Is.EquivalentTo(new[] { "$.first.only", "$.first.b" }));
    }

    [Test]
    public void ArrayDiff_SequencesAreComparedPositionally()
    {
        var data = Parse(
            "first:\n  - id: 1\n    v: a\n  - id: 2\n    v: b\n" +
            "second:\n  - id: 1\n    v: a\n  - id: 2\n    v: z\n");

        new Compare<YamlNode>("$.first", "$.second", "$.result").Execute(data, _context);

        var entries = Results(data);
        Assert.That(entries.Any(e => Field(e, "differenceType") == "arrayDifference" &&
                                     Field(e, "description")!.Contains("2 items")), Is.True);
        var valueDiffs = entries.Where(e => Field(e, "differenceType") == "valueDifference").ToList();
        Assert.That(valueDiffs, Has.Count.EqualTo(1));
        Assert.That(Field(valueDiffs[0], "firstPath"), Is.EqualTo("$.first[1].v"));
    }

    [Test]
    public void ArrayDiff_DifferentLengths_ReportExtraItems()
    {
        var data = Parse("first:\n  - 1\n  - 2\n  - 3\nsecond:\n  - 1\n  - 2\n");

        new Compare<YamlNode>("$.first", "$.second", "$.result").Execute(data, _context);

        var descriptions = Results(data).Select(e => Field(e, "description")!).ToList();
        Assert.That(descriptions.Any(d => d.Contains("different number of items")), Is.True);
        Assert.That(descriptions.Any(d => d.Contains("first array only")), Is.True);
    }

    [Test]
    public void ArrayDiff_KeyBasedMatchingWorksOverYaml()
    {
        var data = Parse(
            "first:\n  - id: 1\n    v: a\n  - id: 2\n    v: b\n" +
            "second:\n  - id: 2\n    v: b\n  - id: 1\n    v: a\n");

        var settings = new CompareSettings
        {
            ArraySettings = { new CompareArraySettings { ArrayPath = "$.first", KeyPaths = { "@.id" } } }
        };

        new Compare<YamlNode>("$.first", "$.second", "$.result", settings).Execute(data, _context);

        Assert.That(Results(data).All(e => Field(e, "foundDifference") == "false"), Is.True);
    }

    [Test]
    public void ResultTypes_FilterAppliesOverYaml()
    {
        var data = Parse("first:\n  a: 1\n  only: y\nsecond:\n  a: 2\n  b: x\n");

        var settings = new CompareSettings { ResultTypes = { DifferenceType.StructureDifference } };

        new Compare<YamlNode>("$.first", "$.second", "$.result", settings).Execute(data, _context);

        var entries = Results(data);
        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries.All(e => Field(e, "differenceType") == "structureDifference"), Is.True);
    }
}
