using FormatConverter.Core;
using FormatConverter.Json;
using FormatConverter.Xml;
using FormatConverter.Yaml;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using Converter = global::FormatConverter.Core.FormatConverter;

namespace FormatConverter.Tests.Integration;

/// <summary>
/// The guard that stops the converter and TLio drifting apart on what a document looks like.
/// </summary>
/// <remarks>
/// <para>
/// A convert in the middle of a script is only meaningful if the commands that run after it
/// address the tree they were promised. That makes the converter's output subject to
/// <c>docs/ai-ref/adapters/document-shape.md</c>, which TLio's own adapters implement — so these
/// tests walk converted output with <c>XmlNodeAdapter</c> and <c>YamlNodeAdapter</c> and assert
/// they see the same object / array / scalar structure the source JSON described.
/// </para>
/// <para>
/// Before the canonical shape landed, <c>{"order":{"lines":[a,b]}}</c> converted to
/// <c>&lt;order&gt;&lt;lines/&gt;&lt;lines/&gt;&lt;/order&gt;</c>, in which every child of
/// <c>order</c> shares one name — so <c>XmlNodeAdapter</c> reported <c>order</c> itself as an
/// array, and a following XPath command addressed something that was not there.
/// </para>
/// </remarks>
[TestFixture]
public sealed class CanonicalShapeTests
{
    private Converter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = new Converter();
        _converter.Register(new JsonFormatAdapter());
        _converter.Register(new XmlFormatAdapter());
        _converter.Register(new YamlFormatAdapter());
    }

    private static readonly string[] Documents =
    [
        """{"order":{"id":"7","status":"new"}}""",
        """{"order":{"lines":[{"sku":"A1"},{"sku":"B2"}]}}""",
        """{"order":{"lines":[{"sku":"A1"}]}}""",
        """{"order":{"tags":["red","green","blue"]}}""",
        """{"order":{"customer":{"address":{"city":"Amsterdam"}}}}""",
        """{"order":{"lines":[{"sku":"A1","parts":["x","y"]}]}}""",
        """{"order":{"note":null}}""",
        """{"order":{"empty":[]}}""",
    ];

    [TestCaseSource(nameof(Documents))]
    public void ConvertedXml_ReadsBackThroughTLiosOwnAdapter(string json)
    {
        var xml = _converter.Convert("json", json, "xml", ConversionSettings.Empty);

        var jsonAdapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var xmlContext = XmlExecutionContext.CreateDefault();
        var converted = xmlContext.NodeAdapter.Parse(xml);

        // "The root object is the document element" — so <order> is the JSON root's single
        // property *value*, and the property name is the element's own name.
        var source = JToken.Parse(json);
        var rootName = jsonAdapter.GetPropertyNames(source).Single();
        Assert.That(converted.Name.LocalName, Is.EqualTo(rootName), $"document element name.\n{xml}");

        AssertSameShape(
            jsonAdapter.GetProperty(source, rootName)!, jsonAdapter,
            converted, xmlContext.NodeAdapter,
            $"$.{rootName}", xml);
    }

    [TestCaseSource(nameof(Documents))]
    public void ConvertedYaml_ReadsBackThroughTLiosOwnAdapter(string json)
    {
        var yaml = _converter.Convert("json", json, "yaml", ConversionSettings.Empty);

        var source = JToken.Parse(json);
        var yamlContext = YamlExecutionContext.CreateDefault();
        var converted = yamlContext.NodeAdapter.Parse(yaml);

        AssertSameShape(
            source, JsonExecutionContext.CreateDefault().NodeAdapter,
            converted, yamlContext.NodeAdapter,
            "$", yaml);
    }

    [Test]
    public void ArrayItemName_IsWhatTLioCallsAnArrayItem()
    {
        var xml = _converter.Convert("json", """{"order":{"lines":[{"sku":"A1"}]}}""", "xml", ConversionSettings.Empty);

        Assert.That(xml, Does.Contain("<lines><item>"),
            "an array of one is only recognisable as an array when its item carries the canonical name");
    }

    [Test]
    public void RepeatedHandling_IsAvailableForLegacySchemas_AndSaysSoByFailingThisShape()
    {
        var settings = new ConversionSettings { ArrayHandling = ArrayHandling.Repeated };
        var xml = _converter.Convert("json", """{"order":{"lines":[{"sku":"A1"},{"sku":"B2"}]}}""", "xml", settings);

        Assert.That(xml, Is.EqualTo("<order><lines><sku>A1</sku></lines><lines><sku>B2</sku></lines></order>"));

        // Documented consequence: in this shape the parent reads as an array, which is exactly
        // why it is not the default.
        var adapter = new XmlNodeAdapter();
        Assert.That(adapter.IsArray(adapter.Parse(xml)), Is.True,
            "the repeated shape makes the parent element look like the array");
    }

    // ── shape walker ─────────────────────────────────────────────────────────

    private static bool IsEmptyContainer<TNode>(TNode node, INodeAdapter<TNode> adapter) =>
        (adapter.IsArray(node) && adapter.GetArrayLength(node) == 0)
        || (adapter.IsObject(node) && !adapter.IsNull(node) && !adapter.GetPropertyNames(node).Any());

    private static void AssertSameShape<TSource, TTarget>(
        TSource source, INodeAdapter<TSource> sourceAdapter,
        TTarget target, INodeAdapter<TTarget> targetAdapter,
        string path, string converted)
    {
        if (IsEmptyContainer(source, sourceAdapter) && targetAdapter.IsNull(target))
        {
            // The one divergence document-shape.md signs off on: <k/> is equally null, "", {} and
            // [], so an empty container does not survive XML with its kind intact. Anything that
            // is not empty still has to.
            return;
        }

        if (sourceAdapter.IsArray(source))
        {
            Assert.That(targetAdapter.IsArray(target), Is.True,
                $"at {path}: the source is an array; the converted document is not.\n{converted}");
            Assert.That(targetAdapter.GetArrayLength(target), Is.EqualTo(sourceAdapter.GetArrayLength(source)),
                $"at {path}: array length differs.\n{converted}");

            for (var i = 0; i < sourceAdapter.GetArrayLength(source); i++)
            {
                AssertSameShape(
                    sourceAdapter.GetArrayElement(source, i), sourceAdapter,
                    targetAdapter.GetArrayElement(target, i), targetAdapter,
                    $"{path}[{i}]", converted);
            }
            return;
        }

        if (sourceAdapter.IsNull(source))
        {
            // Null is the one place the formats genuinely differ in what they can spell; all
            // three agree it is not a container with something in it.
            Assert.That(targetAdapter.IsNull(target), Is.True,
                $"at {path}: the source is null; the converted document is not.\n{converted}");
            return;
        }

        if (sourceAdapter.IsObject(source))
        {
            Assert.That(targetAdapter.IsObject(target), Is.True,
                $"at {path}: the source is an object; the converted document is not.\n{converted}");

            var expectedNames = sourceAdapter.GetPropertyNames(source).ToList();
            Assert.That(targetAdapter.GetPropertyNames(target), Is.EquivalentTo(expectedNames),
                $"at {path}: property names differ.\n{converted}");

            foreach (var name in expectedNames)
            {
                AssertSameShape(
                    sourceAdapter.GetProperty(source, name)!, sourceAdapter,
                    targetAdapter.GetProperty(target, name)!, targetAdapter,
                    $"{path}.{name}", converted);
            }
            return;
        }

        // Scalars: XML and YAML carry no declared type, so the text is what has to match.
        Assert.That(targetAdapter.TryGetString(target), Is.EqualTo(sourceAdapter.TryGetString(source)),
            $"at {path}: scalar value differs.\n{converted}");
    }
}
