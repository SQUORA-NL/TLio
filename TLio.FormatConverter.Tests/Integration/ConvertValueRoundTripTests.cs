using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using TLio.Json;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.FormatConverter.Tests.Integration;

/// <summary>
/// <c>convertValue</c> converts the <i>value</i> at a path, and nothing else. The node's own
/// property name is not part of the value, so it must not leak into the result — and a value
/// that goes out to text and is read back must come back as it was.
/// </summary>
/// <remarks>
/// XML is the one format that cannot hold a bare value: a document needs exactly one root
/// element. So going <i>to</i> XML the value is wrapped in a root element — <c>root</c> unless
/// <c>settings.rootName</c> says otherwise — and reading <i>from</i> XML that root is unwrapped
/// again. The root is the container of the value, never part of it.
/// <para>
/// Before this was fixed the command serialised <c>$.order</c> as <c>&lt;order&gt;…&lt;/order&gt;</c>
/// (the property name became the root) and read <c>&lt;order&gt;…&lt;/order&gt;</c> back as
/// <c>{"order":{…}}</c> (the root became a property), so a value picked up one extra level of
/// nesting every time it made the round trip.
/// </para>
/// </remarks>
[TestFixture]
public sealed class ConvertValueRoundTripTests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());

        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.RegisterFormatConversion<JToken>(converter, "json");
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken Run(JToken document, string script)
    {
        var result = _engine.Execute(script, document, JsonExecutionContext.CreateDefault());
        Assert.That(result.Success, Is.True, "the script should run without failing");
        return result.Data;
    }

    // ── valueOnly is the default; false is the way back to the 1.5.0 output ──

    [Test]
    public void ValueOnlyIsTheDefault()
    {
        var data = Run(JToken.Parse("""{"input":{"field":"1"}}"""),
            """[{"command":"convertValue","path":"$.input","to":"xml"}]""");

        Assert.That(data["input"]!.Value<string>(), Is.EqualTo("<root><field>1</field></root>"));
    }

    [Test]
    public void ValueOnlyFalse_KeepsTheOutputOf150()
    {
        var data = Run(JToken.Parse("""{"input":{"field":"1"}}"""),
            """[{"command":"convertValue","path":"$.input","to":"xml","settings":{"valueOnly":false}}]""");

        Assert.That(data["input"]!.Value<string>(), Is.EqualTo("<input><field>1</field></input>"),
            "with valueOnly off the property name is the root, as it was in 1.5.0");
    }

    [Test]
    public void InAnXmlDocument_TheElementKeepsItsName_AndNothingIsWarnedAbout()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        var options = ParseOptions<System.Xml.Linq.XElement>.CreateDefault();
        options.CommandsProvider.RegisterFormatConversion<System.Xml.Linq.XElement>(converter, "xml");
        var engine = new ScriptEngine<System.Xml.Linq.XElement>(options.CommandsProvider, options.FunctionsProvider);
        var context = TLio.Xml.XmlExecutionContext.CreateWithSlashPaths();
        var doc = new TLio.Xml.XmlNodeAdapter().Parse("<root><input><a>1</a></input></root>");

        var result = engine.Execute(
            """[{"command":"convertValue","path":"/root/input","to":"json","settings":{"valueOnly":true}}]""", doc, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("valueOnly")), Is.False,
            "an XML document has no bare value to separate, so there is nothing to warn about");
    }

    // ── to text: only the value ──────────────────────────────────────────────

    [Test]
    public void ToXml_PropertyNameIsNotPartOfTheResult()
    {
        var data = Run(JToken.Parse("""{"input":{"a":"1","b":"2"}}"""),
            """[{"command":"convertValue","path":"$.input","to":"xml","settings":{"valueOnly":true}}]""");

        Assert.That(data["input"]!.Value<string>(), Is.EqualTo("<root><a>1</a><b>2</b></root>"));
    }

    [Test]
    public void ToXml_RootNameComesFromTheSetting()
    {
        var data = Run(JToken.Parse("""{"input":{"a":"1"}}"""),
            """[{"command":"convertValue","path":"$.input","to":"xml","settings":{"valueOnly":true,"rootName":"payload"}}]""");

        Assert.That(data["input"]!.Value<string>(), Is.EqualTo("<payload><a>1</a></payload>"));
    }

    [Test]
    public void ToYaml_HasNoRootToAdd_SoTheResultIsTheValueAlone()
    {
        var data = Run(JToken.Parse("""{"order":{"sku":"A1"}}"""),
            """[{"command":"convertValue","path":"$.order","to":"yaml","settings":{"valueOnly":true}}]""");

        Assert.That(data["order"]!.Value<string>()!.Trim(), Is.EqualTo("sku: A1"));
    }

    // ── from text: the root is not a property ────────────────────────────────

    [Test]
    public void FromXml_TheRootElementIsUnwrapped()
    {
        var data = Run(JToken.Parse("""{"payload":"<order><sku>A1</sku></order>"}"""),
            """[{"command":"convertValue","path":"$.payload","from":"xml","to":"json","settings":{"valueOnly":true}}]""");

        Assert.That(data["payload"]!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("""{"sku":"A1"}"""));
    }

    // ── the round trip ───────────────────────────────────────────────────────

    private static IEnumerable<TestCaseData> Values()
    {
        yield return new TestCaseData("""{"sku":"A1","qty":"2"}""").SetName("flat object");
        yield return new TestCaseData("""{"field":"1"}""").SetName("single-key object");
        yield return new TestCaseData("""{"order":{"sku":"A1","customer":{"name":"Ada"}}}""").SetName("nested object");
        yield return new TestCaseData("""{"a":"1","b":"2","c":"3"}""").SetName("several keys, no single root");
        yield return new TestCaseData("""["a","b","c"]""").SetName("array of strings");
        yield return new TestCaseData("""[{"sku":"A1"},{"sku":"B2"}]""").SetName("array of objects");
        yield return new TestCaseData("\"hello\"").SetName("a string");
        yield return new TestCaseData("""{"lines":[{"sku":"A1","qty":"2"},{"sku":"B2","qty":"1"}]}""").SetName("object holding an array");
    }

    [TestCaseSource(nameof(Values))]
    public void ThroughXmlAndBack_TheValueIsUnchanged(string value)
    {
        var original = JToken.Parse(value);
        var document = new JObject { ["v"] = original.DeepClone() };

        var asText = Run(document, """[{"command":"convertValue","path":"$.v","to":"xml","settings":{"valueOnly":true}}]""");
        Assert.That(asText["v"]!.Type, Is.EqualTo(JTokenType.String), "XML in a JSON document is a string");

        var back = Run(asText, """[{"command":"convertValue","path":"$.v","from":"xml","to":"json","settings":{"valueOnly":true}}]""");

        Assert.That(JToken.DeepEquals(back["v"], original), Is.True,
            $"expected {original.ToString(Newtonsoft.Json.Formatting.None)} " +
            $"but got {back["v"]!.ToString(Newtonsoft.Json.Formatting.None)}");
    }

    [TestCaseSource(nameof(Values))]
    public void ThroughYamlAndBack_TheValueIsUnchanged(string value)
    {
        var original = JToken.Parse(value);
        var document = new JObject { ["v"] = original.DeepClone() };

        var asText = Run(document, """[{"command":"convertValue","path":"$.v","to":"yaml","settings":{"valueOnly":true}}]""");
        var back = Run(asText, """[{"command":"convertValue","path":"$.v","from":"yaml","to":"json","settings":{"valueOnly":true}}]""");

        Assert.That(JToken.DeepEquals(back["v"], original), Is.True,
            $"expected {original.ToString(Newtonsoft.Json.Formatting.None)} " +
            $"but got {back["v"]!.ToString(Newtonsoft.Json.Formatting.None)}");
    }

    [Test]
    public void TheRoundTripDoesNotAddANestingLevel_EvenRepeated()
    {
        var original = JToken.Parse("""{"sku":"A1"}""");
        var data = new JObject { ["v"] = original.DeepClone() };

        for (var i = 0; i < 3; i++)
        {
            data = (JObject)Run(data, """[{"command":"convertValue","path":"$.v","to":"xml","settings":{"valueOnly":true}}]""");
            data = (JObject)Run(data, """[{"command":"convertValue","path":"$.v","from":"xml","to":"json","settings":{"valueOnly":true}}]""");
        }

        Assert.That(data["v"]!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("""{"sku":"A1"}"""));
    }
}
