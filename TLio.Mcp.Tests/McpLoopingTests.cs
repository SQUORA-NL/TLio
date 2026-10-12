using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

/// <summary>
/// forEach and while live in TLio.Extensions.Looping, an opt-in pack. The ai-ref documents them,
/// so the server has to register them — otherwise tlio_describe teaches a command that
/// tlio_execute then rejects.
/// </summary>
[TestFixture]
public sealed class McpLoopingTests
{
    private ExecutionTools _execution = null!;
    private DiscoveryTools _discovery = null!;
    private RateLimiterService _rateLimiter = null!;

    [SetUp]
    public void SetUp()
    {
        var config = Options.Create(new McpConfiguration
        {
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        _rateLimiter = new RateLimiterService(config);
        _execution = new ExecutionTools(_rateLimiter, config);
        _discovery = new DiscoveryTools(new AiRefReader(config), _rateLimiter, config);
    }

    [TearDown]
    public void TearDown() => _rateLimiter.Dispose();

    [Test]
    public void ForEach_RunsTheNestedScriptOncePerElement()
    {
        const string doc = """{"items":[{"p":1},{"p":2},{"p":3}]}""";
        const string script = """
            [{"command":"forEach","path":"$.items","commands":[
              {"command":"put","path":"@.double","value":"=multiply(@.p, 2)"}
            ]}]
            """;

        var result = (ExecuteResult)_execution.Execute(doc, "json", script);

        Assert.That(result.Success, Is.True, result.Output);
        var items = JObject.Parse(result.Output)["items"]!;
        Assert.That(items.Select(i => (int)i["double"]!), Is.EqualTo(new[] { 2, 4, 6 }));
    }

    [Test]
    public void While_RepeatsUntilTheConditionFails()
    {
        const string doc = """{"n":0}""";
        const string script = """
            [{"command":"while","condition":"=lessThan($.n, 3)","maxIterations":10,"commands":[
              {"command":"set","path":"$.n","value":"=sum($.n, 1)"}
            ]}]
            """;

        var result = (ExecuteResult)_execution.Execute(doc, "json", script);

        Assert.That(result.Success, Is.True, result.Output);
        Assert.That((int)JObject.Parse(result.Output)["n"]!, Is.EqualTo(3));
    }

    [Test]
    public void Describe_ReportsCamelCaseCommandsAsCommands()
    {
        var json = JObject.FromObject(_discovery.Describe("forEach"));
        Assert.That((string?)json["type"], Is.EqualTo("command"));
    }

    [Test]
    public void LoopingCommands_AreListedForDiscovery()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(_discovery.ListCommands());
        Assert.That(json, Does.Contain("ForEach").And.Contain("While"));
    }
}
