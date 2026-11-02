using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.FormatConverter.Tests.Integration;

/// <summary>
/// The whole-document <c>convert</c> command, there and back: a document sent to another format
/// and returned to its own comes back as it started. Six directions, one script each — the
/// scripts in these tests are the ones to copy.
/// </summary>
[TestFixture]
public sealed class RoundTripScriptTests
{
    private MultiFormatScriptRunner _runner = null!;

    // Strings throughout: XML has no types, so a number comes back as text unless inferTypes is set
    // (see NumbersAndBooleans_NeedInferTypesToSurviveXml).
    private const string Json =
        """{"order":{"id":"7","customer":{"name":"Ada"},"lines":[{"sku":"A1","qty":"2"},{"sku":"B2","qty":"1"}]}}""";

    [SetUp]
    public void SetUp()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        _runner = new MultiFormatScriptRunner(converter);
    }

    private string Start(string format) =>
        format == "json" ? Json
        : _runner.Execute("json", Json, $$"""[{"command":"convert","to":"{{format}}"}]""");

    private static string There(string via, string back) =>
        $$"""[{"command":"convert","to":"{{via}}"},{"command":"convert","to":"{{back}}"}]""";

    private static void AssertSame(string format, string expected, string actual)
    {
        switch (format)
        {
            case "json":
                Assert.That(JToken.DeepEquals(JToken.Parse(actual), JToken.Parse(expected)), Is.True, actual);
                break;
            case "xml":
                Assert.That(XNode.DeepEquals(XElement.Parse(actual), XElement.Parse(expected)), Is.True, actual);
                break;
            default:
                YamlAssert.Equivalent(actual, expected, $"{format} round trip");
                break;
        }
    }

    [TestCase("json", "xml")]
    [TestCase("xml", "json")]
    [TestCase("yaml", "json")]
    [TestCase("json", "yaml")]
    [TestCase("yaml", "xml")]
    [TestCase("xml", "yaml")]
    public void ThereAndBack_ReturnsTheStartingDocument(string start, string via)
    {
        var input = Start(start);
        var script = There(via, start);

        var output = _runner.Execute(start, input, script);

        var middle = _runner.Execute(start, input, $$"""[{"command":"convert","to":"{{via}}"}]""");
        TestContext.Out.WriteLine(
            $"--- {start} -> {via} -> {start}\nscript: {script}\nSTART ({start}):\n{input}\nMIDDLE ({via}):\n{middle}\nEND ({start}):\n{output}\n");
        AssertSame(start, input, output);
    }

    [Test]
    public void NumbersAndBooleans_NeedInferTypesToSurviveXml()
    {
        const string input = """{"order":{"id":7,"paid":true,"total":12.5}}""";
        const string script =
            """[{"command":"convert","to":"xml"},{"command":"convert","to":"json","settings":{"inferTypes":true}}]""";

        var output = _runner.Execute("json", input, script);

        Assert.That(JToken.DeepEquals(JToken.Parse(output), JToken.Parse(input)), Is.True, output);
    }
}
