using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

/// <summary>
/// Format conversion through <c>tlio_execute</c>: <c>convert</c> as a boundary in the middle of a
/// script, and <c>convertValue</c> for one value in place.
/// </summary>
[TestFixture]
public sealed class ExecutionToolsConvertTests
{
    private ExecutionTools _sut = null!;
    private RateLimiterService _rateLimiter = null!;

    [SetUp]
    public void SetUp()
    {
        var config = Options.Create(new McpConfiguration
        {
            Observability = new ObservabilityConfig { Enabled = true },
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        _rateLimiter = new RateLimiterService(config);
        _sut = new ExecutionTools(_rateLimiter, config);
    }

    [TearDown]
    public void TearDown() => _rateLimiter.Dispose();

    private ExecuteResult Run(string document, string format, string script) =>
        (ExecuteResult)_sut.Execute(document, format, script);

    // ── convert: the boundary ────────────────────────────────────────────────

    [Test]
    public void Convert_ChangesTheOutputFormat_AndSaysWhichOne()
    {
        var result = Run("""{"order":{"id":"7"}}""", "json", """[{"command":"convert","to":"xml"}]""");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Format, Is.EqualTo("xml"), "the result reports the format the run ended in");
            Assert.That(result.Output, Is.EqualTo("<order><id>7</id></order>"));
        });
    }

    [Test]
    public void Convert_RunsCommandsOnBothSides()
    {
        const string script = """
            [
              { "command": "add",     "path": "$.order.status", "value": "new" },
              { "command": "convert", "to": "yaml" },
              { "command": "add",     "path": "order.source",   "value": "portal" },
              { "command": "convert", "to": "xml" },
              { "command": "rename",  "path": "/order",         "name": "opdracht" }
            ]
            """;

        var result = Run("""{"order":{"id":"7"}}""", "json", script);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));

        var xml = XElement.Parse(result.Output);
        Assert.Multiple(() =>
        {
            Assert.That(xml.Name.LocalName, Is.EqualTo("opdracht"));
            Assert.That(xml.Element("status")?.Value, Is.EqualTo("new"));
            Assert.That(xml.Element("source")?.Value, Is.EqualTo("portal"));
        });
    }

    [Test]
    public void Convert_ToAnUnregisteredFormat_ReportsTheReason()
    {
        var result = Run("""{"a":"1"}""", "json", """[{"command":"convert","to":"toml"}]""");

        Assert.That(result.Success, Is.False);
        Assert.That(string.Join(" ", result.Errors), Does.Contain("toml"));
    }

    [Test]
    public void Convert_WrittenInTheYamlNotation_CrossesTheBoundary()
    {
        // convert is a command like any other in all three notations. The boundary split reads
        // the script in whichever one it is written in, so this works exactly as the JSON
        // spelling does — it used to be refused, because the split only ever parsed JSON.
        const string yamlScript = """
            - command: add
              path: $.order.status
              value: new
            - command: convert
              to: xml
            - command: rename
              path: /order
              name: opdracht
            """;

        var result = Run("""{"order":{"id":"7"}}""", "json", yamlScript);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));

        var xml = XElement.Parse(result.Output);
        Assert.Multiple(() =>
        {
            Assert.That(result.Format, Is.EqualTo("xml"), "the result reports the format the run ended in");
            Assert.That(xml.Name.LocalName, Is.EqualTo("opdracht"), "a command after the boundary ran");
            Assert.That(xml.Element("status")?.Value, Is.EqualTo("new"), "a command before it ran too");
        });
    }

    [Test]
    public void Convert_WrittenInTheXmlNotation_CrossesTheBoundary()
    {
        // The same script in the XML notation, driving a JSON document: the notation a script is
        // written in is independent of the format of the data, boundaries included. Note the
        // paths — JSONPath before the boundary, XPath after — because those follow the document.
        const string xmlScript = """
            <script>
              <add path="$.order.status">new</add>
              <convert to="xml"/>
              <rename path="/order" name="opdracht"/>
            </script>
            """;

        var result = Run("""{"order":{"id":"7"}}""", "json", xmlScript);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));

        var xml = XElement.Parse(result.Output);
        Assert.Multiple(() =>
        {
            Assert.That(result.Format, Is.EqualTo("xml"));
            Assert.That(xml.Name.LocalName, Is.EqualTo("opdracht"));
            Assert.That(xml.Element("status")?.Value, Is.EqualTo("new"));
        });
    }

    [Test]
    public void Convert_SettingsAreReadFromEveryNotation()
    {
        // inferTypes is a bool in JSON, an element in XML and a scalar in YAML. One reader takes
        // all three, so a setting cannot work in one notation and be silently ignored in another.
        const string xmlScript = """
            <script>
              <convert to="json"><settings><inferTypes>true</inferTypes></settings></convert>
            </script>
            """;

        var result = Run("<order><id>7</id></order>", "xml", xmlScript);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.Output, Does.Contain("\"id\":7"), "inferTypes made it a number, not \"7\"");
    }

    // ── convertValue: one value in place ─────────────────────────────────────

    [Test]
    public void ConvertValue_TurnsAnEmbeddedPayloadIntoStructure()
    {
        const string document = """{"id":"1","payload":"<order><sku>A1</sku></order>"}""";
        const string script = """[{"command":"convertValue","path":"$.payload","from":"xml","to":"json"}]""";

        var result = Run(document, "json", script);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.Format, Is.EqualTo("json"), "the document itself never changed format");

        var output = JToken.Parse(result.Output);
        Assert.That(output["payload"]!["order"]!["sku"]!.Value<string>(), Is.EqualTo("A1"));
    }

    [Test]
    public void ConvertValue_TurnsASubtreeIntoText()
    {
        const string script = """[{"command":"convertValue","path":"$.order","to":"xml"}]""";

        var result = Run("""{"id":"1","order":{"sku":"A1"}}""", "json", script);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
        Assert.That(JToken.Parse(result.Output)["order"]!.Value<string>(),
            Is.EqualTo("<order><sku>A1</sku></order>"));
    }

    [Test]
    public void ConvertValue_IsAvailableOnTheXmlEngineToo()
    {
        const string document = """<envelope><payload>{"sku":"A1","qty":"2"}</payload></envelope>""";
        const string script = """[{"command":"convertValue","path":"/envelope/payload","from":"json","to":"xml"}]""";

        var result = Run(document, "xml", script);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
        Assert.Multiple(() =>
        {
            // XML's Replace keeps the element and swaps its content, so payload stays payload.
            Assert.That(result.Output, Does.Contain("<sku>A1</sku>"));
            Assert.That(result.Output, Does.Contain("<qty>2</qty>"));
        });
    }
}
