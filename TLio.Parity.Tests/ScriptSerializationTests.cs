using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Parity.Tests;

/// <summary>
/// A script that is parsed and written back out has to survive the trip.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TLioConvert.Serialize{TNode}"/> is what an editor uses to hold a parsed script as
/// JSON — a canvas that shows one command per node, an API that stores what it was given. Every
/// command whose configuration is an object rather than a string used to arrive on the other
/// side without it: the command name, path, title and description came through, and the
/// decision table, the resolve settings, the compare and csv settings did not. Executing the
/// result then failed with "Config property is required" — a script that was correct when it
/// was written and empty by the time it ran.
/// </para>
/// <para>
/// The round trip here is parse → serialize → parse → execute, held against executing the
/// original: the same document out, or the settings did not survive.
/// </para>
/// </remarks>
[TestFixture]
public class ScriptSerializationTests
{
    private static readonly string DecisionTableScript = """
    [
      { "command": "decisionTable", "path": "$",
        "title": "Mortality age band",
        "decisionTable": {
          "inputs":  [ { "name": "age",  "path": "$.calc.age" } ],
          "outputs": [ { "name": "band", "path": "$.calc.band" } ],
          "rules": [
            { "priority": 1, "conditions": { "age": "<25" },              "results": { "band": "18-24" } },
            { "priority": 2, "conditions": { "age": ">=25 && <40" },      "results": { "band": "25-39" } }
          ],
          "defaultResults": { "band": "40+" }
        }
      }
    ]
    """;

    private static readonly string ResolveScript = """
    [
      { "command": "resolve", "path": "$.lines[*]",
        "resolveSettings": [
          {
            "referencesCollectionPath": "$.tariffs",
            "resolveKeys": [ { "keyPath": "@.code", "referenceKeyPath": "@.code" } ],
            "values": [ { "targetPath": "@.rate", "value": "@.rate" } ]
          }
        ]
      }
    ]
    """;

    private static readonly string DecisionTableDocument =
        """{ "calc": { "age": 38 } }""";

    private static readonly string ResolveDocument = """
    {
      "lines":   [ { "code": "A1" }, { "code": "B2" } ],
      "tariffs": [ { "code": "A1", "rate": 12 }, { "code": "B2", "rate": 30 } ]
    }
    """;

    [TestCase("decisionTable\":{", nameof(DecisionTableScript))]
    [TestCase("resolveSettings\":[", nameof(ResolveScript))]
    public void SerializingAScript_KeepsTheSettingsBlock(string key, string scriptField)
    {
        var json = TLioConvert.Serialize(Parse(ScriptText(scriptField)), Adapter);

        Assert.That(json, Does.Contain($"\"{key}"),
            $"the {key} block was dropped on the way out, so the script no longer runs");
    }

    [TestCase(nameof(DecisionTableScript), nameof(DecisionTableDocument))]
    [TestCase(nameof(ResolveScript), nameof(ResolveDocument))]
    public void AScriptSurvivesParseSerializeParse(string scriptField, string documentField)
    {
        var script   = ScriptText(scriptField);
        var document = ScriptText(documentField);

        var direct    = Execute(script, document);
        var roundTrip = Execute(TLioConvert.Serialize(Parse(script), Adapter), document);

        Assert.Multiple(() =>
        {
            Assert.That(roundTrip.Success, Is.True, "the round-tripped script did not run");
            Assert.That(roundTrip.Data.ToString(), Is.EqualTo(direct.Data.ToString()));
        });
    }


    // ── The two silent corruptions the settings blocks travelled with ────────

    [Test]
    public void AStructuredValueSurvives_RatherThanBecomingThePlaceholder()
    {
        var script = """
        [ { "command": "add", "path": "$.order", "value": { "id": 7, "lines": [ 1, 2 ], "ok": true } } ]
        """;

        var json = TLioConvert.Serialize(Parse(script), Adapter);

        Assert.That(json, Does.Not.Contain("[expanding]"),
            "an object or array value serialized to the name of its value type, not to its value");
        Assert.That(Execute(json, "{}").Data.ToString(), Is.EqualTo(Execute(script, "{}").Data.ToString()));
    }

