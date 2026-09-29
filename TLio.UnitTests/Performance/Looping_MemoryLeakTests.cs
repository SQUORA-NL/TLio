using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Looping;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.Performance;

/// <summary>
/// Regression check for unbounded memory growth: one <see cref="CompiledScript{TNode}"/> —
/// the same shared-singleton shape a long-lived host holds for the life of the process — is
/// executed thousands of times against a fresh document and a fresh
/// <see cref="TLio.Core.Contracts.IExecutionContext{TNode}"/> per call, in batches, forcing a
/// full GC and measuring the managed heap after each batch.
///
/// A real leak — something a compiled script's commands or a shared execution context pins
/// across calls, or a cache that grows per node touched rather than per document (the shape
/// <c>YamlParentTracker</c> is called out for in the repo's concurrency notes) — shows up here as
/// heap-after-batch-N growing roughly proportionally to N. A healthy steady state does not: once
/// warmup/JIT settles, each batch's garbage is collected back out, so heap after the last batch
/// stays within a small, constant multiple of heap after the first.
///
/// Unlike <see cref="Looping_PortfolioBenchmarkTests"/> this is a threshold-based CI gate, not a
/// manual benchmark: it runs single-threaded so the measurement isn't confused by concurrent
/// allocation, and its batch size is small enough (unlike the 100k/1M portfolios) to run in every
/// build.
/// </summary>
[TestFixture]
public class Looping_MemoryLeakTests
{
    /// <summary>Same two-primitive shape as <see cref="Looping_PortfolioBenchmarkTests"/> (while
    /// builds a sequence, forEach classifies + folds it via a nested ifElse), sized down so a run
    /// of several thousand executions stays fast enough for every build.</summary>
    private const string Script = """
        [
          { "command": "add", "path": "$.stepsCsv", "value": "" },
          { "command": "add", "path": "$.counter", "value": 0 },
          { "command": "while",
            "condition": "=lessOrEqual($.counter, $.stepCount)",
            "maxIterations": 1000,
            "commands": [
              { "command": "set", "path": "$.stepsCsv",
                "value": "=concat($.stepsCsv, =if(=equals($.stepsCsv,''),'',','), $.counter)" },
              { "command": "set", "path": "$.counter", "value": "=sum($.counter, 1)" }
            ] },
          { "command": "add", "path": "$.steps", "value": "=split($.stepsCsv, ',')" },

          { "command": "add", "path": "$.state", "value": {} },
          { "command": "add", "path": "$.state.running", "value": 0 },
          { "command": "add", "path": "$.currentStep", "value": "" },

          { "command": "forEach", "path": "$.steps", "commands": [
              { "command": "set", "path": "$.currentStep", "value": "=fetch(=scriptpath())" },
              { "command": "ifElse",
                "condition": "=equals($.currentStep, '0')",
                "ifScript": [
                  { "command": "set", "path": "@",
                    "value": { "step": "=fetch($.currentStep)", "kind": "first", "value": 0 } }
                ],
                "elseScript": [
                  { "command": "set", "path": "@",
                    "value": { "step": "=fetch($.currentStep)", "kind": "rest",
                               "value": "=sum($.state.running, $.currentStep)" } },
                  { "command": "set", "path": "$.state.running",
                    "value": "=sum($.state.running, $.currentStep)" }
                ]
              }
          ] },

          { "command": "add", "path": "$.summary", "value": {} },
          { "command": "add", "path": "$.summary.count", "value": "=count($.steps)" },
          { "command": "remove", "path": "$.stepsCsv" },
          { "command": "remove", "path": "$.counter" },
          { "command": "remove", "path": "$.steps" },
          { "command": "remove", "path": "$.state" },
          { "command": "remove", "path": "$.currentStep" }
        ]
        """;

    private const int StepCount = 10;
    private const int WarmupIterations = 500;
    private const int BatchSize = 5_000;
    private const int BatchCount = 5;

    // Conservative on purpose (same "2x measured value" margin the other performance tests in
    // this folder use): a real leak that retains data proportionally to executions would push
    // this ratio toward BatchCount (5x here), not hover near 1.0.
    private const double MaxLastVsFirstBatchGrowthRatio = 2.0;

    private static ScriptEngine<JToken> CreateEngine()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        options.FunctionsProvider.RegisterText<JToken>();
        options.CommandsProvider.RegisterLooping<JToken>();
        return new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private static JToken BuildDocument(int index) =>
        new JObject { ["stepCount"] = StepCount, ["seed"] = index };

    private static void RunBatch(CompiledScript<JToken> script, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var context = JsonExecutionContext.CreateDefault();
            var result = script.Execute(BuildDocument(i), context);
            if (!result.Success)
            {
                var reasons = string.Join("; ", context.GetLogEntries().Select(e => $"{e.Level}: {e.Message}"));
                throw new InvalidOperationException($"Execution {i} failed: {reasons}");
            }
        }
    }

    [Test]
    public void RepeatedExecution_SharedCompiledScript_HeapStaysBounded()
    {
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var engine = CreateEngine();
        var script = engine.Compile(Script, adapter);

        // Warmup: pay for JIT/tiering and let the GC settle into steady state before measuring.
        RunBatch(script, WarmupIterations);
        ForceGc();

        var heapAfterBatch = new long[BatchCount];
        for (var b = 0; b < BatchCount; b++)
        {
            RunBatch(script, BatchSize);
            ForceGc();
            heapAfterBatch[b] = GC.GetTotalMemory(false);
            TestContext.WriteLine(
                $"batch {b + 1}/{BatchCount} ({BatchSize:N0} executions, {(b + 1) * BatchSize:N0} total): " +
                $"heap={heapAfterBatch[b]:N0} bytes");
        }

        var firstBatchHeap = heapAfterBatch[0];
        var lastBatchHeap = heapAfterBatch[^1];
        var growthRatio = (double)lastBatchHeap / firstBatchHeap;

        TestContext.WriteLine(
            $"heap after batch 1: {firstBatchHeap:N0} B | after batch {BatchCount}: {lastBatchHeap:N0} B | " +
            $"ratio={growthRatio:F2}x (threshold: {MaxLastVsFirstBatchGrowthRatio:F1}x)");

        Assert.That(growthRatio, Is.LessThan(MaxLastVsFirstBatchGrowthRatio),
            $"Managed heap grew {growthRatio:F2}x from batch 1 ({firstBatchHeap:N0} B) to batch {BatchCount} " +
            $"({lastBatchHeap:N0} B) — {BatchSize * (BatchCount - 1):N0} more executions apart, measured right " +
            "after a forced full GC each time. That shape (heap scaling with execution count rather than " +
            "flattening out) is what a leak looks like, not GC noise.");
    }

    private static void ForceGc()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }
}
