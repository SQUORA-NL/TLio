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
    [Description("Performs a structural diff between an input and a target document. " +
                 "Returns a gap report listing every change needed (change_type: Add/Mutate/Rename/Remove/Reorder), " +
                 "with 'suggested_command' and 'describe_call' on each change telling you exactly which TLio command to use " +
                 "and how to get its full documentation. Also returns 'command_guidance' summarising all recommended commands. " +
                 "Use this to plan your script before calling tlio_execute.")]
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

            AnnotateCommandSuggestions(changes);

            var summary = BuildSummary(changes);
            var unresolvedCount = changes.Count(c => c.Resolution == "unresolved");
            var commandGuidance = BuildCommandGuidance(changes);

            return new AnalyzeResult
            {
                Changes = changes,
                Summary = summary,
                UnresolvedCount = unresolvedCount,
                CommandGuidance = commandGuidance
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

    private static void AnnotateCommandSuggestions(List<ChangeItem> changes)
    {
        foreach (var change in changes)
        {
            (change.SuggestedCommand, change.DescribeCall) = change.ChangeType switch
            {
                "Add" => ("put",
                    "tlio_describe('Put') — or tlio_describe('Add') if the field must not already exist"),
                "Mutate" => ("set",
                    "tlio_describe('Set') — or tlio_describe('Put') if the field may not exist yet"),
                "Rename" => ("rename",
                    "tlio_describe('Rename') — keeps position and XML attributes, and is the only command that renames an XML document element"),
                "Remove" => ("remove",
                    "tlio_describe('Remove')"),
                "Reorder" => ("copy + remove (rebuild order)",
                    "tlio_describe('Copy') — TLio does not have a native reorder command; rebuild the target array"),
                _ => (null, null)
            };
        }
    }

    private static string BuildCommandGuidance(List<ChangeItem> changes)
    {
        if (changes.Count == 0) return "No changes required — no commands needed.";

        var groups = changes
            .Where(c => c.SuggestedCommand != null)
            .GroupBy(c => c.SuggestedCommand!)
            .OrderBy(g => g.Key)
            .ToList();

        if (groups.Count == 0) return "No command suggestions available.";

        var lines = new List<string>
        {
            "Recommended commands for each change type:"
        };

        foreach (var g in groups)
        {
            var count = g.Count();
            var sample = g.First();
            lines.Add($"  • {count}× {g.Key}: {sample.DescribeCall}");
        }

        lines.Add("Call tlio_guide for the full command and function decision tree.");
        lines.Add("Call tlio_describe('CommandName') for usage guidance, when-to-use, and common mistakes before writing each command.");

        return string.Join("\n", lines);
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
