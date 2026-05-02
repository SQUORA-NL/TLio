using System.Text.Json;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

/// <summary>
/// Challenges the MCP tools with a realistic agent scenario:
/// - Non-trivial transformation (rename fields, change values)
/// - Agent starts with a wrong or incomplete script
/// - Must converge within 3 iterations using tlio_analyze + tlio_execute feedback
///
/// These tests verify that the feedback returned is specific enough for an AI to
/// self-correct, not just that the plumbing works.
/// </summary>
[TestFixture]
public sealed class McpFeedbackQualityTests
{
    private ExecutionTools _execution = null!;
    private AnalysisTools _analysis = null!;
    private RateLimiterService _rateLimiter = null!;

    // Scenario: person.name (Alice) → person.name (Bob), person.role (admin) → person.active (true)
    private const string Input = """{"person":{"name":"Alice","role":"admin"}}""";
    private const string Target = """{"person":{"name":"Bob","active":true}}""";

    [SetUp]
    public void SetUp()
    {
        var config = Options.Create(new McpConfiguration
        {
            Observability = new ObservabilityConfig { Enabled = true },
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        _rateLimiter = new RateLimiterService(config);
        var diff = new StructuralDiffService();
        var docs = new DocumentService();
        _execution = new ExecutionTools(_rateLimiter, config);
        _analysis = new AnalysisTools(diff, docs, _rateLimiter, config);
    }

    [TearDown]
    public void TearDown() => _rateLimiter.Dispose();

    // ── Feedback quality: tlio_analyze ───────────────────────────────────────

    [Test]
    public void Analyze_GapReport_DescribesEachChangeWithPath()
    {
        var result = (AnalyzeResult)_analysis.Analyze(Input, "json", Target, "json");

        // Agent needs to know exactly which paths to operate on
        foreach (var change in result.Changes)
        {
            Assert.That(
                string.IsNullOrEmpty(change.SourcePath) && string.IsNullOrEmpty(change.TargetPath),
                Is.False,
                $"Change of type '{change.ChangeType}' has no path information — agent cannot act on it.");
            Assert.That(change.Description, Is.Not.Empty,
                $"Change at {change.SourcePath}/{change.TargetPath} has no description.");
        }
    }

    [Test]
    public void Analyze_GapReport_IdentifiesAllRequiredChanges()
    {
        var result = (AnalyzeResult)_analysis.Analyze(Input, "json", Target, "json");

        // Expected: Mutate $.person.name (Alice→Bob), Remove $.person.role, Add $.person.active
        var types = result.Changes.Select(c => c.ChangeType).ToHashSet();
        Assert.That(types, Does.Contain("Mutate"), "Should detect value change (Alice→Bob)");
        Assert.That(types, Does.Contain("Remove").Or.Contain("Add"),
            "Should detect the role→active field change");

        Console.WriteLine("=== tlio_analyze output (agent sees this) ===");
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ── Feedback quality: bad script → trace guides correction ───────────────

    [Test]
    public void Execute_WrongPath_TraceShowsNoopNotSuccess()
    {
        // Agent's first guess: wrong path (forgot the person. prefix)
        const string wrongScript = """[{"command":"set","path":"$.name","value":"Bob"}]""";

        var result = (ExecuteResult)_execution.Execute(Input, "json", wrongScript);

        // The path $.name doesn't exist on the root — should be $.person.name
        Assert.That(result.Trace, Has.Count.EqualTo(1));
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"),
            "Agent must see 'noop' not 'success' so it knows the path was wrong.");
        Assert.That(result.Trace[0].MatchedCount, Is.EqualTo(0),
            "matched_count=0 tells agent the path found nothing.");

        Console.WriteLine("=== Wrong script trace (agent sees this) ===");
        Console.WriteLine(JsonSerializer.Serialize(result.Trace, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Test]
    public void Execute_CorrectScript_TraceShowsSuccessForEachCommand()
    {
        // Corrected script — agent uses $.person.name after seeing noop on $.name
        const string correctScript = """
            [
              {"command":"set","path":"$.person.name","value":"Bob"},
              {"command":"remove","path":"$.person.role"},
              {"command":"add","path":"$.person.active","value":true}
            ]
            """;

        var result = (ExecuteResult)_execution.Execute(Input, "json", correctScript);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace, Has.Count.EqualTo(3));
        Assert.That(result.Trace.Select(t => t.Outcome), Has.All.EqualTo("success"),
            "All commands must show 'success' for agent to know it converged.");

        var output = JToken.Parse(result.Output);
        Assert.That(output["person"]!["name"]!.ToString(), Is.EqualTo("Bob"));
        Assert.That(output["person"]!["active"]!.Value<bool>(), Is.True);
        Assert.That(output["person"]!["role"], Is.Null);

        Console.WriteLine("=== Correct script output ===");
        Console.WriteLine(result.Output);
        Console.WriteLine("=== Correct script trace ===");
        Console.WriteLine(JsonSerializer.Serialize(result.Trace, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ── Three-try convergence test ────────────────────────────────────────────

    [Test]
    public void ThreeTryWorkflow_ConvergesWithinMaxIterations()
    {
        const int maxTries = 3;
        var currentDoc = Input;
        var remainingChanges = int.MaxValue;
        var traceJson = (string?)null;

        for (var attempt = 1; attempt <= maxTries; attempt++)
        {
            // Agent calls tlio_analyze each iteration to see what's left
            var analysis = (AnalyzeResult)_analysis.Analyze(currentDoc, "json", Target, "json",
                intent: "rename name to Bob, replace role with active=true",
                priorTraceJson: traceJson);

            Console.WriteLine($"=== Attempt {attempt}: {analysis.Changes.Count} changes remaining ===");
            foreach (var c in analysis.Changes)
                Console.WriteLine($"  {c.ChangeType}: {c.SourcePath} → {c.TargetPath}: {c.Description}");

            if (analysis.Changes.Count == 0)
            {
                remainingChanges = 0;
                break;
            }

            // Simulate agent building a script from the gap report
            var script = BuildScriptFromGaps(analysis.Changes);
            Console.WriteLine($"  Script: {script}");

            var execResult = (ExecuteResult)_execution.Execute(currentDoc, "json", script);
            Console.WriteLine($"  Success: {execResult.Success}, Trace entries: {execResult.Trace.Count}");
            foreach (var t in execResult.Trace)
                Console.WriteLine($"    [{t.Outcome}] {t.CommandName} @ {t.Path} (matched: {t.MatchedCount})");

            if (!execResult.Success) break;

            traceJson = JsonSerializer.Serialize(execResult.Trace);
            currentDoc = execResult.Output;
            remainingChanges = analysis.Changes.Count;
        }

        // Verify we actually converged
        var finalAnalysis = (AnalyzeResult)_analysis.Analyze(currentDoc, "json", Target, "json");
        Assert.That(finalAnalysis.Changes, Is.Empty,
            $"Did not converge within {maxTries} tries. Remaining: {finalAnalysis.Summary}");
    }

    // ── Simulate an AI agent building a script from a gap report ─────────────
    // Mimics the minimal reasoning an AI would apply given the change items:
    // parse the json_value marker from descriptions and build type-correct commands.

    private static string BuildScriptFromGaps(IReadOnlyList<ChangeItem> changes)
    {
        var commands = new JArray();
        foreach (var change in changes)
        {
            switch (change.ChangeType)
            {
                case "Mutate":
                {
                    var jv = ExtractJsonValue(change.Description);
                    if (jv is not null)
                        commands.Add(new JObject
                        {
                            ["command"] = "set",
                            ["path"] = change.SourcePath,
                            ["value"] = jv
                        });
                    break;
                }
                case "Add":
                {
                    var jv = ExtractJsonValue(change.Description);
                    if (jv is not null)
                        commands.Add(new JObject
                        {
                            ["command"] = "add",
                            ["path"] = change.TargetPath,
                            ["value"] = jv
                        });
                    break;
                }
                case "Remove":
                    commands.Add(new JObject { ["command"] = "remove", ["path"] = change.SourcePath });
                    break;
                case "Rename":
                    commands.Add(new JObject
                    {
                        ["command"] = "copy",
                        ["fromPath"] = change.SourcePath,
                        ["toPath"] = change.TargetPath
                    });
                    commands.Add(new JObject { ["command"] = "remove", ["path"] = change.SourcePath });
                    break;
            }
        }
        return commands.ToString(Newtonsoft.Json.Formatting.None);
    }

    // Parse the "json_value: <value>." marker that tlio_analyze embeds in descriptions.
    // Using JToken.Parse ensures correct type: true→bool, "Bob"→string, 42→int.
    private static JToken? ExtractJsonValue(string description)
    {
        const string marker = "json_value: ";
        var start = description.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = description.LastIndexOf('.');
        if (end <= start) return null;
        var raw = description[start..end].Trim();
        try { return JToken.Parse(raw); }
        catch { return JValue.CreateString(raw); }
    }
}
