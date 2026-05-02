using System.Text.Json.Serialization;

namespace TLio.Mcp.Models;

public sealed class CommandTraceRecord
{
    [JsonPropertyName("command_name")]
    public string CommandName { get; init; } = "";

    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("outcome")]
    public string Outcome { get; init; } = "";

    [JsonPropertyName("matched_count")]
    public int MatchedCount { get; init; }

    [JsonPropertyName("detail")]
    public string Detail { get; init; } = "";
}

public sealed class ExecuteResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("output")]
    public string Output { get; init; } = "";

    [JsonPropertyName("format")]
    public string Format { get; init; } = "";

    [JsonPropertyName("trace")]
    public IReadOnlyList<CommandTraceRecord> Trace { get; init; } = [];

    [JsonPropertyName("errors")]
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Aggregated actionable guidance for every noop and failure in the trace.
    /// An agent can inspect this list to fix all script issues in the next iteration
    /// without having to filter the trace manually.
    /// </summary>
    [JsonPropertyName("suggestions")]
    public IReadOnlyList<string> Suggestions { get; init; } = [];
}

public sealed class ChangeItem
{
    [JsonPropertyName("source_path")]
    public string SourcePath { get; set; } = "";

    [JsonPropertyName("target_path")]
    public string TargetPath { get; set; } = "";

    [JsonPropertyName("change_type")]
    public string ChangeType { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("intent_annotation")]
    public string? IntentAnnotation { get; set; }

    [JsonPropertyName("resolution")]
    public string? Resolution { get; set; }

    /// <summary>The TLio command recommended to implement this change.</summary>
    [JsonPropertyName("suggested_command")]
    public string? SuggestedCommand { get; set; }

    /// <summary>MCP call to get full docs for the suggested command.</summary>
    [JsonPropertyName("describe_call")]
    public string? DescribeCall { get; set; }
}

public sealed class AnalyzeResult
{
    [JsonPropertyName("changes")]
    public IReadOnlyList<ChangeItem> Changes { get; init; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    [JsonPropertyName("unresolved_count")]
    public int UnresolvedCount { get; init; }

    /// <summary>
    /// Grouped guidance mapping each change type to the recommended TLio command.
    /// Read this before writing a script to pick the right commands.
    /// </summary>
    [JsonPropertyName("command_guidance")]
    public string CommandGuidance { get; init; } = "";
}
