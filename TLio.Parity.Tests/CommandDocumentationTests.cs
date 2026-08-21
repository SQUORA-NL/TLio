using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;

namespace TLio.Parity.Tests;

/// <summary>
/// A command may carry a title and a description: free text saying what the step is for, so a
/// script reads as more than a list of paths. Both are optional, both are documentation, and
/// the engine never reads either one — a script does the same thing with them and without them.
///
/// Every notation accepts them, because a script that can be written in one notation has to be
/// writable in the others; these tests hold that, and hold the one place XML has to differ.
/// </summary>
[TestFixture]
public class CommandDocumentationTests
{
    private const string JsonDocument = """{"n":5,"email":"a@b.c"}""";

    private const string DocumentedJson = """
        [
          { "command": "put", "path": "$.name", "value": "Alice",
            "title": "Name the customer",
            "description": "Downstream systems key on it, so it is written before anything else." },
          { "command": "ifElse", "condition": "=equals($.n, 5)", "title": "Branch on n",
            "ifScript":   [ { "command": "put", "path": "$.r", "value": "yes", "title": "Matched" } ],
            "elseScript": [ { "command": "put", "path": "$.r", "value": "no" } ] }
        ]
        """;

    private const string DocumentedXml = """
        <script>
          <put path="$.name"
               title="Name the customer"
               description="Downstream systems key on it, so it is written before anything else.">Alice</put>
          <ifElse condition="=equals($.n, 5)" title="Branch on n">
            <ifScript><put path="$.r" title="Matched">yes</put></ifScript>
            <elseScript><put path="$.r">no</put></elseScript>
          </ifElse>
        </script>
        """;

    private const string DocumentedYaml = """
        - command: put
          path: $.name
          value: Alice
          title: Name the customer
          description: >-
            Downstream systems key on it, so it is written before anything else.
        - command: ifElse
          condition: "=equals($.n, 5)"
          title: Branch on n
          ifScript:
            - command: put
              path: $.r
              value: yes
              title: Matched
          elseScript:
            - command: put
              path: $.r
              value: no
        """;

    private static IEnumerable<(ScriptFormat, string)> AllNotations()
    {
        yield return (ScriptFormat.Json, DocumentedJson);
        yield return (ScriptFormat.Xml, DocumentedXml);
        yield return (ScriptFormat.Yaml, DocumentedYaml);
    }

    [Test]
    public void EveryNotation_ReadsTitleAndDescription()
    {
        foreach (var (notation, text) in AllNotations())
        {
            var script = Parse(text);

            Assert.That(script[0].Title, Is.EqualTo("Name the customer"),
                $"{notation} notation dropped the title");
            Assert.That(script[0].Description,
                Is.EqualTo("Downstream systems key on it, so it is written before anything else."),
                $"{notation} notation dropped the description");
        }
    }

    /// <summary>A description is optional on its own: a titled command need not carry one.</summary>
    [Test]
    public void EveryNotation_TitleWithoutDescription_LeavesDescriptionUnset()
    {
        foreach (var (notation, text) in AllNotations())
        {
            var branch = Parse(text)[1];
            Assert.That(branch.Title, Is.EqualTo("Branch on n"), $"{notation} notation dropped the title");
            Assert.That(branch.Description, Is.Null, $"{notation} notation invented a description");
        }
    }

    /// <summary>Both are absent, not empty, when the script says nothing.</summary>
    [Test]
    public void ACommandWithoutThem_HasNeither()
    {
        var command = Parse("""[ { "command": "put", "path": "$.name", "value": "Alice" } ]""")[0];

        Assert.Multiple(() =>
        {
            Assert.That(command.Title, Is.Null);
            Assert.That(command.Description, Is.Null);
        });
    }

