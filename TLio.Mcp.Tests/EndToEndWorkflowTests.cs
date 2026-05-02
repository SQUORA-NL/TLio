using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

/// <summary>
/// Simulates the three-try agent workflow:
///   1. Call tlio_analyze to get a gap report
///   2. Build a script from the gaps and call tlio_execute
///   3. If not fully resolved, call tlio_analyze again with the prior trace
///
/// The workflow must reach the desired result within 3 iterations.
/// </summary>
[TestFixture]
public sealed class EndToEndWorkflowTests
{
    private string _tempRoot = "";
    private DiscoveryTools _discovery = null!;
    private ExecutionTools _execution = null!;
    private AnalysisTools _analysis = null!;
    private RateLimiterService _rateLimiter = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "tlio_e2e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "commands"));
        File.WriteAllText(Path.Combine(_tempRoot, "commands", "set.md"),
            "# set\nSets the value at an existing path.\n\n## Syntax\n`{\"command\":\"set\",\"path\":\"$.field\",\"value\":\"newValue\"}`");

        var config = Options.Create(new McpConfiguration
        {
            AiRefRoot = _tempRoot,
            Observability = new ObservabilityConfig { Enabled = true },
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        _rateLimiter = new RateLimiterService(config);
        var reader = new AiRefReader(config);
        var diff = new StructuralDiffService();
        var docs = new DocumentService();

        _discovery = new DiscoveryTools(reader, _rateLimiter, config);
        _execution = new ExecutionTools(_rateLimiter, config);
        _analysis = new AnalysisTools(diff, docs, _rateLimiter, config);
    }

    [TearDown]
    public void TearDown()
    {
        _rateLimiter.Dispose();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── Scenario: rename a field value ───────────────────────────────────────

    [Test]
    public void Workflow_SimpleFieldChange_ConvergesInOneStep()
    {
        const string input = """{"name":"Alice"}""";
        const string target = """{"name":"Bob"}""";

        // Step 1: analyze the gap
        var analysis = (AnalyzeResult)_analysis.Analyze(input, "json", target, "json",
            intent: "change the name value");

        Assert.That(analysis.Changes, Has.Count.EqualTo(1));
        Assert.That(analysis.Changes[0].ChangeType, Is.EqualTo("Mutate"));

        // Step 2: agent builds and runs the script based on the gap report
        const string script = """[{"command":"set","path":"$.name","value":"Bob"}]""";
        var execResult = (ExecuteResult)_execution.Execute(input, "json", script);

        Assert.That(execResult.Success, Is.True);
        Assert.That(JToken.DeepEquals(JToken.Parse(execResult.Output), JToken.Parse(target)), Is.True,
            $"Output: {execResult.Output}");

        // Step 3: verify no remaining changes (zero gaps after successful execution)
        var refinement = (AnalyzeResult)_analysis.Analyze(execResult.Output, "json", target, "json");
        Assert.That(refinement.Changes, Is.Empty, "All changes should be resolved after step 1.");
    }

    [Test]
    public void Workflow_TraceRefinement_MarksResolvedChanges()
    {
        const string input = """{"name":"Alice","age":30}""";
        const string target = """{"name":"Bob","age":30}""";

        // Successful execution trace
        const string script = """[{"command":"set","path":"$.name","value":"Bob"}]""";
        var execResult = (ExecuteResult)_execution.Execute(input, "json", script);
        Assert.That(execResult.Success, Is.True);

        // Serialize trace for refinement
        var traceJson = System.Text.Json.JsonSerializer.Serialize(execResult.Trace);

        // Analyze with prior trace — the name change should be marked resolved
        var refined = (AnalyzeResult)_analysis.Analyze(input, "json", target, "json",
            priorTraceJson: traceJson);

        Assert.That(refined.Changes, Has.Count.EqualTo(1));
        Assert.That(refined.Changes[0].Resolution, Is.EqualTo("resolved"));
        Assert.That(refined.UnresolvedCount, Is.EqualTo(0));
    }

    // ── Scenario: discovery → execute → verify ────────────────────────────────

    [Test]
    public void Workflow_DiscoverAndExecute_SetCommandWorksEndToEnd()
    {
        // Agent queries available commands
        var listResult = JObject.FromObject(_discovery.ListCommands());
        var names = listResult["commands"]!.Select(c => c["name"]!.ToString()).ToList();
        Assert.That(names, Does.Contain("set"));

        // Agent gets docs for the set command
        var descResult = JObject.FromObject(_discovery.Describe("set", "command"));
        Assert.That(descResult["error"], Is.Null);
        Assert.That(descResult["content"]!.ToString(), Does.Contain("## Syntax"));

        // Agent runs the command
        const string input = """{"status":"pending"}""";
        const string script = """[{"command":"set","path":"$.status","value":"done"}]""";
        var execResult = (ExecuteResult)_execution.Execute(input, "json", script);

        Assert.That(execResult.Success, Is.True);
        Assert.That(JToken.Parse(execResult.Output)["status"]!.ToString(), Is.EqualTo("done"));
    }
}