    [TestCase("xml")]
    [TestCase("yaml")]
    public void AValueIsWrittenAsJson_WhateverFormatTheDocumentIsIn(string format)
    {
        // The notation a script is written in is independent of the format it transforms, and
        // that holds in this direction too: serializing to JSON must not emit the document
        // format's own text. It used to hand the whole value to the adapter's serializer, which
        // wrote "<value>…" for XML and a bare "=concat(…)" for YAML — neither of them JSON.
        var serialized = format == "xml" ? SerializeXml() : SerializeYaml();

        Assert.DoesNotThrow(() => JToken.Parse(serialized),
            $"serializing a script for a {format} document produced text that is not JSON: {serialized}");
    }

    private static string SerializeXml()
    {
        var options = FormatRunners.Options<XElement>();
        var adapter = new XmlNodeAdapter();
        var parser  = new XmlScriptParser<XElement>(options.CommandsProvider, options.FunctionsProvider, adapter);

        return TLioConvert.Serialize(parser.ParseScript(
            """<script><add path="/order/total" value="=concat('EUR ', /order/net)"/></script>"""), adapter);
    }

    private static string SerializeYaml()
    {
        var options = FormatRunners.Options<YamlNode>();
        var adapter = YamlExecutionContext.CreateDefault().NodeAdapter;
        var parser  = new YamlScriptParser<YamlNode>(options.CommandsProvider, options.FunctionsProvider, adapter);

        return TLioConvert.Serialize(parser.ParseScript(
            "- command: add\n  path: $.order.total\n  value: \"=concat('EUR ', $.order.net)\"\n"), adapter);
    }

    // ── Every sample, through the same trip ──────────────────────────────────

    /// <summary>
    /// The samples in <c>docs/samples</c> are the largest scripts in the repository and the ones
    /// people copy: decision tables, resolves, nested settings, all three notations. Each is
    /// parsed, written back out as JSON, parsed again and run — and has to produce the output
    /// committed next to it.
    /// </summary>
    /// <remarks>
    /// Samples that cross a format boundary are skipped: <c>convert</c> needs
    /// MultiFormatScriptRunner, which lives in a package this project deliberately does not
    /// reference. They are covered by TLio.FormatConverter.Tests.
    /// </remarks>
    [TestCaseSource(nameof(SampleDirectories))]
    public void ASampleSurvivesParseSerializeParse(string directory)
    {
        var scriptPath = Directory.EnumerateFiles(directory, "script.*").Single();
        var inputPath  = Directory.EnumerateFiles(directory, "input.*").Single();
        var outputPath = Directory.EnumerateFiles(directory, "output.*").Single();

        var (actual, expected, success, log) = Path.GetExtension(inputPath) switch
        {
            ".xml"  => RunXmlSample(scriptPath, inputPath, outputPath),
            ".yaml" => RunYamlSample(scriptPath, inputPath, outputPath),
            _       => RunJsonSample(scriptPath, inputPath, outputPath),
        };

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True, $"the round-tripped sample did not run: {log}");
            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    private static IEnumerable<TestCaseData> SampleDirectories()
    {
        var samples = Path.Combine(RepositoryRoot, "docs", "samples");

        foreach (var directory in Directory.EnumerateDirectories(samples, "*", SearchOption.AllDirectories).Order())
        {
            if (!Directory.EnumerateFiles(directory, "script.*").Any()) continue;
            if (!Directory.EnumerateFiles(directory, "input.*").Any()) continue;
            if (!Directory.EnumerateFiles(directory, "output.*").Any()) continue;

            var script = File.ReadAllText(Directory.EnumerateFiles(directory, "script.*").Single());
            if (CrossesAFormatBoundary(script)) continue;

            yield return new TestCaseData(directory)
                .SetName($"ASampleSurvivesParseSerializeParse({Path.GetRelativePath(samples, directory)})");
        }
    }

