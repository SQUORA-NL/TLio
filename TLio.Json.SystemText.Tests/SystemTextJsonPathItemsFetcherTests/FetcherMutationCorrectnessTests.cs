using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Client;
using TLio.Json.SystemText;
using TLio.Json.SystemText.Tests.Fixtures;

namespace TLio.Json.SystemText.Tests.SystemTextJsonPathItemsFetcherTests;

/// <summary>
/// Verifies that the fetcher's document cache reflects mid-execution mutations.
/// A command that mutates a node must be visible to subsequent path selections in the same script.
/// </summary>
[TestFixture]
public class FetcherMutationCorrectnessTests
{
    private ScriptEngine<JsonNode> _engine = null!;
    private SystemTextJsonNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        _engine  = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
        _adapter = new SystemTextJsonNodeAdapter();
    }

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "FetcherMutation" })]
    public void MutationIsVisibleToSubsequentCommands(string inputJson, string scriptJson, string expectedJson)
    {
        var input    = _adapter.Parse(inputJson);
        var expected = _adapter.Parse(expectedJson);
        var ctx      = SystemTextJsonExecutionContext.CreateDefault();

        var result = _engine.Execute(scriptJson, input, ctx);

        Assert.That(result.Success, Is.True,
            $"Execution failed.\n{string.Join("\n", ctx.GetLogEntries().Select(e => $"  [{e.Level}] {e.Message}"))}");
        Assert.That(JsonNode.DeepEquals(result.Data, expected), Is.True,
            $"Post-mutation value not reflected.\n  Expected: {expected}\n  Actual:   {result.Data}");
    }

    [Test]
    public void MultipleSequentialMutations_EachVisibleToNextCommand()
    {
        // Three commands: set $.a, copy $.a→$.b, copy $.b→$.c
        var script = """
            [
              {"command":"set",  "path":"$.a",         "value":"step1"},
              {"command":"copy", "fromPath":"$.a",      "toPath":"$.b"},
              {"command":"copy", "fromPath":"$.b",      "toPath":"$.c"}
            ]
            """;

        var input    = _adapter.Parse("""{"a":"","b":"","c":""}""");
        var expected = _adapter.Parse("""{"a":"step1","b":"step1","c":"step1"}""");
        var ctx      = SystemTextJsonExecutionContext.CreateDefault();

        var result = _engine.Execute(script, input, ctx);

        Assert.That(result.Success, Is.True);
        Assert.That(JsonNode.DeepEquals(result.Data, expected), Is.True,
            $"Chain of mutations not fully reflected.\n  Expected: {expected}\n  Actual:   {result.Data}");
    }
}
