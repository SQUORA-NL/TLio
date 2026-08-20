using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Parity.Tests;

/// <summary>
/// The notation a script is written in is independent of the format of the document it
/// transforms. These tests hold that line from both directions:
///
/// - the same script written in JSON, XML and YAML produces the same result, and
/// - each notation drives every document format, not only the one it shares a syntax with.
///
/// The second half is the part that used to be broken. Both non-JSON parsers built a
/// structured value by handing their own text to the node adapter, which is the adapter for
/// the *data* — so a YAML script against an XML document handed the XML adapter YAML, threw,
/// and the swallowed failure left the command writing nothing at all.
///
/// Paths are the one thing that does not carry across: each case is written per document
/// format, because a document's path language is its own.
/// </summary>
[TestFixture]
public class ScriptNotationTests
{
    // The same four commands — a scalar value, a structured value with a function nested inside
    // it, a boolean property, and a nested script — spelled three ways against JSON paths.

    private const string JsonPathScriptInJson = """
        [
          { "command": "put", "path": "$.name", "value": "Alice" },
          { "command": "put", "path": "$.point", "value": { "x": 1, "echo": "=fetch($.email)" } },
          { "command": "copy", "fromPath": "$.email", "toPath": "$.contact", "destinationAsArray": true },
          { "command": "ifElse", "condition": "=equals($.n, 5)",
            "ifScript":   [ { "command": "put", "path": "$.r", "value": "yes" } ],
            "elseScript": [ { "command": "put", "path": "$.r", "value": "no"  } ] }
        ]
        """;

    private const string JsonPathScriptInXml = """
        <script>
          <put path="$.name">Alice</put>
          <put path="$.point"><value><x>1</x><echo>=fetch($.email)</echo></value></put>
          <copy fromPath="$.email" toPath="$.contact" destinationAsArray="true"/>
          <ifElse condition="=equals($.n, 5)">
            <ifScript><put path="$.r">yes</put></ifScript>
            <elseScript><put path="$.r">no</put></elseScript>
          </ifElse>
        </script>
        """;

    private const string JsonPathScriptInYaml = """
        - command: put
          path: $.name
          value: Alice
        - command: put
          path: $.point
          value:
            x: 1
            echo: "=fetch($.email)"
        - command: copy
          fromPath: $.email
          toPath: $.contact
          destinationAsArray: true
        - command: ifElse
          condition: "=equals($.n, 5)"
          ifScript:
            - command: put
              path: $.r
              value: yes
          elseScript:
            - command: put
              path: $.r
              value: no
        """;

    private const string JsonDocument = """{"n":5,"email":"a@b.c"}""";

    [Test]
    public void ThreeNotations_SameScript_AgreeOnJsonDocument()
    {
        var fromJson = RunJson(JsonPathScriptInJson);
        var fromXml  = RunJson(JsonPathScriptInXml);
        var fromYaml = RunJson(JsonPathScriptInYaml);

        Assert.Multiple(() =>
        {
            Assert.That(fromXml, Is.EqualTo(fromJson), "XML notation diverged from JSON");
            Assert.That(fromYaml, Is.EqualTo(fromJson), "YAML notation diverged from JSON");
        });
    }

    /// <summary>
    /// A number written in a structured value stays a number in every notation. XML and YAML
    /// route the value through the JSON converter for exactly this: the adapter's typed
    /// creation methods, rather than the text of the value.
    /// </summary>
    [Test]
    public void ThreeNotations_NumberInStructuredValue_StaysANumber()
    {
        foreach (var (notation, script) in AllNotations())
            Assert.That(RunJson(script), Does.Contain("\"x\":1"),
                $"{notation} notation did not keep the number typed");
    }

    /// <summary>
    /// A function nested inside a structured value is evaluated when the command runs, in every
    /// notation. XML and YAML used to write the literal text of the expression instead.
    /// </summary>
    [Test]
    public void ThreeNotations_FunctionInsideStructuredValue_IsEvaluated()
    {
        foreach (var (notation, script) in AllNotations())
        {
            var result = RunJson(script);
            Assert.That(result, Does.Contain("\"echo\":\"a@b.c\""),
                $"{notation} notation did not expand the nested function");
            Assert.That(result, Does.Not.Contain("=fetch"),
                $"{notation} notation wrote the expression instead of its value");
        }
    }

    private static IEnumerable<(ScriptFormat, string)> AllNotations()
    {
        yield return (ScriptFormat.Json, JsonPathScriptInJson);
        yield return (ScriptFormat.Xml, JsonPathScriptInXml);
        yield return (ScriptFormat.Yaml, JsonPathScriptInYaml);
    }

    // ── Every notation against every document format ─────────────────────────

    private const string XmlDocument = "<order><n>5</n><email>a@b.c</email></order>";
    private const string YamlDocument = "n: 5\nemail: a@b.c\n";

    private const string XPathScriptInXml = """
        <script>
          <put path="/order/point"><value><x>1</x><echo>=fetch(/order/email)</echo></value></put>
        </script>
        """;

