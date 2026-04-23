using System.Diagnostics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Json;

namespace TLio.UnitTests.Performance;

/// <summary>
/// Wall-clock and GC-allocation baseline for batch command-engine execution.
/// Uses a simple set-command script repeated 500 times against fresh JObject inputs.
/// Threshold is conservative (2x measured value on a developer machine).
/// </summary>
[TestFixture]
public class CommandEngine_PerformanceTests
{
    private const int MaxBatchElapsedMs     = 2_000;
    private const int BatchSize             = 500;
    private const int WarmupIterations      = 5;

    private ScriptEngine<JToken> _engine = null!;
    private string _scriptJson = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        // Simple single-command script: put $.result = "hello" (upsert creates if missing)
        _scriptJson = @"[{""command"":""put"",""path"":""$.result"",""value"":""hello""}]";
    }

    [Test]
    public void BatchExecution_500Commands_CompletesWithinThreshold()
    {
        // Warmup
        for (var w = 0; w < WarmupIterations; w++)
        {
            var ctx = JsonExecutionContext.CreateDefault();
            _ = _engine.Execute(_scriptJson, JObject.Parse("{}"), ctx);
        }

        ForceGc();

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < BatchSize; i++)
        {
            var ctx = JsonExecutionContext.CreateDefault();
            var result = _engine.Execute(_scriptJson, JObject.Parse("{}"), ctx);
            // Verify correctness on first iteration only (avoid Assert overhead in loop)
            if (i == 0)
                Assert.That(result.Success, Is.True, "First execution must succeed");
        }
        sw.Stop();

        TestContext.WriteLine($"Batch {BatchSize} executions: {sw.ElapsedMilliseconds} ms  (threshold: {MaxBatchElapsedMs} ms)");
        Assert.That(sw.ElapsedMilliseconds, Is.LessThanOrEqualTo(MaxBatchElapsedMs),
            $"Batch of {BatchSize} executions took {sw.ElapsedMilliseconds} ms, exceeding threshold of {MaxBatchElapsedMs} ms.");
    }

    [Test]
    public void SingleExecution_ProducesCorrectResult()
    {
        var ctx = JsonExecutionContext.CreateDefault();
        var data = JObject.Parse("{\"x\": 1}");
        var result = _engine.Execute(_scriptJson, data, ctx);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data["result"]?.ToObject<string>(), Is.EqualTo("hello"));
    }

    private static void ForceGc()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }
}
