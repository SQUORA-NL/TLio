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
    [Description("Executes a TLio script against a document and returns the transformed output with an execution trace.")]
    public object Execute(
        [Description("Raw document text to transform")] string document,
        [Description("Document format: 'json', 'xml', or 'yaml'")] string format,
        [Description("TLio script as a JSON array of command objects")] string script,
        [Description("XML path style: 'slash' (default) or 'xpath'. Only applies when format is 'xml'.")] string? xmlPathStyle = null)
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

        return issues.Select(t => t.Outcome switch
        {
            "noop" =>
                $"[noop] '{t.CommandName}' at '{t.Path}' matched 0 nodes — the path does not exist in the document. " +
                $"If updating an existing field use 'set'; if creating a new field use 'add'. " +
                $"Call tlio_analyze to see the exact paths that need changes.",
            "failure" =>
                $"[failure] '{t.CommandName}' at '{t.Path}' — {t.Detail} " +
                $"Review the command definition and ensure all required properties (path, value) are present.",
            _ => $"[{t.Outcome}] '{t.CommandName}' at '{t.Path}': {t.Detail}"
        }).ToList();
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
