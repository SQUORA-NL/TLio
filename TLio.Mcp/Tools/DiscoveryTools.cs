using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using TLio.Mcp.Configuration;
using TLio.Mcp.Services;

namespace TLio.Mcp.Tools;

[McpServerToolType]
public sealed class DiscoveryTools
{
    private readonly AiRefReader _reader;
    private readonly RateLimiterService _rateLimiter;
    private readonly McpConfiguration _config;

    public DiscoveryTools(AiRefReader reader, RateLimiterService rateLimiter, IOptions<McpConfiguration> config)
    {
        _reader = reader;
        _rateLimiter = rateLimiter;
        _config = config.Value;
    }

    [McpServerTool(Name = "tlio_list_commands")]
    [Description("Lists all available TLio commands with their names and one-line intent descriptions.")]
    public object ListCommands()
    {
        if (!_rateLimiter.TryAcquire(out var retryAfter))
            return RateLimitError(retryAfter);

        var commands = _reader.ListCommands()
            .Select(c => new { name = c.Name, intent = c.Intent })
            .ToArray();
        return new { commands };
    }

    [McpServerTool(Name = "tlio_list_functions")]
    [Description("Lists all available TLio functions with their names and one-line intent descriptions.")]
    public object ListFunctions()
    {
        if (!_rateLimiter.TryAcquire(out var retryAfter))
            return RateLimitError(retryAfter);

        var functions = _reader.ListFunctions()
            .Select(f => new { name = f.Name, intent = f.Intent })
            .ToArray();
        return new { functions };
    }

    [McpServerTool(Name = "tlio_describe")]
    [Description("Returns full documentation for a named command, function, or adapter.")]
    public object Describe(
        [Description("Exact name of the command, function, or adapter")] string name,
        [Description("Type hint: 'command', 'function', or 'adapter'. Auto-detected if omitted.")] string? type = null)
    {
        if (!_rateLimiter.TryAcquire(out var retryAfter))
            return RateLimitError(retryAfter);

        var content = _reader.GetContent(name, type);
        if (content is null)
        {
            var suggestions = _reader.GetSuggestions(name, type);
            return new { error = "not_found", name, suggestions };
        }

        var resolvedType = type ?? DetectType(name);
        return new { name, type = resolvedType, content };
    }

    private static string DetectType(string name)
    {
        // Heuristic: lowercase first char usually means function; otherwise command
        return char.IsLower(name[0]) ? "function" : "command";
    }

    private static object RateLimitError(int retryAfter) => new
    {
        error = "rate_limit_exceeded",
        retry_after_seconds = retryAfter,
        message = $"Rate limit exceeded. Retry in {retryAfter} seconds."
    };
}
