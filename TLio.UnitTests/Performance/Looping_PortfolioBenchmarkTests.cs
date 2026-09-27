using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Looping;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.Performance;

/// <summary>
/// Wall-clock throughput of a single compiled script — a <c>while</c> loop that builds a
/// sequence, followed by a <c>forEach</c> that classifies each element via a nested
/// <c>ifElse</c> (first / last / middle) and folds a running total — run over a portfolio of
/// documents, sequential and parallel. Structurally this is the shape of ACTUS PAM's own
/// schedule-then-fold script (TLio-Samples' <c>TLio.Sample.Actus.Api</c>, the sample this
/// benchmark was originally written against and later generalized away from): the two loop
/// primitives combined, a literal value written per branch, string equality comparisons, and
/// <c>@</c>/<c>scriptpath()</c> resolving the current element — but with none of ACTUS's own
/// domain vocabulary (dates, notionals, day-count conventions), since none of that is what this
/// exercises. Anyone benchmarking the real ACTUS PAM sample should do that in TLio-Samples
/// against its own script, not here.
///
/// This is a benchmark, not a regression gate — [Explicit] because a 100k/1M-document run takes
/// real wall-clock minutes and has no "fast enough" pass/fail line, unlike the threshold-based
/// tests in <c>Looping_PerformanceTests.cs</c>. Run manually to get numbers for this machine:
///
///   DOTNET_gcServer=1 dotnet test TLio.UnitTests -c Release --filter "FullyQualifiedName~Looping_PortfolioBenchmarkTests"
///
/// (Release matters: Debug JIT/tiering makes the larger portfolios several times slower.
/// DOTNET_gcServer=1 matters for <see cref="Portfolio_Throughput_Parallel"/> specifically: this
/// workload allocates a full JSON document tree per document, and Workstation GC — the default
/// for a console/test host — measured roughly half the throughput Server GC does here, since
/// Server GC gives each core its own heap instead of coordinating collections through one.)
///
/// <see cref="Portfolio_Throughput_Parallel"/>'s own remarks cover four real bugs this benchmark
/// (in its original, ACTUS-named form) found and fixed: two correctness bugs in concurrent
/// execution of a shared <see cref="CompiledScript{TNode}"/> (<c>CommandBase.Clone()</c>'s
/// shallow clone, <c>FixedValue</c>'s unshared-reference bug), and two throughput bugs (a
/// process-wide lock in <c>JsonPathItemsFetcher</c>'s path cache, and — the dominant one —
/// <c>JsonNodeAdapter.TryGetDouble</c> using exceptions as its "is this numeric" test, found via
/// dotnet-trace and confirmed against a synthetic control benchmark that reached ~1250% CPU on a
/// 14-core machine with no code changes at all, proving neither the hardware nor .NET's
/// ThreadPool was ever the bottleneck).
/// </summary>
[TestFixture]
[Explicit("Benchmark, not a CI gate - run manually for portfolio throughput numbers.")]
public class Looping_PortfolioBenchmarkTests
{
    /// <summary>
    /// A while loop builds a CSV of sequential indices ("0,1,2,...,stepCount") the same way
    /// ACTUS PAM's own script builds a CSV of schedule dates — string concatenation, no native
    /// "append" primitive — then a forEach classifies each one via a nested ifElse (first / last
    /// / middle), the same two-level nesting <c>ifElse</c> inside <c>forEach</c>'s body ACTUS PAM
    /// uses for IED / MD / IP. <c>=equals(...)</c> compares the *string* form of each step
    /// against <c>'0'</c> and against <c>=toString($.stepCount)</c> — the comparisons that made
    /// <c>TryGetDouble</c>'s exception-per-call bug matter, since neither side is ever numeric to
    /// that check.
    /// </summary>
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
                  { "command": "ifElse",
                    "condition": "=equals($.currentStep, =toString($.stepCount))",
                    "ifScript": [
                      { "command": "set", "path": "@",
                        "value": { "step": "=fetch($.currentStep)", "kind": "last",
                                   "value": "=sum($.state.running, $.currentStep)" } }
                    ],
                    "elseScript": [
                      { "command": "set", "path": "@",
                        "value": { "step": "=fetch($.currentStep)", "kind": "middle",
                                   "value": "=sum($.state.running, $.currentStep)" } },
                      { "command": "set", "path": "$.state.running",
                        "value": "=sum($.state.running, $.currentStep)" }
                    ]
                  }
                ]
              }
          ] },

          { "command": "add", "path": "$.summary", "value": {} },
          { "command": "add", "path": "$.summary.total", "value": "=sum($.steps[*].value)" },
          { "command": "add", "path": "$.summary.count", "value": "=count($.steps)" },
          { "command": "put", "path": "$.results", "value": "$.steps" },
          { "command": "remove", "path": "$.stepsCsv" },
          { "command": "remove", "path": "$.counter" },
          { "command": "remove", "path": "$.steps" },
          { "command": "remove", "path": "$.state" },
          { "command": "remove", "path": "$.currentStep" }
        ]
        """;

    /// <summary>41 elements per document (a step count of 40) — the same portfolio-element count
    /// ACTUS PAM's own 10-year, quarterly-payment contract produces (IED + 39 IP + MD).</summary>
    private const int StepCount = 40;

    private static ScriptEngine<JToken> CreateEngine()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        options.FunctionsProvider.RegisterText<JToken>();
        options.CommandsProvider.RegisterLooping<JToken>();
        return new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    /// <summary>Terms vary slightly per index so the portfolio isn't N copies of one value.</summary>
    private static JToken BuildDocument(int index) =>
        new JObject
        {
            ["documentId"] = $"DOC{index:D7}",
            ["stepCount"] = StepCount,
            ["seed"] = index % 90_000,
        };

    private static (long ElapsedMs, long ResultCount) RunPortfolio(CompiledScript<JToken> script, int documentCount)
    {
        var resultTotal = 0L;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < documentCount; i++)
        {
            var context = JsonExecutionContext.CreateDefault();
            var result = script.Execute(BuildDocument(i), context);
            if (!result.Success)
            {
                var reasons = string.Join("; ", context.GetLogEntries().Select(e => $"{e.Level}: {e.Message}"));
                throw new InvalidOperationException($"Document {i} failed to execute: {reasons}");
            }
            resultTotal += result.Data.SelectToken("$.summary.count")!.Value<int>();
        }
        sw.Stop();
        return (sw.ElapsedMilliseconds, resultTotal);
    }

    [TestCase(100)]
    [TestCase(10_000)]
    [TestCase(100_000)]
    [TestCase(1_000_000)]
    public void Portfolio_Throughput(int documentCount)
    {
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var engine = CreateEngine();
        var script = engine.Compile(Script, adapter);

        // Warmup: pays for JIT/tiering once, outside the timed run, same as the other
        // performance tests in this folder.
        RunPortfolio(script, System.Math.Min(500, documentCount));

        var (elapsedMs, resultTotal) = RunPortfolio(script, documentCount);
        var perDocumentUs = elapsedMs * 1000.0 / documentCount;
        var documentsPerSecond = elapsedMs == 0 ? double.PositiveInfinity : documentCount / (elapsedMs / 1000.0);

        TestContext.WriteLine(
            $"portfolio={documentCount:N0} documents | total={elapsedMs:N0} ms | " +
            $"per-document={perDocumentUs:F2} us | throughput={documentsPerSecond:N0} documents/sec | " +
            $"results={resultTotal:N0}");

        Assert.That(resultTotal, Is.EqualTo((long)(StepCount + 1) * documentCount),
            $"each document's sequence should be {StepCount + 1} elements (0..{StepCount})");
    }

    /// <summary>
    /// Same script, same portfolio shape, but documents are independent so nothing stops running
    /// them across all cores: the script is compiled exactly once — one shared
    /// <see cref="CompiledScript{JToken}"/>, the same singleton-compiled-script shape a real host
    /// holds for the life of the process — and every document calls
    /// <see cref="CompiledScript{TNode}.Execute"/> on that same shared instance concurrently, each
    /// with its own fresh <see cref="IExecutionContext{TNode}"/> and input <see cref="JToken"/>.
    ///
    /// This only became safe, and only became fast, after fixing the four bugs described in this
    /// class's own remarks — see there for what each one was and how it showed up here.
    /// </summary>
    [TestCase(100)]
    [TestCase(10_000)]
    [TestCase(100_000)]
    [TestCase(1_000_000)]
    public void Portfolio_Throughput_Parallel(int documentCount)
    {
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var engine = CreateEngine();
        var script = engine.Compile(Script, adapter);

        // Warmup: pays for JIT/tiering once, outside the timed parallel run.
        RunPortfolio(script, System.Math.Min(500, documentCount));

        // ParallelOptions.MaxDegreeOfParallelism is a ceiling, not a guarantee: the ThreadPool
        // otherwise injects worker threads gradually ("hill-climbing") rather than starting a
        // short-lived, CPU-bound run with as many as it will ever need.
        ThreadPool.SetMinThreads(Environment.ProcessorCount, Environment.ProcessorCount);

        var resultTotal = 0L;
        var sw = Stopwatch.StartNew();
        Parallel.For(0, documentCount,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            i =>
            {
                var context = JsonExecutionContext.CreateDefault();
                var result = script.Execute(BuildDocument(i), context);
                if (!result.Success)
                {
                    var reasons = string.Join("; ", context.GetLogEntries().Select(e => $"{e.Level}: {e.Message}"));
                    throw new InvalidOperationException($"Document {i} failed to execute: {reasons}");
                }
                Interlocked.Add(ref resultTotal, result.Data.SelectToken("$.summary.count")!.Value<int>());
            });
        sw.Stop();

        var perDocumentUs = sw.ElapsedMilliseconds * 1000.0 / documentCount;
        var documentsPerSecond = sw.ElapsedMilliseconds == 0
            ? double.PositiveInfinity
            : documentCount / (sw.ElapsedMilliseconds / 1000.0);

        TestContext.WriteLine(
            $"portfolio={documentCount:N0} documents | cores={Environment.ProcessorCount} | " +
            $"total={sw.ElapsedMilliseconds:N0} ms | per-document={perDocumentUs:F2} us | " +
            $"throughput={documentsPerSecond:N0} documents/sec | results={resultTotal:N0}");

        Assert.That(resultTotal, Is.EqualTo((long)(StepCount + 1) * documentCount),
            $"each document's sequence should be {StepCount + 1} elements (0..{StepCount})");
    }
}
