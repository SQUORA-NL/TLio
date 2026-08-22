using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using TLio.Core.Contracts;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;
using TLio.Client;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;

namespace TLio.Parity.Tests;

/// <summary>Every trace entry a sweep produced, in the order the commands ran.</summary>
public sealed class RecordingTraceCollector : ITraceCollector
{
    public List<TraceEntry> Entries { get; } = new();
    public void Record(TraceEntry entry) => Entries.Add(entry);
}

/// <summary>The result of running a sweep script against one format.</summary>
public sealed record SweepRun(
    string Format,
    bool Success,
    string Document,
    IReadOnlyList<TraceEntry> Trace,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Commands that matched nothing or failed — a function that silently did not run.</summary>
    public IEnumerable<TraceEntry> NotApplied =>
        Trace.Where(t => t.Outcome != TraceOutcome.Success);

    public string Report() =>
        new StringBuilder()
            .AppendLine($"{Format}: {Trace.Count} commands, {NotApplied.Count()} not applied")
            .AppendLine(string.Join("\n", NotApplied.Select((t, i) => $"    [{t.Outcome}] {t.CommandName} @ {t.Path} — {t.Detail}")))
            .ToString();
}

/// <summary>
/// Runs a sweep script — one script that touches every command and every function — against a
/// format, starting from an empty document, and records what each command actually did.
///
/// <para>The script text handed in is the script as written, not a translation of one. JSON and
/// YAML get the same file: they share the path language, and JSON is a subset of YAML, so the
/// YAML parser reads a .json script unchanged. XML gets its own file, written in XPath, because
/// its path language is a different one — which is the point of the path language being
/// injected rather than assumed.</para>
///
/// <para>The document comparison alone would catch a wrong value; the trace is what catches a
/// command that quietly matched nothing, which is how an unsupported function or path form
/// fails.</para>
/// </summary>
public static class SweepRunner
{
    /// <summary>
    /// Runs one format and turns a throw into a reported failure. A command is allowed to warn
    /// and no-op; throwing out of the engine is a defect, and one format doing it must not hide
    /// what the other two did.
    /// </summary>
    private static SweepRun Guard(string format, Func<SweepRun> run)
    {
        try { return run(); }
        catch (Exception ex)
        {
            return new SweepRun(format, false, $"THREW: {ex.GetType().Name}: {ex.Message}",
                Array.Empty<TraceEntry>(), new[] { $"THREW {ex.GetType().Name}: {ex.Message}" });
        }
    }

    public static SweepRun Run(string format, string script) => format switch
    {
        "XML"  => Guard("XML",  () => RunXml(script)),
        "YAML" => Guard("YAML", () => RunYaml(script)),
        _      => Guard("JSON", () => RunJson(script)),
    };

    public static SweepRun RunJson(string scriptJson)
    {
        var options = FormatRunners.Options<JToken>();
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var context = JsonExecutionContext.CreateDefault();
        var trace   = new RecordingTraceCollector();
        context.TraceCollector = trace;

        var result = engine.Execute(scriptJson, JToken.Parse("{}"), context);

        return new SweepRun("JSON", result.Success,
            FormatRunners.RenderForComparison(result.Data), trace.Entries, Warnings(context));
    }

    public static SweepRun RunXml(string scriptXml)
    {
        var options = FormatRunners.Options<XElement>();
        var adapter = new XmlNodeAdapter();
        var parser  = new XmlScriptParser<XElement>(
            options.CommandsProvider, options.FunctionsProvider, adapter);
        var context = XmlExecutionContext.CreateWithSlashPaths();
        var trace   = new RecordingTraceCollector();
        context.TraceCollector = trace;

        var data   = adapter.Parse($"<{CanonicalShape.RootName}/>");
        var result = parser.ParseScript(scriptXml).Execute(data, context);

        return new SweepRun("XML", result.Success,
            FormatRunners.RenderXmlForComparison(result.Data), trace.Entries, Warnings(context));
    }

