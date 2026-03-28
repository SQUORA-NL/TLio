using TLio.Client;
using TLio.Core.Models;
using TLio.Core.Models.Logging;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;

namespace TLio.Sample.Api;

/// <summary>
/// Stateless helper that loads a bundled script and executes a TLio transformation
/// for the requested data format.
/// </summary>
internal static class TransformService
{
    /// <summary>
    /// Execute the bundled transformation for <paramref name="format"/>.
    /// Returns null when the format is unknown (caller should respond with 415).
    /// Returns a failed result when parsing or execution fails (caller should respond with 422).
    /// </summary>
    public static (bool Success, string Output, IReadOnlyList<LogEntry> Log)? Execute(
        string format, string payload)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => Run(JsonExecutionContext.CreateDefault(),       "transform-json",  payload),
            "xml"  => Run(XmlExecutionContext.CreateWithNativeXPath(), "transform-xml",  payload),
            "yaml" => Run(YamlExecutionContext.CreateDefault(),        "transform-yaml", payload),
            _      => null
        };
    }

    private static (bool Success, string Output, IReadOnlyList<LogEntry> Log)? Run<TNode>(
        ExecutionContext<TNode> context,
        string scriptName,
        string payload)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", $"{scriptName}.json");
        if (!File.Exists(scriptPath))
            return (false, string.Empty, new List<LogEntry>
            {
                new(Microsoft.Extensions.Logging.LogLevel.Error, "TransformService",
                    $"Script file not found: {scriptPath}", DateTimeOffset.UtcNow)
            });

        var scriptText = File.ReadAllText(scriptPath);

        TNode input;
        try
        {
            input = context.NodeAdapter.Parse(payload);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, new List<LogEntry>
            {
                new(Microsoft.Extensions.Logging.LogLevel.Error, "TransformService",
                    $"Failed to parse input: {ex.Message}", DateTimeOffset.UtcNow)
            });
        }

        var options = ParseOptions<TNode>.CreateDefault();
        var engine  = new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider);
        var result  = engine.Execute(scriptText, input, context);
        var log     = context.GetLogEntries();

        return (result.Success, context.NodeAdapter.Serialize(result.Data), log);
    }
}