    private const string XPathScriptInYaml = """
        - command: put
          path: /order/point
          value:
            x: 1
            echo: "=fetch(/order/email)"
        """;

    private const string XPathScriptInJson = """
        [ { "command": "put", "path": "/order/point",
            "value": { "x": 1, "echo": "=fetch(/order/email)" } } ]
        """;

    [Test]
    public void EveryNotation_DrivesAnXmlDocument()
    {
        foreach (var (notation, script) in
                 new[] { (ScriptFormat.Xml, XPathScriptInXml),
                         (ScriptFormat.Yaml, XPathScriptInYaml),
                         (ScriptFormat.Json, XPathScriptInJson) })
        {
            var result = RunXml(script);
            Assert.That(result, Does.Contain("<echo>a@b.c</echo>"),
                $"{notation} notation did not write into the XML document");
            Assert.That(result, Does.Contain("<x>1</x>"),
                $"{notation} notation lost the structured value on an XML document");
        }
    }

    [Test]
    public void EveryNotation_DrivesAYamlDocument()
    {
        foreach (var (notation, script) in AllNotations())
        {
            var result = RunYaml(script);
            Assert.That(result, Does.Contain("a@b.c"),
                $"{notation} notation did not write into the YAML document");
            Assert.That(result, Does.Contain("yes"),
                $"{notation} notation did not run the nested script on a YAML document");
        }
    }

    // ── Detection and registration ───────────────────────────────────────────

    [Test]
    public void Engine_DetectsTheNotationFromTheText()
    {
        var engine = CreateEngine<JToken>();
        var context = JsonExecutionContext.CreateDefault();

        // No format argument anywhere — each script is routed by its own shape.
        foreach (var (notation, script) in AllNotations())
        {
            var result = engine.Execute(script, JToken.Parse(JsonDocument), context);
            Assert.That(result.Success, Is.True, $"{notation} notation was not detected");
        }
    }

    /// <summary>
    /// A notation nobody registered fails visibly. It used to reach the JSON parser, which
    /// answered an XML script with an empty script and no complaint — the run "succeeded"
    /// having done nothing.
    /// </summary>
    [Test]
    public void Engine_UnregisteredNotation_SaysSo()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var bare = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        var script = bare.Parse(JsonPathScriptInXml, JsonExecutionContext.CreateDefault().NodeAdapter);

        Assert.That(script, Is.Empty);
        Assert.That(script.ParseWarnings, Has.Some.Contains("UseXmlScripts"));
    }

    [Test]
    public void Engine_MalformedScript_CarriesTheReason()
    {
        var engine = CreateEngine<JToken>();
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;

        foreach (var (notation, text) in
                 new[] { (ScriptFormat.Json, "[ {"),
                         (ScriptFormat.Xml, "<script><set/>"),
                         (ScriptFormat.Yaml, "- command: set\n\tbad: indent") })
        {
            var script = engine.Parse(text, notation, adapter);
            Assert.That(script, Is.Empty, $"{notation} notation parsed malformed text");
            Assert.That(script.ParseWarnings, Is.Not.Empty,
                $"{notation} notation gave no reason for producing nothing");
        }
    }

    /// <summary>An empty script is a legal script, and says nothing about being unreadable.</summary>
    [Test]
    public void Engine_EmptyScript_ParsesWithoutComplaint()
    {
        var engine = CreateEngine<JToken>();
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;

        foreach (var (notation, text) in
                 new[] { (ScriptFormat.Json, "[]"), (ScriptFormat.Xml, "<script/>") })
        {
            var script = engine.Parse(text, notation, adapter);
            Assert.That(script, Is.Empty);
            Assert.That(script.ParseWarnings, Is.Empty,
                $"{notation} notation warned about a deliberately empty script");
        }
    }

    // ── Runners ──────────────────────────────────────────────────────────────

    private static ScriptEngine<TNode> CreateEngine<TNode>()
    {
        var options = ParseOptions<TNode>.CreateDefault();
        return new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider)
            .UseXmlScripts()
            .UseYamlScripts();
    }

    private static string RunJson(string script)
    {
        var context = JsonExecutionContext.CreateDefault();
        var result = CreateEngine<JToken>().Execute(script, JToken.Parse(JsonDocument), context);
        return context.NodeAdapter.Serialize(result.Data);
    }

    private static string RunXml(string script)
    {
        var context = XmlExecutionContext.CreateWithNativeXPath();
        var doc = context.NodeAdapter.Parse(XmlDocument);
        var result = CreateEngine<XElement>().Execute(script, doc, context);
        return context.NodeAdapter.Serialize(result.Data);
    }

    private static string RunYaml(string script)
    {
        var context = YamlExecutionContext.CreateDefault();
        var doc = context.NodeAdapter.Parse(YamlDocument);
        var result = CreateEngine<YamlNode>().Execute(script, doc, context);
        return context.NodeAdapter.Serialize(result.Data);
    }
}