    public static SweepRun RunYaml(string script)
    {
        var options = FormatRunners.Options<YamlNode>();
        var context = YamlExecutionContext.CreateDefault();
        var adapter = context.NodeAdapter;
        var parser  = new YamlScriptParser<YamlNode>(
            options.CommandsProvider, options.FunctionsProvider, adapter);
        var trace   = new RecordingTraceCollector();
        context.TraceCollector = trace;

        // The .json script text, unchanged — JSON is a subset of YAML.
        var data   = adapter.Parse("{}");
        var result = parser.ParseScript(script).Execute(data, context);

        return new SweepRun("YAML", result.Success,
            FormatRunners.RenderYamlForComparison(result.Data), trace.Entries, Warnings(context));
    }

    /// <summary>
    /// The sweep parsed, written back out as a JSON script, and run from that — the trip a
    /// script makes through an editor that holds it as JSON.
    /// </summary>
    /// <remarks>
    /// The sweep already proves every registered command runs. This proves the other half:
    /// that every registered command can be written out and read back. A command whose
    /// configuration is an object — a decision table, a resolve, a compare — used to lose it
    /// silently here, and only the command name, path and title arrived on the other side.
    /// </remarks>
    public static SweepRun RunSerialized(string format, string script) => format switch
    {
        // What comes back from the serializer is a JSON script whatever notation went in, so it
        // is read by the engine — which detects the notation — rather than by the notation's own
        // parser. The document stays in its own format; the notation never was tied to it.
        "XML"  => Guard("XML",  () => RunXmlJsonScript(SerializeXml(script))),
        "YAML" => Guard("YAML", () => RunYamlJsonScript(SerializeYaml(script))),
        _      => Guard("JSON", () => RunJson(SerializeJson(script))),
    };

    private static SweepRun RunXmlJsonScript(string scriptJson)
    {
        var options = FormatRunners.Options<XElement>();
        var adapter = new XmlNodeAdapter();
        var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
        var context = XmlExecutionContext.CreateWithSlashPaths();
        var trace   = new RecordingTraceCollector();
        context.TraceCollector = trace;

        var data   = adapter.Parse($"<{CanonicalShape.RootName}/>");
        var result = engine.Parse(scriptJson, adapter).Execute(data, context);

        return new SweepRun("XML", result.Success,
            FormatRunners.RenderXmlForComparison(result.Data), trace.Entries, Warnings(context));
    }

    private static SweepRun RunYamlJsonScript(string scriptJson)
    {
        var options = FormatRunners.Options<YamlNode>();
        var context = YamlExecutionContext.CreateDefault();
        var adapter = context.NodeAdapter;
        var engine  = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
        var trace   = new RecordingTraceCollector();
        context.TraceCollector = trace;

        var data   = adapter.Parse("{}");
        var result = engine.Parse(scriptJson, adapter).Execute(data, context);

        return new SweepRun("YAML", result.Success,
            FormatRunners.RenderYamlForComparison(result.Data), trace.Entries, Warnings(context));
    }

    private static string SerializeJson(string script)
    {
        var options = FormatRunners.Options<JToken>();
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        return TLioConvert.Serialize(engine.Parse(script, adapter), adapter);
    }

    private static string SerializeXml(string script)
    {
        var options = FormatRunners.Options<XElement>();
        var adapter = new XmlNodeAdapter();
        var parser  = new XmlScriptParser<XElement>(
            options.CommandsProvider, options.FunctionsProvider, adapter);

        return TLioConvert.Serialize(parser.ParseScript(script), adapter);
    }

    private static string SerializeYaml(string script)
    {
        var options = FormatRunners.Options<YamlNode>();
        var adapter = YamlExecutionContext.CreateDefault().NodeAdapter;
        var parser  = new YamlScriptParser<YamlNode>(
            options.CommandsProvider, options.FunctionsProvider, adapter);

        return TLioConvert.Serialize(parser.ParseScript(script), adapter);
    }

    private static List<string> Warnings<TNode>(IExecutionContext<TNode> context) =>
        context.GetLogEntries()
            .Where(e => e.Level >= Microsoft.Extensions.Logging.LogLevel.Warning)
            .Select(e => e.Message).ToList();
}
