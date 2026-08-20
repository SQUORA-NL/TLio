using FormatConverter.Json;
using FormatConverter.TLio;
using FormatConverter.Xml;
using FormatConverter.Yaml;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Json;
using Converter = global::FormatConverter.Core.FormatConverter;

namespace FormatConverter.Tests.Integration;

/// <summary>
/// <c>convertValue</c> — converting one value in place, without changing the format of the
/// document around it.
/// </summary>
/// <remarks>
/// This runs on the engine like any other command, with no runner involved: the document's node
/// type never changes, only the value at the path does.
/// </remarks>
[TestFixture]
public sealed class ConvertValueTests
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

    private (JToken Data, IExecutionContext<JToken> Context, bool Success) Run(string json, string script)
    {
        var context = JsonExecutionContext.CreateDefault();
        var data = JToken.Parse(json);
        var result = _engine.Execute(script, data, context);
        return (result.Data, context, result.Success);
    }

    // ── text into structure ──────────────────────────────────────────────────

    [Test]
    public void EmbeddedXmlPayload_BecomesStructure()
    {
        // The everyday case: a JSON envelope carrying XML as a string.
        const string json = """{"id":"1","payload":"<order><sku>A1</sku><qty>2</qty></order>"}""";
        const string script = """[{"command":"convertValue","path":"$.payload","from":"xml","to":"json"}]""";

        var (data, _, success) = Run(json, script);

        Assert.That(success, Is.True);
        Assert.That(data["payload"]!["order"]!["sku"]!.Value<string>(), Is.EqualTo("A1"));
        Assert.That(data["id"]!.Value<string>(), Is.EqualTo("1"), "the rest of the document is untouched");
    }

    [Test]
    public void EmbeddedYamlPayload_BecomesStructure()
    {
        const string json = """{"payload":"order:\n  sku: A1\n"}""";
        const string script = """[{"command":"convertValue","path":"$.payload","from":"yaml","to":"json"}]""";

        var (data, _, success) = Run(json, script);

        Assert.That(success, Is.True);
        Assert.That(data["payload"]!["order"]!["sku"]!.Value<string>(), Is.EqualTo("A1"));
    }

    // ── structure into text ──────────────────────────────────────────────────

    [Test]
    public void Subtree_BecomesTextInAnotherFormat()
    {
        // No 'from': the node is structure in the document's own format.
        const string json = """{"id":"1","order":{"sku":"A1"}}""";
        const string script = """[{"command":"convertValue","path":"$.order","to":"xml"}]""";

        var (data, _, success) = Run(json, script);

        Assert.That(success, Is.True);
        Assert.That(data["order"]!.Type, Is.EqualTo(JTokenType.String));
        Assert.That(data["order"]!.Value<string>(), Is.EqualTo("<order><sku>A1</sku></order>"));
    }

    [Test]
    public void ConvertingToTheDocumentsOwnFormat_GivesStructureNotText()
    {
        const string json = """{"payload":"<order><sku>A1</sku></order>"}""";

        var (data, _, _) = Run(json, """[{"command":"convertValue","path":"$.payload","from":"xml","to":"json"}]""");

        Assert.That(data["payload"]!.Type, Is.EqualTo(JTokenType.Object));
    }

    [Test]
    public void ThereAndBackAgain()
    {
        const string json = """{"order":{"sku":"A1","qty":"2"}}""";
        const string script = """
            [
              {"command":"convertValue","path":"$.order","to":"xml"},
              {"command":"convertValue","path":"$.order","from":"xml","to":"json"}
            ]
            """;

        var (data, _, success) = Run(json, script);

        Assert.That(success, Is.True);
        Assert.That(data["order"]!["order"]!["sku"]!.Value<string>(), Is.EqualTo("A1"),
            "the serialised subtree carries its own element name, which re-parsing restores");
    }

    // ── several at once ──────────────────────────────────────────────────────

    [Test]
    public void EveryMatchedNodeIsConverted()
    {
        const string json = """{"messages":[{"body":"<m><a>1</a></m>"},{"body":"<m><a>2</a></m>"}]}""";
        const string script = """[{"command":"convertValue","path":"$.messages[*].body","from":"xml","to":"json"}]""";

        var (data, _, success) = Run(json, script);

        Assert.That(success, Is.True);
        Assert.That(data["messages"]!.Select(m => m["body"]!["m"]!["a"]!.Value<string>()),
            Is.EqualTo(new[] { "1", "2" }));
    }

    // ── when it cannot ───────────────────────────────────────────────────────

    [Test]
    public void MalformedPayload_FailsWithTheParserReason()
    {
        const string json = """{"payload":"<order><unclosed>"}""";
        const string script = """[{"command":"convertValue","path":"$.payload","from":"xml","to":"json"}]""";

        var (data, context, success) = Run(json, script);

        Assert.That(success, Is.False);
        Assert.That(data["payload"]!.Value<string>(), Is.EqualTo("<order><unclosed>"), "the value is left alone");
        Assert.That(context.GetLogEntries().HasErrors, Is.True);
    }

    [Test]
    public void UnregisteredFormat_FailsRatherThanThrowing()
    {
        const string script = """[{"command":"convertValue","path":"$.payload","from":"xml","to":"toml"}]""";

        var (_, context, success) = Run("""{"payload":"<a>1</a>"}""", script);

        Assert.That(success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("toml")), Is.True);
    }

    [Test]
    public void PathThatMatchesNothing_WarnsAndChangesNothing()
    {
        const string script = """[{"command":"convertValue","path":"$.missing","from":"xml","to":"json"}]""";

        var (data, context, _) = Run("""{"payload":"<a>1</a>"}""", script);

        Assert.That(data["payload"]!.Value<string>(), Is.EqualTo("<a>1</a>"));
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("no nodes matched")), Is.True);
    }

    [Test]
    public void MissingTo_IsAValidationFailure()
    {
        var (_, context, success) = Run("""{"payload":"<a>1</a>"}""",
            """[{"command":"convertValue","path":"$.payload","from":"xml"}]""");

        Assert.That(success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("'to'")), Is.True);
    }

    // ── the boundary command, run in the wrong place ─────────────────────────

    [Test]
    public void BoundaryConvert_RunInline_FailsAndSaysWhy()
    {
        var (data, context, success) = Run("""{"a":"1"}""", """[{"command":"convert","to":"xml"}]""");

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False, "reporting success would claim a conversion that did not happen");
            Assert.That(data["a"]!.Value<string>(), Is.EqualTo("1"));
            Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("MultiFormatScriptRunner")), Is.True,
                "an unknown-command warning would not have told anyone what to do instead");
        });
    }
}
