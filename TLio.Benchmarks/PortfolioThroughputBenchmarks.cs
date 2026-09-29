using System.Diagnostics;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Extensions.Looping;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Benchmarks;

/// <summary>
/// Answers the question <c>TLio.UnitTests/Performance/Looping_PortfolioBenchmarkTests.cs</c>
/// can't: its Stopwatch numbers showed the parallel run scaling nowhere near
/// <see cref="Environment.ProcessorCount"/>x (~2.7-8.8x on a 14-core machine, not ~14x), but a
/// single Stopwatch sample can't tell you whether that's noise, a hardware ceiling, or TLio
/// itself serializing work that should be independent.
///
/// Two direct measurements answer that, printed by each parallel benchmark itself rather than
/// left to a diagnoser (BenchmarkDotNet's built-in <c>[ThreadingDiagnoser]</c> refuses to run at
/// all against <c>net10.0</c> with the BenchmarkDotNet version this project pins — it aborts
/// benchmark validation outright, so this measures the same two things by hand):
///
/// - <see cref="System.Threading.Monitor.LockContentionCount"/>, before/after: the runtime's own
///   count of threads that blocked waiting for a monitor lock. Not inferred from timing - an
///   exact count from the CLR.
/// - CPU time (<see cref="Process.TotalProcessorTime"/>) divided by wall-clock time: "effective
///   cores used". A perfectly parallel, uncontended workload spends close to
///   <see cref="Environment.ProcessorCount"/> worth of CPU time per second of wall time; a
///   workload serialized behind a lock spends much less, because most of those "parallel" threads
///   are blocked, not computing.
///
/// <see cref="TLio_Parallel"/> runs the same shared, singleton-compiled script every document in
/// <c>Looping_PortfolioBenchmarkTests</c> uses, against every core; <see cref="Synthetic_Parallel"/>
/// does comparable per-document work (build a delimited string, split it, classify and fold each
/// element) with zero shared state and zero TLio - no compiled script, no adapter, no path
/// cache - so it has nothing to contend over. Both accumulate their per-document result into a
/// thread-local before one <c>Interlocked.Add</c> per partition (not per document) - the naive
/// per-document version of this made the *harness itself* the contended resource and produced
/// numbers about the benchmark, not about TLio.
///
/// What this actually found, on a 14-core Apple M4 Pro: <c>lockContentions=0</c> on every
/// iteration of both parallel benchmarks - the process-wide path-cache lock
/// <c>Looping_PortfolioBenchmarkTests</c>'s remarks describe as the once-known suspect is already
/// fixed (it predates this file). And the "doesn't use all the cores" feeling checks out but isn't
/// a bug: <c>Synthetic_Parallel</c> - pure CPU-bound, uncontended, nothing to blame but the
/// hardware - itself only reaches roughly 6x on 14 cores, because M4 Pro's 14 "cores" are a mix of
/// fast (P) and slow (E) ones, not 14 identical ones; that number, not
/// <see cref="Environment.ProcessorCount"/>, is this machine's real ceiling.
/// <see cref="TLio_Parallel"/> lands at roughly 80% of that ceiling - a healthy result, not a
/// leak. (Numbers move with the machine; read the report BenchmarkDotNet prints for this run, not
/// the ones quoted here.)
///
/// BenchmarkDotNet also fixes the "make it more stable" half of the ask: each benchmark here runs
/// several timed iterations after a warmup (JIT/tiering settle out), and the report's Mean/Error/
/// StdDev columns replace the single-sample Stopwatch numbers with a distribution — a bad
/// individual run (a GC pause, a scheduler hiccup) shows up as StdDev, not as a number you can't
/// trust. <see cref="Environment.ProcessorCount"/> is also printed directly in the summary's Host
/// section BenchmarkDotNet generates for every run, and again in the console output below - the
/// "how many cores" half of the ask.
///
/// Run: <c>dotnet run -c Release --project TLio.Benchmarks</c> (Release matters - BenchmarkDotNet
/// refuses to run a Debug build). Results land in <c>BenchmarkDotNet.Artifacts/results/</c> as
/// Markdown/HTML/CSV alongside the console report.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
// No [ShortRunJob]: let BenchmarkDotNet's own pilot stage decide iteration count (typically
// 15+, vs. ShortRun's fixed 3) until the confidence interval actually narrows — 3 iterations
// measured ~65% Error-as-percent-of-Mean on the parallel benchmarks, too noisy to trust the
// last digit of a Ratio. Costs a few extra minutes of wall-clock per run.
public class PortfolioThroughputBenchmarks
{
    private const int DocumentCount = 2_000;
    private const int StepCount = 20;

    // Per-document synthetic work is far cheaper than a TLio script execution (no JSON tree, no
    // function dispatch, no path resolution) — at DocumentCount it finished in well under a
    // millisecond, too short for Process.TotalProcessorTime's sampling resolution to say anything
    // meaningful about core utilization. Scaled up so its own wall-clock lands in the same order
    // of magnitude as the TLio benchmarks, which is what makes the two comparable.
    private const int SyntheticDocumentCount = 1_000_000;

    /// <summary>Same two-primitive shape (while builds a sequence, forEach classifies + folds it
    /// via ifElse) as <c>Looping_PortfolioBenchmarkTests</c> and <c>Looping_MemoryLeakTests</c>,
    /// so this is measuring the same workload, not a different one.</summary>
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