    /// <summary>The commands inside a nested script are documented the same way.</summary>
    [Test]
    public void EveryNotation_DocumentsCommandsInsideANestedScript()
    {
        foreach (var (notation, text) in AllNotations())
        {
            var branch = Parse(text)[1];
            var nested = (TLioScript<JToken>)branch.GetType().GetProperty("IfScript")!.GetValue(branch)!;

            Assert.That(nested[0].Title, Is.EqualTo("Matched"),
                $"{notation} notation dropped the title of a nested command");
        }
    }

    /// <summary>
    /// The point of the feature is that it changes nothing. Each notation's documented script
    /// produces exactly what the same script produces with every title and description removed.
    /// </summary>
    [Test]
    public void EveryNotation_DocumentationDoesNotChangeTheResult()
    {
        var undocumented = Run("""
            [ { "command": "put", "path": "$.name", "value": "Alice" },
              { "command": "ifElse", "condition": "=equals($.n, 5)",
                "ifScript":   [ { "command": "put", "path": "$.r", "value": "yes" } ],
                "elseScript": [ { "command": "put", "path": "$.r", "value": "no" } ] } ]
            """);

        foreach (var (notation, text) in AllNotations())
            Assert.That(Run(text), Is.EqualTo(undocumented),
                $"{notation} notation let documentation reach the result");
    }

    /// <summary>
    /// In XML the two fields are attributes, and a <c>&lt;title&gt;</c> child element stays what
    /// it has always been: part of the value being written. A script that puts an object with a
    /// title in it must not lose that object to the documentation fields.
    /// </summary>
    [Test]
    public void Xml_TitleChildElement_IsStillValueContent()
    {
        var result = Run("""
            <script>
              <put path="$.article" title="Write the article">
                <title>Sale</title>
                <description>Half price</description>
              </put>
            </script>
            """);

        Assert.That(result, Does.Contain("\"title\":\"Sale\""));
        Assert.That(result, Does.Contain("\"description\":\"Half price\""));
    }

    [Test]
    public void Xml_TitleAttribute_DocumentsTheCommandItWasWrittenOn()
    {
        var command = Parse("""
            <script>
              <put path="$.article" title="Write the article"><title>Sale</title></put>
            </script>
            """)[0];

        Assert.That(command.Title, Is.EqualTo("Write the article"));
    }

    /// <summary>
    /// Serialising a script writes the documentation back out, so a script that is parsed and
    /// written again is still readable by the person who wrote it.
    /// </summary>
    [Test]
    public void SerializingAScript_KeepsTheDocumentation()
    {
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var json = TLioConvert.Serialize(Parse(DocumentedJson), adapter);

        Assert.That(json, Does.Contain("\"title\":\"Name the customer\""));
        Assert.That(json, Does.Contain("\"description\":\"Downstream systems key on it"));
        Assert.That(json, Does.Contain("\"title\":\"Matched\""), "a nested command lost its title");
    }

    /// <summary>An undocumented command serialises exactly as it did before the fields existed.</summary>
    [Test]
    public void SerializingAnUndocumentedCommand_WritesNeitherField()
    {
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var json = TLioConvert.Serialize(
            Parse("""[ { "command": "put", "path": "$.name", "value": "Alice" } ]"""), adapter);

        Assert.That(json, Does.Not.Contain("title"));
        Assert.That(json, Does.Not.Contain("description"));
    }

    // ── Runners ──────────────────────────────────────────────────────────────

    private static ScriptEngine<JToken> CreateEngine()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        return new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider)
            .UseXmlScripts()
            .UseYamlScripts();
    }

    /// <summary>Parses in whichever notation the text is written in — the engine detects it.</summary>
    private static TLioScript<JToken> Parse(string script) =>
        CreateEngine().Parse(script, JsonExecutionContext.CreateDefault().NodeAdapter);

    private static string Run(string script)
    {
        var context = JsonExecutionContext.CreateDefault();
        var result = CreateEngine().Execute(script, JToken.Parse(JsonDocument), context);
        return context.NodeAdapter.Serialize(result.Data);
    }
}
