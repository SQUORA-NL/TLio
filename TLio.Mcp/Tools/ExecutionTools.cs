using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using YamlDotNet.RepresentationModel;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.ETL;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.Json;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Xml;
using TLio.Yaml;
using TLio.FormatConverter.Json;
using TLio.FormatConverter;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;

namespace TLio.Mcp.Tools;

[McpServerToolType]
public sealed class ExecutionTools
{
    private readonly RateLimiterService _rateLimiter;
    private readonly McpConfiguration _config;
    private readonly ScriptEngine<JToken> _jsonEngine;
    private readonly ScriptEngine<XElement> _xmlEngine;
    private readonly ScriptEngine<YamlNode> _yamlEngine;
    private readonly MultiFormatScriptRunner _runner;

    public ExecutionTools(RateLimiterService rateLimiter, IOptions<McpConfiguration> config)
    {
        _rateLimiter = rateLimiter;
        _config = config.Value;

        var converter = CreateConverter();
        _jsonEngine = CreateEngine<JToken>(converter, "json");
        _xmlEngine = CreateEngine<XElement>(converter, "xml");
        _yamlEngine = CreateEngine<YamlNode>(converter, "yaml");

        // A script that crosses a format boundary cannot run on one engine — the node type
        // changes at the boundary — so it goes through the runner instead, which splits it and
        // re-hosts each section on the engine for that format.
        _runner = new MultiFormatScriptRunner(converter);
        _runner.RegisterExecutor(new ScriptEngineSectionExecutor<JToken>(
            "json", _jsonEngine, () => JsonExecutionContext.CreateDefault()));
        _runner.RegisterExecutor(new ScriptEngineSectionExecutor<XElement>(
            "xml", _xmlEngine, () => XmlExecutionContext.CreateWithNativeXPath()));
        _runner.RegisterExecutor(new ScriptEngineSectionExecutor<YamlNode>(
            "yaml", _yamlEngine, () => YamlExecutionContext.CreateDefault()));
    }

    [McpServerTool(Name = "tlio_execute")]
    [Description("Executes a TLio script against a document and returns the transformed output with an execution trace. " +
                 "The result includes: 'success' (bool), 'output' (transformed document), 'trace' (per-command outcome: success/noop/failure with detail), " +
                 "and 'suggestions' (actionable fixes for every noop and failure, including tlio_describe calls for docs). " +
                 "A script may change the document's format partway through with the 'convert' command " +
                 "({\"command\":\"convert\",\"to\":\"yaml\"}); commands after it use the new format's path language, " +
                 "and 'format' in the result reports the format the run ended in. " +
                 "To convert one value in place instead — an XML payload held as a string in a JSON document — " +
                 "use 'convertValue' with a path, which leaves the surrounding document alone. " +
                 "Before writing a script: call tlio_guide for the command/function decision tree, " +
                 "then call tlio_describe('CommandName') for usage guidance and common mistakes for each command you plan to use.")]
    public object Execute(
        [Description("Raw document text to transform")] string document,
        [Description("Document format: 'json', 'xml', or 'yaml'")] string format,
        [Description("TLio script. A JSON array of command objects by default; may also be written in the XML notation (<script><set path=\"...\">...</set></script>) or the YAML notation (a sequence of '- command: set' mappings). The notation is detected from the text and is independent of the document format — set scriptFormat to override. Paths inside the script must always speak the document format's path language.")] string script,
        [Description("XML path style: 'slash' (default, simple hierarchies) or 'xpath' (predicates and axes). Both anchor on the document node, so paths name the document element: '/order/customer', never '/customer'. Only applies when format is 'xml'.")] string? xmlPathStyle = null,
        [Description("Script notation: 'json' (default), 'xml', or 'yaml'. Omit to detect it from the script text.")] string? scriptFormat = null)
    {
        if (!_rateLimiter.TryAcquire(out var retryAfter))
            return RateLimitError(retryAfter);

        ScriptFormat notation;
        if (string.IsNullOrWhiteSpace(scriptFormat))
            notation = ScriptFormatDetector.Detect(script);
        else if (!ScriptFormatDetector.TryParse(scriptFormat, out notation))
            return new { success = false, error = $"Unsupported scriptFormat '{scriptFormat}'. Use json, xml, or yaml." };

        var documentFormat = format.ToLowerInvariant();
        if (documentFormat is not ("json" or "xml" or "yaml"))
            return new { success = false, error = $"Unsupported format '{format}'. Use json, xml, or yaml." };

        // convert is a command like any other in all three notations, so the notation the
        // caller declared (or the one detected) is handed to the runner and it splits the script
        // in that notation.
        if (MultiFormatScriptRunner.CrossesAFormatBoundary(script, notation))
            return RunMultiFormat(document, documentFormat, script, notation);

        return documentFormat switch
        {
            "json" => RunJson(document, script, notation),
            "xml" => RunXml(document, script, notation, xmlPathStyle),
            _ => RunYaml(document, script, notation),
        };
    }

