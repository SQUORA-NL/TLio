using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Client;
using TLio.Json.SystemText;
using TLio.Json.SystemText.Tests.Fixtures;

namespace TLio.Json.SystemText.Tests.CompiledScriptTests;

/// <summary>
/// Verifies that CompiledScript is safe for concurrent use: 100 parallel executions each
/// produce the correct, independent result with no cross-execution state contamination.
/// </summary>
[TestFixture]
public class CompiledScript_ConcurrencyTests
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

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "CompiledScript" })]
    public void ConcurrentExecutions_AllProduceCorrectResult(string inputJson, string scriptJson, string expectedJson)
    {
        const int Concurrency = 100;
        var compiled = _engine.Compile(scriptJson, _adapter);
        var expected = _adapter.Parse(expectedJson);

        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, Concurrency, _ =>
        {
            var input  = _adapter.Parse(inputJson);
            var ctx    = SystemTextJsonExecutionContext.CreateDefault();
            var result = compiled.Execute(input, ctx);

            if (!result.Success)
                errors.Add($"Execution failed. Log: {string.Join(", ", ctx.GetLogEntries().Select(e => e.Message))}");
            else if (!JsonNode.DeepEquals(result.Data, expected))
                errors.Add($"Result mismatch. Expected: {expected}  Actual: {result.Data}");
        });

        Assert.That(errors, Is.Empty,
            $"{errors.Count} of {Concurrency} concurrent executions had errors:\n" +
            string.Join("\n", errors.Take(5)));
    }

    [Test]
    public void CreateExecutable_IsSafeToCallConcurrently()
    {
        var scriptJson = """[{"command":"set","path":"$.x","value":1}]""";
        var compiled   = _engine.Compile(scriptJson, _adapter);

        var executables = new System.Collections.Concurrent.ConcurrentBag<object>();
        Parallel.For(0, 100, _ => executables.Add(compiled.CreateExecutable()));

        Assert.That(executables.Count, Is.EqualTo(100));
    }
}
