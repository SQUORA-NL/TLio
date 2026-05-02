using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

[TestFixture]
public sealed class DiscoveryToolsTests : IDisposable
{
    private string _tempRoot = "";
    private DiscoveryTools _sut = null!;
    private RateLimiterService _rateLimiter = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "tlio_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "commands"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "functions"));

        File.WriteAllText(Path.Combine(_tempRoot, "commands", "set.md"),
            "# Set\nSets the value at the specified path.\n\n## Syntax\n`{ \"command\": \"set\", \"path\": \"...\", \"value\": \"...\" }`");
        File.WriteAllText(Path.Combine(_tempRoot, "commands", "add.md"),
            "# Add\nAdds a new node at the specified path.\n\n## Syntax\n`{ \"command\": \"add\", \"path\": \"...\" }`");
        File.WriteAllText(Path.Combine(_tempRoot, "commands", "copy.md"),
            "# Copy\nCopies a node from source to target path.\n\n## Syntax\n`{ \"command\": \"copy\" }`");
        File.WriteAllText(Path.Combine(_tempRoot, "functions", "concat.md"),
            "# concat\nConcatenates two or more string values.\n\n## Syntax\n`concat(a, b)`");
        File.WriteAllText(Path.Combine(_tempRoot, "functions", "format.md"),
            "# format\nFormats a value using a format string.");

        var config = Options.Create(new McpConfiguration
        {
            AiRefRoot = _tempRoot,
            RateLimit = new RateLimitConfig { RequestsPerMinute = 100, WindowCount = 6 }
        });
        _rateLimiter = new RateLimiterService(config);
        var reader = new AiRefReader(config);
        _sut = new DiscoveryTools(reader, _rateLimiter, config);
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void ListCommands_ReturnsKnownEntries()
    {
        var result = JObject.FromObject(_sut.ListCommands());

        var commands = result["commands"]!.ToObject<JArray>()!;
        var names = commands.Select(c => c["name"]!.ToString()).ToList();

        Assert.That(names, Does.Contain("set"));
        Assert.That(names, Does.Contain("add"));
        Assert.That(names, Does.Contain("copy"));
        Assert.That(commands.Count, Is.EqualTo(3));
    }

    [Test]
    public void ListCommands_EachEntry_HasIntentField()
    {
        var result = JObject.FromObject(_sut.ListCommands());
        var commands = result["commands"]!.ToObject<JArray>()!;

        foreach (var cmd in commands)
        {
            Assert.That(cmd["intent"], Is.Not.Null, $"Command '{cmd["name"]}' has no intent.");
            Assert.That(cmd["intent"]!.ToString(), Is.Not.Empty);
        }
    }

    [Test]
    public void ListFunctions_ReturnsKnownFunctions()
    {
        var result = JObject.FromObject(_sut.ListFunctions());

        var functions = result["functions"]!.ToObject<JArray>()!;
        var names = functions.Select(f => f["name"]!.ToString()).ToList();

        Assert.That(names, Does.Contain("concat"));
        Assert.That(names, Does.Contain("format"));
        Assert.That(functions.Count, Is.EqualTo(2));
    }

    [Test]
    public void Describe_ExactCommandName_ReturnsContentWithSyntax()
    {
        var result = JObject.FromObject(_sut.Describe("add", "command"));

        Assert.That(result["error"], Is.Null);
        Assert.That(result["name"]!.ToString(), Is.EqualTo("add"));
        Assert.That(result["content"]!.ToString(), Does.Contain("## Syntax"));
    }

    [Test]
    public void Describe_ExactFunctionName_ReturnsContent()
    {
        var result = JObject.FromObject(_sut.Describe("concat", "function"));

        Assert.That(result["error"], Is.Null);
        Assert.That(result["content"]!.ToString(), Does.Contain("concat"));
    }

    [Test]
    public void Describe_UnknownName_ReturnsNotFoundError()
    {
        var result = JObject.FromObject(_sut.Describe("zzz_nonexistent"));

        Assert.That(result["error"]!.ToString(), Is.EqualTo("not_found"));
        Assert.That(result["name"]!.ToString(), Is.EqualTo("zzz_nonexistent"));
    }

    [Test]
    public void Describe_NearMissName_ReturnsSuggestions()
    {
        // "st" is Levenshtein distance 1 from "set"
        var result = JObject.FromObject(_sut.Describe("st"));

        Assert.That(result["error"]!.ToString(), Is.EqualTo("not_found"));
        var suggestions = result["suggestions"]!.ToObject<string[]>()!;
        Assert.That(suggestions, Does.Contain("set"));
    }
}