    private static bool CrossesAFormatBoundary(string script) =>
        script.Contains("\"command\": \"convert\"") ||
        script.Contains("command: convert") ||
        script.Contains("<convert ");

    private static string RepositoryRoot
    {
        get
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "samples")))
                dir = dir.Parent;

            return dir?.FullName ?? throw new DirectoryNotFoundException("docs/samples is not above the test directory.");
        }
    }

    private static (string Actual, string Expected, bool Success, string Log) RunJsonSample(
        string scriptPath, string inputPath, string outputPath)
    {
        var script   = RoundTrip(File.ReadAllText(scriptPath), Adapter, Parse);
        var outcome  = Execute(script, File.ReadAllText(inputPath));

        return (outcome.Data.ToString(), JToken.Parse(File.ReadAllText(outputPath)).ToString(), outcome.Success, "");
    }

    private static (string Actual, string Expected, bool Success, string Log) RunXmlSample(
        string scriptPath, string inputPath, string outputPath)
    {
        var options = FormatRunners.Options<XElement>();
        var adapter = new XmlNodeAdapter();
        var parser  = new XmlScriptParser<XElement>(options.CommandsProvider, options.FunctionsProvider, adapter);
        var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
        var context = XmlExecutionContext.CreateWithNativeXPath();

        var json   = TLioConvert.Serialize(parser.ParseScript(File.ReadAllText(scriptPath)), adapter);
        var result = engine.Parse(json, adapter).Execute(adapter.Parse(File.ReadAllText(inputPath)), context);

        return (result.Data.ToString(),
                XDocument.Parse(File.ReadAllText(outputPath)).Root!.ToString(),
                result.Success,
                string.Join("; ", context.GetLogEntries().Where(e => e.Level >= LogLevel.Warning).Select(e => e.Message)));
    }

    private static (string Actual, string Expected, bool Success, string Log) RunYamlSample(
        string scriptPath, string inputPath, string outputPath)
    {
        var options = FormatRunners.Options<YamlNode>();
        var context = YamlExecutionContext.CreateDefault();
        var adapter = context.NodeAdapter;
        var parser  = new YamlScriptParser<YamlNode>(options.CommandsProvider, options.FunctionsProvider, adapter);
        var engine  = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);

        var json   = TLioConvert.Serialize(parser.ParseScript(File.ReadAllText(scriptPath)), adapter);
        var result = engine.Parse(json, adapter).Execute(adapter.Parse(File.ReadAllText(inputPath)), context);

        return (adapter.Serialize(result.Data, false).Trim(),
                File.ReadAllText(outputPath).Trim(),
                result.Success,
                string.Join("; ", context.GetLogEntries().Where(e => e.Level >= LogLevel.Warning).Select(e => e.Message)));
    }

    private static string RoundTrip(
        string script, INodeAdapter<JToken> adapter, Func<string, Core.Models.TLioScript<JToken>> parse) =>
        TLioConvert.Serialize(parse(script), adapter);

    // ── helpers ──────────────────────────────────────────────────────────────

    private static INodeAdapter<JToken> Adapter => JsonExecutionContext.CreateDefault().NodeAdapter;

    private static string ScriptText(string field) =>
        (string)typeof(ScriptSerializationTests)
            .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

    private static Core.Models.TLioScript<JToken> Parse(string script)
    {
        var options = FormatRunners.Options<JToken>();
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        return engine.Parse(script, Adapter);
    }

    private static (bool Success, JToken Data) Execute(string script, string document)
    {
        var options = FormatRunners.Options<JToken>();
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result  = engine.Execute(script, JToken.Parse(document), JsonExecutionContext.CreateDefault());
        return (result.Success, result.Data);
    }
}
