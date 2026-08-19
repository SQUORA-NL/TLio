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

namespace TLio.Mcp.Tools;

[McpServerToolType]
public sealed class ExecutionTools
{
    private readonly RateLimiterService _rateLimiter;
    private readonly McpConfiguration _config;
    private readonly ScriptEngine<JToken> _jsonEngine;
    private readonly ScriptEngine<XElement> _xmlEngine;
    private readonly ScriptEngine<YamlNode> _yamlEngine;

    public ExecutionTools(RateLimiterService rateLimiter, IOptions<McpConfiguration> config)
    {
        _rateLimiter = rateLimiter;
        _config = config.Value;
        _jsonEngine = CreateEngine<JToken>();
        _xmlEngine = CreateEngine<XElement>();
        _yamlEngine = CreateEngine<YamlNode>();
    }

    [McpServerTool(Name = "tlio_execute")]
    [Description("Executes a TLio script against a document and returns the transformed output with an execution trace. " +
                 "The result includes: 'success' (bool), 'output' (transformed document), 'trace' (per-command outcome: success/noop/failure with detail), " +
                 "and 'suggestions' (actionable fixes for every noop and failure, including tlio_describe calls for docs). " +
                 "Before writing a script: call tlio_guide for the command/function decision tree, " +
                 "then call tlio_describe('CommandName') for usage guidance and common mistakes for each command you plan to use.")]
    public object Execute(
        [Description("Raw document text to transform")] string document,
        [Description("Document format: 'json', 'xml', or 'yaml'")] string format,
        [Description("TLio script as a JSON array of command objects")] string script,
        [Description("XML path style: 'slash' (default, simple hierarchies) or 'xpath' (predicates and axes). Both anchor on the document node, so paths name the document element: '/order/customer', never '/customer'. Only applies when format is 'xml'.")] string? xmlPathStyle = null)
    {
        if (!_rateLimiter.TryAcquire(out var retryAfter))
            return RateLimitError(retryAfter);

        return format.ToLowerInvariant() switch
        {
            "json" => RunJson(document, script),
            "xml" => RunXml(document, script, xmlPathStyle),
            "yaml" => RunYaml(document, script),
            _ => new { success = false, error = $"Unsupported format '{format}'. Use json, xml, or yaml." }
        };
    }

    private ExecuteResult RunJson(string document, string script)
    {
        var context = JsonExecutionContext.CreateDefault();
        return RunWithContext(document, script, context, _jsonEngine, "json");
    }

    private ExecuteResult RunXml(string document, string script, string? pathStyle)
    {
        var context = string.Equals(pathStyle, "xpath", StringComparison.OrdinalIgnoreCase)
            ? XmlExecutionContext.CreateWithNativeXPath()
            : XmlExecutionContext.CreateWithSlashPaths();
        return RunWithContext(document, script, context, _xmlEngine, "xml");
    }

    private ExecuteResult RunYaml(string document, string script)
    {
        var context = YamlExecutionContext.CreateDefault();
        return RunWithContext(document, script, context, _yamlEngine, "yaml");
    }

    private ExecuteResult RunWithContext<TNode>(
        string documentText,
        string scriptText,
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
        try { result = engine.Execute(scriptText, doc, context); }
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

    private static ScriptEngine<TNode> CreateEngine<TNode>()
    {
        var options = ParseOptions<TNode>.CreateDefault();
        options.FunctionsProvider.RegisterMath<TNode>();
        options.FunctionsProvider.RegisterText<TNode>();
        options.FunctionsProvider.RegisterTimeDate<TNode>();
        options.CommandsProvider.RegisterETL<TNode>();
        return new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider);
    }

    private static object RateLimitError(int retryAfter) => new
    {
        error = "rate_limit_exceeded",
        retry_after_seconds = retryAfter,
        message = $"Rate limit exceeded. Retry in {retryAfter} seconds."
    };
}
