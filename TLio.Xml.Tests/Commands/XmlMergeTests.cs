using System.Xml.Linq;
using NUnit.Framework;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Contracts;
using TLio.Xml;

namespace TLio.Xml.Tests.Commands;

/// <summary>
/// Merge command against the XML adapter (issue #30).
///
/// XML has no native array type — an element whose children all share one name
/// looks like both an object and an array. The merge command therefore only uses
/// array semantics for such an element when the script declares array settings
/// for its path, which keeps plain object merges unchanged.
/// </summary>
[TestFixture]
public class XmlMergeTests
{
    private IExecutionContext<XElement> _context = null!;

    [SetUp]
    public void SetUp() => _context = XmlExecutionContext.CreateWithSlashPaths();

    private const string ItemsDocument = """
        <root>
          <source>
            <items>
              <item><id>1</id><name>one-updated</name></item>
              <item><id>3</id><name>three</name></item>
            </items>
          </source>
          <target>
            <items>
              <item><id>1</id><name>one</name></item>
              <item><id>2</id><name>two</name></item>
            </items>
          </target>
        </root>
        """;

    private static MergeSettings ArrayKeys(string arrayPath, params string[] keys) =>
        new()
        {
            ArraySettings =
            {
                new MergeArraySettings { ArrayPath = arrayPath, KeyPaths = keys.ToList() }
            }
        };

    [Test]
    public void MergeObjects_AddsMissingElements()
    {
        var data = XElement.Parse("""
            <root>
              <source><name>Alice</name></source>
              <target><id>1</id></target>
            </root>
            """);

        var result = new Merge<XElement>("/source", "/target").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.Element("target")!.Element("name")!.Value, Is.EqualTo("Alice"));
        Assert.That(data.Element("target")!.Element("id")!.Value, Is.EqualTo("1"));
    }

    [Test]
    public void KeyPaths_MergeMatchingElementsAndAppendNewOnes()
    {
        var data = XElement.Parse(ItemsDocument);

        var result = new Merge<XElement>("/source", "/target", ArrayKeys("/target/items", "id"))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var items = data.Element("target")!.Element("items")!.Elements("item").ToList();
        Assert.That(items.Count, Is.EqualTo(3));
        Assert.That(items[0].Element("name")!.Value, Is.EqualTo("one-updated"));
        Assert.That(items[1].Element("name")!.Value, Is.EqualTo("two"));
        Assert.That(items[2].Element("id")!.Value, Is.EqualTo("3"));
    }

    [Test]
    public void WithoutArraySettings_RepeatedElementsUseObjectSemantics()
    {
        // Backwards compatible: without array settings XML repeated elements are
        // merged as objects (first matching child), exactly as before.
        var data = XElement.Parse(ItemsDocument);

        new Merge<XElement>("/source", "/target").Execute(data, _context);

        var items = data.Element("target")!.Element("items")!.Elements("item").ToList();
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[0].Element("name")!.Value, Is.EqualTo("one-updated"));
    }

    [Test]
    public void UniqueItemsWithoutKeys_SkipsDuplicateElements()
    {
        var data = XElement.Parse("""
            <root>
              <source><tags><tag>a</tag><tag>b</tag></tags></source>
              <target><tags><tag>a</tag></tags></target>
            </root>
            """);

        var settings = new MergeSettings
        {
            ArraySettings =
            {
                new MergeArraySettings { ArrayPath = "/target/tags", UniqueItemsWithoutKeys = true }
            }
        };

        new Merge<XElement>("/source", "/target", settings).Execute(data, _context);

        var tags = data.Element("target")!.Element("tags")!.Elements("tag")
            .Select(t => t.Value).ToList();
        Assert.That(tags, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void OnlyStructure_KeepsExistingValues()
    {
        var data = XElement.Parse("""
            <root>
              <source><a>from-source</a><c>new</c></source>
              <target><a>keep-me</a></target>
            </root>
            """);

        var settings = new MergeSettings { Strategy = MergeSettings.StrategyOnlyStructure };
        new Merge<XElement>("/source", "/target", settings).Execute(data, _context);

        Assert.That(data.Element("target")!.Element("a")!.Value, Is.EqualTo("keep-me"));
        Assert.That(data.Element("target")!.Element("c")!.Value, Is.EqualTo("new"));
    }

    [Test]
    public void OnlyValues_AddsNoNewElements()
    {
        var data = XElement.Parse("""
            <root>
              <source><a>from-source</a><c>new</c></source>
              <target><a>overwrite-me</a></target>
            </root>
            """);

        var settings = new MergeSettings { Strategy = MergeSettings.StrategyOnlyValues };
        new Merge<XElement>("/source", "/target", settings).Execute(data, _context);

        Assert.That(data.Element("target")!.Element("a")!.Value, Is.EqualTo("from-source"));
        Assert.That(data.Element("target")!.Element("c"), Is.Null);
    }

    [Test]
    public void MatchSettings_SkipMergeWhenKeysDiffer()
    {
        var data = XElement.Parse("""
            <root>
              <source><id>1</id><extra>added</extra></source>
              <target><id>2</id></target>
            </root>
            """);

        var settings = new MergeSettings { MatchSettings = { KeyPaths = { "id" } } };
        new Merge<XElement>("/source", "/target", settings).Execute(data, _context);

        Assert.That(data.Element("target")!.Element("extra"), Is.Null);
    }

    [Test]
    public void NativeXPathContext_SupportsKeyPaths()
    {
        var context = XmlExecutionContext.CreateWithNativeXPath();
        var data = XElement.Parse(ItemsDocument);

        var result = new Merge<XElement>("source", "target", ArrayKeys("target/items", "id"))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        var items = data.Element("target")!.Element("items")!.Elements("item").ToList();
        Assert.That(items.Count, Is.EqualTo(3));
        Assert.That(items[0].Element("name")!.Value, Is.EqualTo("one-updated"));
    }
}
