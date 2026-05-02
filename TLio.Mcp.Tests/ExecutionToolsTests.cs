using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

[TestFixture]
public sealed class ExecutionToolsTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private ExecutionTools _sut = null!;
    private RateLimiterService _rateLimiter = null!;

    [SetUp]
    public void SetUp()
    {
        _rateLimiter = CreateRateLimiter(observabilityEnabled: true);
        _sut = CreateTools(observabilityEnabled: true);
    }

    [TearDown]
    public void TearDown() => _rateLimiter.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RateLimiterService CreateRateLimiter(bool observabilityEnabled = true)
    {
        var config = Options.Create(new McpConfiguration
        {
            Observability = new ObservabilityConfig { Enabled = observabilityEnabled },
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        return new RateLimiterService(config);
    }

    private static ExecutionTools CreateTools(bool observabilityEnabled = true)
    {
        var config = Options.Create(new McpConfiguration
        {
            Observability = new ObservabilityConfig { Enabled = observabilityEnabled },
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        var rateLimiter = new RateLimiterService(config);
        return new ExecutionTools(rateLimiter, config);
    }

    private static (string input, string script, string expected) LoadFixture(string name)
    {
        var dir = Path.Combine(FixturesDir, name);
        return (
            File.ReadAllText(Path.Combine(dir, "input.json")),
            File.ReadAllText(Path.Combine(dir, "script.json")),
            File.ReadAllText(Path.Combine(dir, "result.json"))
        );
    }

    private static ExecuteResult AsExecuteResult(object result) => (ExecuteResult)result;

    // ── Execute_SetSuccess ────────────────────────────────────────────────────

    [Test]
    public void Execute_SetSuccess_ReturnsTrue()
    {
        var (input, script, _) = LoadFixture("Execute_SetSuccess");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void Execute_SetSuccess_OutputMatchesExpected()
    {
        var (input, script, expected) = LoadFixture("Execute_SetSuccess");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        var outputToken = JToken.Parse(result.Output);
        var expectedToken = JToken.Parse(expected);
        Assert.That(JToken.DeepEquals(outputToken, expectedToken), Is.True,
            $"Expected: {expected}\nActual: {result.Output}");
    }

    [Test]
    public void Execute_SetSuccess_TraceHasSuccessEntry()
    {
        var (input, script, _) = LoadFixture("Execute_SetSuccess");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        Assert.That(result.Trace, Has.Count.EqualTo(1));
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        Assert.That(result.Trace[0].MatchedCount, Is.EqualTo(1));
    }

    // ── Execute_NoOp ─────────────────────────────────────────────────────────

    [Test]
    public void Execute_NoOp_ReturnsTrue()
    {
        var (input, script, _) = LoadFixture("Execute_NoOp");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void Execute_NoOp_OutputUnchanged()
    {
        var (input, script, expected) = LoadFixture("Execute_NoOp");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        var outputToken = JToken.Parse(result.Output);
        var expectedToken = JToken.Parse(expected);
        Assert.That(JToken.DeepEquals(outputToken, expectedToken), Is.True,
            $"Expected: {expected}\nActual: {result.Output}");
    }

    [Test]
    public void Execute_NoOp_TraceHasNoopEntry()
    {
        var (input, script, _) = LoadFixture("Execute_NoOp");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        Assert.That(result.Trace, Has.Count.EqualTo(1));
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"));
        Assert.That(result.Trace[0].MatchedCount, Is.EqualTo(0));
    }

    // ── Execute_TypeMismatch (validation failure — missing required field) ───────

    [Test]
    public void Execute_ValidationFailure_ReturnsFalse()
    {
        var (input, script, _) = LoadFixture("Execute_TypeMismatch");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void Execute_ValidationFailure_OutputUnchanged()
    {
        var (input, script, _) = LoadFixture("Execute_TypeMismatch");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        var outputToken = JToken.Parse(result.Output);
        var inputToken = JToken.Parse(input);
        Assert.That(JToken.DeepEquals(outputToken, inputToken), Is.True);
    }

    [Test]
    public void Execute_ValidationFailure_TraceHasFailureEntry()
    {
        var (input, script, _) = LoadFixture("Execute_TypeMismatch");

        var result = AsExecuteResult(_sut.Execute(input, "json", script));

        Assert.That(result.Trace, Has.Count.EqualTo(1));
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("failure"));
        Assert.That(result.Trace[0].MatchedCount, Is.EqualTo(0));
    }

    // ── Observability disabled ────────────────────────────────────────────────

    [Test]
    public void Execute_ObservabilityDisabled_TraceIsEmpty()
    {
        var tools = CreateTools(observabilityEnabled: false);
        var (input, script, _) = LoadFixture("Execute_SetSuccess");

        var result = AsExecuteResult(tools.Execute(input, "json", script));

        Assert.That(result.Trace, Is.Empty);
        Assert.That(result.Success, Is.True);
    }

    // ── Unsupported format ────────────────────────────────────────────────────

    [Test]
    public void Execute_UnsupportedFormat_ReturnsErrorObject()
    {
        var result = _sut.Execute("{}", "toml", "[]");

        Assert.That(result, Is.Not.InstanceOf<ExecuteResult>());
        var json = JObject.FromObject(result);
        Assert.That(json["success"]!.Value<bool>(), Is.False);
        Assert.That(json["error"]!.ToString(), Does.Contain("Unsupported"));
    }
}