    /// <summary>
    /// Run a script that changes format partway through. The document's node type changes at each
    /// boundary, so no single engine can carry it — the runner splits the script and hands each
    /// section to the engine for its format.
    /// </summary>
    private ExecuteResult RunMultiFormat(
        string document, string format, string script, ScriptFormat notation)
    {
        try
        {
            var result = _runner.Run(format, document, script, notation);

            return new ExecuteResult
            {
                Success = result.Success,
                Output = result.Document,
                Format = result.FormatId,
                Errors = result.Logs
                    .Where(e => e.Level == LogLevel.Error)
                    .Select(e => e.Message)
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            return new ExecuteResult
            {
                Success = false,
                Output = document,
                Format = format,
                Errors = [$"Conversion error: {ex.Message}"]
            };
        }
    }

    private ExecuteResult RunJson(string document, string script, ScriptFormat notation)
    {
        var context = JsonExecutionContext.CreateDefault();
        return RunWithContext(document, script, notation, context, _jsonEngine, "json");
    }

    private ExecuteResult RunXml(string document, string script, ScriptFormat notation, string? pathStyle)
    {
        var context = string.Equals(pathStyle, "xpath", StringComparison.OrdinalIgnoreCase)
            ? XmlExecutionContext.CreateWithNativeXPath()
            : XmlExecutionContext.CreateWithSlashPaths();
        return RunWithContext(document, script, notation, context, _xmlEngine, "xml");
    }

    private ExecuteResult RunYaml(string document, string script, ScriptFormat notation)
    {
        var context = YamlExecutionContext.CreateDefault();
        return RunWithContext(document, script, notation, context, _yamlEngine, "yaml");
    }

    private ExecuteResult RunWithContext<TNode>(
        string documentText,
        string scriptText,
        ScriptFormat notation,
        ExecutionContext<TNode> context,
        ScriptEngine<TNode> engine,
        string format)
    {
        McpTraceCollector? collector = null;
        if (_config.Observability.Enabled)
        {
            collector = new McpTraceCollector();
            context.TraceCollector = collector;
        }

        TNode doc;
        try { doc = context.NodeAdapter.Parse(documentText); }
        catch (Exception ex)
        {
            return new ExecuteResult
            {
                Success = false,
                Output = documentText,
                Format = format,
                Errors = [$"Parse error: {ex.Message}"]
            };
        }

        TLioExecutionResult<TNode> result;
        try { result = engine.Execute(scriptText, notation, doc, context); }
        catch (Exception ex)
        {
            return new ExecuteResult
            {
                Success = false,
                Output = documentText,
                Format = format,
                Errors = [$"Script error: {ex.Message}"]
            };
        }

        string output;
        try { output = context.NodeAdapter.Serialize(result.Data); }
        catch { output = documentText; }

        var trace = collector?.Entries
            .Select(e => new CommandTraceRecord
            {
                CommandName = e.CommandName,
                Path = e.Path,
                Outcome = e.Outcome.ToString().ToLowerInvariant(),
                MatchedCount = e.MatchedCount,
                Detail = e.Detail
            })
            .ToList() ?? (IReadOnlyList<CommandTraceRecord>)[];

        var errors = context.GetLogEntries()
            .Where(e => e.Level == LogLevel.Error)
            .Select(e => e.Message)
            .ToList();

        var suggestions = BuildSuggestions(trace);

        return new ExecuteResult
        {
            Success = result.Success,
            Output = output,
            Format = format,
            Trace = trace,
            Errors = errors,
            Suggestions = suggestions
        };
    }

    private static IReadOnlyList<string> BuildSuggestions(IReadOnlyList<CommandTraceRecord> trace)
    {
        var issues = trace.Where(t => t.Outcome is "noop" or "failure").ToList();
        if (issues.Count == 0) return [];

        var suggestions = new List<string>();

        foreach (var t in issues)
        {
            var describeCall = $"tlio_describe('{t.CommandName}')";

            if (t.Outcome == "noop")
            {
                if (t.Detail.Contains("already exists"))
                    suggestions.Add(
                        $"[noop] '{t.CommandName}' at '{t.Path}': field already exists — 'add' skips existing fields by design. " +
                        $"Use 'set' to update an existing field, or 'put' for unconditional write (upsert). " +
                        $"Call {describeCall} for the add/set/put comparison table.");
                else if (t.Detail.Contains("not found") || t.Detail.Contains("0 nodes") || t.Detail.Contains("no nodes"))
                    suggestions.Add(
                        $"[noop] '{t.CommandName}' at '{t.Path}': path matched 0 nodes — the field does not exist. " +
                        $"Use 'add' or 'put' to create a new field, or call tlio_analyze to verify the correct path. " +
                        $"Call {describeCall} for when-to-use guidance and common path mistakes.");
                else
                    suggestions.Add(
                        $"[noop] '{t.CommandName}' at '{t.Path}': {t.Detail} " +
                        $"Call {describeCall} for usage guidance and common mistakes.");
            }
            else // failure
            {
                suggestions.Add(
                    $"[failure] '{t.CommandName}' at '{t.Path}': {t.Detail} " +
                    $"Call {describeCall} for required properties, when-to-use rules, and common mistakes.");
            }
        }

        suggestions.Add(
            "Call tlio_guide for the command and function decision tree. " +
            "Call tlio_describe('CommandName') for full documentation before writing each command.");

        return suggestions;
    }

    private static TLio.FormatConverter.Core.FormatConverter CreateConverter()
    {
        var converter = new TLio.FormatConverter.Core.FormatConverter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        return converter;
    }

    private static ScriptEngine<TNode> CreateEngine<TNode>(
        TLio.FormatConverter.Core.FormatConverter converter, string documentFormatId)
    {
        var options = ParseOptions<TNode>.CreateDefault();
        options.FunctionsProvider.RegisterMath<TNode>();
        options.FunctionsProvider.RegisterText<TNode>();
        options.FunctionsProvider.RegisterTimeDate<TNode>();
        options.CommandsProvider.RegisterETL<TNode>();
        options.CommandsProvider.RegisterFormatConversion<TNode>(converter, documentFormatId);
        return new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider)
            // All three notations on every engine: the notation a script is written in is
            // independent of the document it transforms, so an XML script may drive a JSON
            // document as long as its paths are JSONPath.
            .UseXmlScripts()
            .UseYamlScripts();
    }

    private static object RateLimitError(int retryAfter) => new
    {
        error = "rate_limit_exceeded",
        retry_after_seconds = retryAfter,
        message = $"Rate limit exceeded. Retry in {retryAfter} seconds."
    };
}
