using System.Xml.Linq;
using NUnit.Framework;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Models;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Contracts;

namespace TLio.Xml.Tests.SlashPath;

/// <summary>
/// The Compare command drives the whole diff through INodeAdapter / IItemsFetcher,
/// so the structural diff must behave the same over XML documents as over JSON.
/// </summary>
[TestFixture]
public class XmlCompareTests
{
    private IExecutionContext<XElement> _context = null!;

    [SetUp]
    public void SetUp() => _context = XmlExecutionContext.CreateWithSlashPaths();

    private static List<XElement> Results(XElement data)
    {
        var result = data.Element("result");
        Assert.That(result, Is.Not.Null, $"No result element written. Document: {data}");
        return result!.Elements().ToList();
    }

    private static string? Field(XElement entry, string name) => entry.Element(name)?.Value;

    // ── Scalar back-compat ────────────────────────────────────────────────────

    [Test]
    public void Primitives_WriteScalarResult()
    {
        var data = XElement.Parse("<root><a>2</a><b>1</b></root>");

        var result = new Compare<XElement>("/a", "/b", "/result").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.Element("result")?.Value, Is.EqualTo("greater"));
    }

    [Test]
    public void Primitives_WithEqualTextUnderDifferentElementNames_AreEqual()
    {
        var data = XElement.Parse("<root><a>hello</a><b>hello</b></root>");

        new Compare<XElement>("/a", "/b", "/result").Execute(data, _context);

        Assert.That(data.Element("result")?.Value, Is.EqualTo("equal"));
    }

    // ── Structural diff ───────────────────────────────────────────────────────

    [Test]
    public void ObjectDiff_ReportsValueDifferenceWithXmlPaths()
    {
        var data = XElement.Parse(
            "<root><first><a>1</a><b>x</b></first><second><a>2</a><b>x</b></second></root>");

        var result = new Compare<XElement>("/first", "/second", "/result").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var entries = Results(data);
        var valueDiffs = entries.Where(e => Field(e, "differenceType") == "valueDifference").ToList();
        Assert.That(valueDiffs, Has.Count.EqualTo(1));
        Assert.That(Field(valueDiffs[0], "firstPath"), Is.EqualTo("/first/a"));
        Assert.That(Field(valueDiffs[0], "secondPath"), Is.EqualTo("/second/a"));
        Assert.That(Field(valueDiffs[0], "foundDifference"), Is.EqualTo("true"));
    }

    [Test]
    public void ObjectDiff_EqualContentUnderDifferentElementNames_ReportsNoDifference()
    {
        var data = XElement.Parse(
            "<root><first><a>1</a><b>x</b></first><second><a>1</a><b>x</b></second></root>");

        new Compare<XElement>("/first", "/second", "/result").Execute(data, _context);

        var entries = Results(data);
        Assert.That(entries.All(e => Field(e, "foundDifference") == "false"), Is.True,
            $"Unexpected differences: {data.Element("result")}");
    }

    [Test]
    public void ObjectDiff_ElementOnOneSideOnly_ReportsStructureDifference()
    {
        var data = XElement.Parse(
            "<root><first><a>1</a><only>y</only></first><second><a>1</a><b>x</b></second></root>");

        new Compare<XElement>("/first", "/second", "/result").Execute(data, _context);

        var structureDiffs = Results(data)
            .Where(e => Field(e, "differenceType") == "structureDifference").ToList();
        Assert.That(structureDiffs, Has.Count.EqualTo(2));
        Assert.That(structureDiffs.Select(e => Field(e, "firstPath")),
            Is.EquivalentTo(new[] { "/first/only", "/first/b" }));
    }

    [Test]
    public void ArrayDiff_RepeatedElementsAreComparedPositionally()
    {
        var data = XElement.Parse(
            "<root>" +
            "<first><item><id>1</id><v>a</v></item><item><id>2</id><v>b</v></item></first>" +
            "<second><item><id>1</id><v>a</v></item><item><id>2</id><v>z</v></item></second>" +
            "</root>");

        new Compare<XElement>("/first", "/second", "/result").Execute(data, _context);

        var entries = Results(data);
        Assert.That(entries.Any(e => Field(e, "differenceType") == "arrayDifference" &&
                                     Field(e, "description")!.Contains("2 items")), Is.True);
        var valueDiffs = entries.Where(e => Field(e, "differenceType") == "valueDifference").ToList();
        Assert.That(valueDiffs, Has.Count.EqualTo(1));
        Assert.That(Field(valueDiffs[0], "firstPath"), Is.EqualTo("/first/item/v"));
    }

    [Test]
    public void ArrayDiff_KeyBasedMatchingWorksOverXml()
    {
        var data = XElement.Parse(
            "<root>" +
            "<first><item><id>1</id><v>a</v></item><item><id>2</id><v>b</v></item></first>" +
            "<second><item><id>2</id><v>b</v></item><item><id>1</id><v>a</v></item></second>" +
            "</root>");

        var settings = new CompareSettings
        {
            ArraySettings = { new CompareArraySettings { ArrayPath = "/first", KeyPaths = { "id" } } }
        };

        new Compare<XElement>("/first", "/second", "/result", settings).Execute(data, _context);

        Assert.That(Results(data).All(e => Field(e, "foundDifference") == "false"), Is.True,
            $"Expected reordered items to match by key. Got: {data.Element("result")}");
    }

    [Test]
    public void ResultTypes_FilterAppliesOverXml()
    {
        var data = XElement.Parse(
            "<root><first><a>1</a><only>y</only></first><second><a>2</a><b>x</b></second></root>");

        var settings = new CompareSettings { ResultTypes = { DifferenceType.ValueDifference } };

        new Compare<XElement>("/first", "/second", "/result", settings).Execute(data, _context);

        var entries = Results(data);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(Field(entries[0], "differenceType"), Is.EqualTo("valueDifference"));
    }
}
