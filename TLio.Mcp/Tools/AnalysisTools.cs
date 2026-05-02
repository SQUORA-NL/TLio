using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;

namespace TLio.Mcp.Tools;

[McpServerToolType]
public sealed class AnalysisTools
{
    private readonly StructuralDiffService _diff;
    private readonly DocumentService _docs;
    private readonly RateLimiterService _rateLimiter;
    private readonly McpConfiguration _config;

    public AnalysisTools(
        StructuralDiffService diff,
        DocumentService docs,
        RateLimiterService rateLimiter,
        IOptions<McpConfiguration> config)
    {
        _diff = diff;
        _docs = docs;
        _rateLimiter = rateLimiter;
        _config = config.Value;
    }

    [McpServerTool(Name = "tlio_analyze")]
    [Description("Performs a structural diff between an input and a target document. Returns a gap report listing every change needed to convert input into target.")]
    public object Analyze(
        [Description("Raw input document text")] string input,
        [Description("Input document format: 'json', 'xml', or 'yaml'")] string inputFormat,
        [Description("Raw target document text")] string target,
        [Description("Target document format: 'json', 'xml', or 'yaml'")] string targetFormat,
        [Description("Plain-language description of the transformation intent (optional)")] string? intent = null,
        [Description("Trace from a previous tlio_execute call for refinement mode (JSON array, optional)")] string? priorTraceJson = null)
    {
        if (!_rateLimiter.TryAcquire(out var retryAfter))
            return new
            {
                error = "rate_limit_exceeded",
                retry_after_seconds = retryAfter,
                message = $"Rate limit exceeded. Retry in {retryAfter} seconds."
            };

        try
        {
            var changes = ComputeChanges(input, inputFormat, target, targetFormat);

            if (!string.IsNullOrWhiteSpace(intent))
                _diff.AnnotateWithIntent(changes, intent);

            if (!string.IsNullOrWhiteSpace(priorTraceJson))
            {
                var priorTrace = ParsePriorTrace(priorTraceJson);
                if (priorTrace.Count > 0)
                    _diff.ApplyRefinement(changes, priorTrace);
            }

            var summary = BuildSummary(changes);
            var unresolvedCount = changes.Count(c => c.Resolution == "unresolved");

            return new AnalyzeResult
            {
                Changes = changes,
                Summary = summary,
                UnresolvedCount = unresolvedCount
            };
        }
        catch (Exception ex)
        {
            return new { error = "analysis_error", message = ex.Message };
        }
    }

    private List<ChangeItem> ComputeChanges(
        string inputText, string inputFormat,
        string targetText, string targetFormat)
    {
        var fmt = inputFormat.ToLowerInvariant();
        return fmt switch
        {
            "json" => _diff.DiffJson(_docs.ParseJson(inputText), _docs.ParseJson(targetText)),
            "xml" => _diff.DiffXml(_docs.ParseXml(inputText), _docs.ParseXml(targetText)),
            "yaml" => _diff.DiffYaml(_docs.ParseYaml(inputText), _docs.ParseYaml(targetText)),
            _ => throw new InvalidOperationException($"Unsupported format '{inputFormat}'. Use json, xml, or yaml.")
        };
    }

    private static IReadOnlyList<CommandTraceRecord> ParsePriorTrace(string json)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<CommandTraceRecord>>(json, options) ?? [];
        }
        catch { return []; }
    }

    private static string BuildSummary(List<ChangeItem> changes)
    {
        if (changes.Count == 0) return "No changes required.";

        var counts = changes
            .GroupBy(c => c.ChangeType)
            .ToDictionary(g => g.Key, g => g.Count());

        var parts = new List<string>();
        foreach (var ct in new[] { "Rename", "Mutate", "Add", "Remove", "Reorder" })
            if (counts.TryGetValue(ct, out var n))
                parts.Add($"{n} {ct.ToLowerInvariant()}{(n > 1 ? "s" : "")}");

        return $"{changes.Count} change{(changes.Count > 1 ? "s" : "")} required: {string.Join(", ", parts)}.";
    }
}
