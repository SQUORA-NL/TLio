using Microsoft.Extensions.Options;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

[TestFixture]
public sealed class AnalysisToolsTests
{
    private AnalysisTools _sut = null!;
    private RateLimiterService _rateLimiter = null!;

    [SetUp]
    public void SetUp()
    {
        var config = Options.Create(new McpConfiguration
        {
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        _rateLimiter = new RateLimiterService(config);
        var diff = new StructuralDiffService();
        var docs = new DocumentService();
        _sut = new AnalysisTools(diff, docs, _rateLimiter, config);
    }

    [TearDown]
    public void TearDown() => _rateLimiter.Dispose();

    private static AnalyzeResult Analyze(AnalysisTools sut,
        string input, string target,
        string? intent = null, string? priorTraceJson = null)
    {
        var result = sut.Analyze(input, "json", target, "json", intent, priorTraceJson);
        return (AnalyzeResult)result;
    }

    // ── No changes ───────────────────────────────────────────────────────────

    [Test]
    public void Analyze_IdenticalDocuments_ZeroChanges()
    {
        const string doc = """{"name":"Alice","age":30}""";

        var result = Analyze(_sut, doc, doc);

        Assert.That(result.Changes, Is.Empty);
        Assert.That(result.Summary, Is.EqualTo("No changes required."));
        Assert.That(result.UnresolvedCount, Is.EqualTo(0));
    }

    // ── Mutate ───────────────────────────────────────────────────────────────

    [Test]
    public void Analyze_ChangedField_DetectsMutate()
    {
        const string input = """{"name":"Alice"}""";
        const string target = """{"name":"Bob"}""";

        var result = Analyze(_sut, input, target);

        Assert.That(result.Changes, Has.Count.EqualTo(1));
        Assert.That(result.Changes[0].ChangeType, Is.EqualTo("Mutate"));
        Assert.That(result.Changes[0].SourcePath, Is.EqualTo("$.name"));
    }

    [Test]
    public void Analyze_ChangedField_SummaryMentionsMutate()
    {
        const string input = """{"x":1}""";
        const string target = """{"x":2}""";

        var result = Analyze(_sut, input, target);

        Assert.That(result.Summary, Does.Contain("mutate"));
    }

    // ── Add ──────────────────────────────────────────────────────────────────

    [Test]
    public void Analyze_AddedField_DetectsAdd()
    {
        const string input = """{"name":"Alice"}""";
        const string target = """{"name":"Alice","age":30}""";

        var result = Analyze(_sut, input, target);

        Assert.That(result.Changes, Has.Count.EqualTo(1));
        Assert.That(result.Changes[0].ChangeType, Is.EqualTo("Add"));
        Assert.That(result.Changes[0].TargetPath, Is.EqualTo("$.age"));
    }

    // ── Remove ───────────────────────────────────────────────────────────────

    [Test]
    public void Analyze_RemovedField_DetectsRemove()
    {
        const string input = """{"name":"Alice","age":30}""";
        const string target = """{"name":"Alice"}""";

        var result = Analyze(_sut, input, target);

        Assert.That(result.Changes, Has.Count.EqualTo(1));
        Assert.That(result.Changes[0].ChangeType, Is.EqualTo("Remove"));
        Assert.That(result.Changes[0].SourcePath, Is.EqualTo("$.age"));
    }

    // ── Intent annotation ────────────────────────────────────────────────────

    [Test]
    public void Analyze_IntentMatchesPath_AnnotatesChange()
    {
        const string input = """{"name":"Alice"}""";
        const string target = """{"name":"Bob"}""";

        var result = Analyze(_sut, input, target, intent: "rename the name field");

        Assert.That(result.Changes[0].IntentAnnotation, Is.Not.Null);
        Assert.That(result.Changes[0].IntentAnnotation, Does.Contain("name"));
    }

    [Test]
    public void Analyze_IntentNoMatch_NoAnnotation()
    {
        const string input = """{"x":1}""";
        const string target = """{"x":2}""";

        var result = Analyze(_sut, input, target, intent: "adjust the foobar value");

        Assert.That(result.Changes[0].IntentAnnotation, Is.Null);
    }

    // ── Refinement via prior trace ────────────────────────────────────────────

    [Test]
    public void Analyze_PriorTraceWithSuccessPath_MarksResolved()
    {
        const string input = """{"name":"Alice"}""";
        const string target = """{"name":"Bob"}""";
        const string priorTrace = """[{"command_name":"set","path":"$.name","outcome":"success","matched_count":1,"detail":""}]""";

        var result = Analyze(_sut, input, target, priorTraceJson: priorTrace);

        Assert.That(result.Changes[0].Resolution, Is.EqualTo("resolved"));
        Assert.That(result.UnresolvedCount, Is.EqualTo(0));
    }

    [Test]
    public void Analyze_PriorTraceWithNoMatchingPath_MarksUnresolved()
    {
        const string input = """{"name":"Alice"}""";
        const string target = """{"name":"Bob"}""";
        const string priorTrace = """[{"command_name":"set","path":"$.other","outcome":"success","matched_count":1,"detail":""}]""";

        var result = Analyze(_sut, input, target, priorTraceJson: priorTrace);

        Assert.That(result.Changes[0].Resolution, Is.EqualTo("unresolved"));
        Assert.That(result.UnresolvedCount, Is.EqualTo(1));
    }

    // ── Summary formatting ────────────────────────────────────────────────────

    [Test]
    public void Analyze_MultipleChanges_SummaryListsAll()
    {
        const string input = """{"a":1,"b":"old"}""";
        const string target = """{"b":"new","c":3}""";

        var result = Analyze(_sut, input, target);

        Assert.That(result.Changes.Count, Is.GreaterThan(1));
        Assert.That(result.Summary, Does.Contain("change"));
    }
}