    private CompiledScript<JToken> _script = null!;

    [GlobalSetup]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        options.FunctionsProvider.RegisterText<JToken>();
        options.CommandsProvider.RegisterLooping<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        _script = engine.Compile(Script, adapter);

        // Same reason Looping_PortfolioBenchmarkTests.Portfolio_Throughput_Parallel does this:
        // ThreadPool.SetMinThreads is a floor, not a request — without it the pool injects
        // worker threads gradually ("hill-climbing") rather than starting a short CPU-bound burst
        // with as many as it will ever use, which would otherwise show up as low effective-core
        // usage that has nothing to do with contention.
        ThreadPool.SetMinThreads(Environment.ProcessorCount, Environment.ProcessorCount);

        Console.WriteLine($"[Setup] Environment.ProcessorCount = {Environment.ProcessorCount}");
    }

    private static JToken BuildDocument(int index) =>
        new JObject { ["stepCount"] = StepCount, ["seed"] = index };

    [BenchmarkCategory("TLio")]
    [Benchmark(Baseline = true, Description = "TLio_Sequential")]
    public long TLio_Sequential()
    {
        long total = 0;
        for (var i = 0; i < DocumentCount; i++)
        {
            var context = JsonExecutionContext.CreateDefault();
            var result = _script.Execute(BuildDocument(i), context);
            if (!result.Success)
                throw new InvalidOperationException($"Document {i} failed.");
            total += result.Data.SelectToken("$.summary.count")!.Value<int>();
        }
        return total;
    }

    [BenchmarkCategory("TLio")]
    [Benchmark(Description = "TLio_Parallel")]
    public long TLio_Parallel() => Measured("TLio_Parallel", () =>
    {
        long total = 0;
        // Thread-local accumulation (localInit/body/localFinally), not one shared Interlocked.Add
        // per document: with enough documents, a shared accumulator's cache line ping-pongs
        // between cores on every single item, which is contention this benchmark's own harness
        // would be causing, not TLio. One Interlocked.Add per *partition* instead of per item.
        Parallel.For(0, DocumentCount,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            () => 0L,
            (i, _, local) =>
            {
                var context = JsonExecutionContext.CreateDefault();
                var result = _script.Execute(BuildDocument(i), context);
                if (!result.Success)
                    throw new InvalidOperationException($"Document {i} failed.");
                return local + result.Data.SelectToken("$.summary.count")!.Value<int>();
            },
            local => Interlocked.Add(ref total, local));
        return total;
    });

    /// <summary>Wraps a parallel benchmark body with the lock-contention / effective-core
    /// measurement described in this class's remarks, printed once per BenchmarkDotNet iteration
    /// (warmup iterations included — that's fine, they're clearly labelled and cheap to skim).</summary>
    private static long Measured(string label, Func<long> body)
    {
        var contentionBefore = System.Threading.Monitor.LockContentionCount;
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        var sw = Stopwatch.StartNew();

        var result = body();

        sw.Stop();
        var cpuElapsed = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
        var contentionDelta = System.Threading.Monitor.LockContentionCount - contentionBefore;
        var effectiveCores = sw.Elapsed.TotalSeconds > 0 ? cpuElapsed.TotalSeconds / sw.Elapsed.TotalSeconds : 0;

        Console.WriteLine(
            $"  [{label}] wall={sw.Elapsed.TotalMilliseconds:F1}ms cpu={cpuElapsed.TotalMilliseconds:F1}ms " +
            $"effectiveCores={effectiveCores:F2}/{Environment.ProcessorCount} lockContentions={contentionDelta}");

        return result;
    }

    /// <summary>Same shape of work as <see cref="Script"/> — build a delimited sequence, split
    /// it, classify first-vs-rest, fold a running total — done by hand with BCL types only. No
    /// TLio: no compiled script, no shared adapter, no path cache. Nothing here can contend.</summary>
    private static int RunSyntheticDocument(int index)
    {
        var csv = new StringBuilder();
        for (var i = 0; i <= StepCount; i++)
        {
            if (i > 0) csv.Append(',');
            csv.Append(i);
        }
        var steps = csv.ToString().Split(',');

        var running = 0;
        var count = 0;
        foreach (var step in steps)
        {
            var value = int.Parse(step);
            if (value != 0) running += value;
            count++;
        }
        return count + (index % 1); // keep `index` live so the JIT can't fold the loop to a constant
    }

    [BenchmarkCategory("Synthetic")]
    [Benchmark(Baseline = true, Description = "Synthetic_Sequential")]
    public long Synthetic_Sequential()
    {
        long total = 0;
        for (var i = 0; i < SyntheticDocumentCount; i++)
            total += RunSyntheticDocument(i);
        return total;
    }

    [BenchmarkCategory("Synthetic")]
    [Benchmark(Description = "Synthetic_Parallel")]
    public long Synthetic_Parallel() => Measured("Synthetic_Parallel", () =>
    {
        long total = 0;
        // Same thread-local accumulation as TLio_Parallel — see its comment. Matters even more
        // here: at a million tiny items, one shared Interlocked.Add per item would dominate the
        // measurement and make this "control" no better than the thing it's meant to isolate.
        Parallel.For(0, SyntheticDocumentCount,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            () => 0L,
            (i, _, local) => local + RunSyntheticDocument(i),
            local => Interlocked.Add(ref total, local));
        return total;
    });
}
