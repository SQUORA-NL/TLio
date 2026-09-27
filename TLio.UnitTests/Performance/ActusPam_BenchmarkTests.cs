using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Looping;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.UnitTests.Performance;

/// <summary>
/// Wall-clock throughput of the ACTUS PAM contract script from
/// TLio-Samples' <c>TLio.Sample.Actus.Api</c> (<c>Fixtures/Actus/Scripts/pam-simple.json</c> here
/// is a checked-in copy — the sample itself moved to the TLio-Samples repository, so this can no
/// longer link the file directly) run over a portfolio of contracts — one
/// <see cref="CompiledScript{TNode}.Execute"/> per contract against a fresh
/// <see cref="IExecutionContext{TNode}"/>, exactly the shape that sample's own <c>Program.cs</c>
/// uses per HTTP request and a batch valuation job would use per contract. The script is compiled
/// once and reused across the whole portfolio, as that sample's own host does.
///
/// This is a benchmark, not a regression gate — [Explicit] because a 100k/1M-contract run takes
/// real wall-clock minutes and has no "fast enough" pass/fail line, unlike the threshold-based
/// tests elsewhere in this folder. Run manually to get numbers for this machine:
///
///   DOTNET_gcServer=1 dotnet test TLio.UnitTests -c Release --filter "FullyQualifiedName~ActusPam_BenchmarkTests"
///
/// (Release matters: Debug JIT/tiering makes the larger portfolios several times slower.
/// DOTNET_gcServer=1 matters for <see cref="Portfolio_Throughput_Parallel"/> specifically: this
/// workload allocates a full JSON document tree per contract, and Workstation GC — the default
/// for a console/test host — measured roughly half the throughput Server GC does here, since
/// Server GC gives each core its own heap instead of coordinating collections through one.)
///
/// <see cref="Portfolio_Throughput_Parallel"/>'s own remarks cover a second real scalability bug
/// this benchmark surfaced (a process-wide lock shared across every concurrent execution). Even
/// with that fixed and Server GC on, parallel throughput still capped around 650-750% CPU of the
/// 1400% available (Apple M4 Pro, 14 cores) — profiling with dotnet-trace (sampled managed
/// stacks) found why: <c>JsonNodeAdapter.TryGetDouble</c>
/// (<c>TLio.Json/JsonNodeAdapter.cs</c>) used a bare <c>try { v.Value&lt;double?&gt;() } catch { return null; }</c>
/// to test whether a value was numeric — and every non-numeric comparison a script makes (every
/// date, every id, every empty-string check — the overwhelming majority in a typical script)
/// threw and caught a <c>FormatException</c> to find out. Cheap on one thread (a ~2.9x single
/// thread number was still being reported as "the ceiling" before this was found), but .NET's
/// exception path takes an internal runtime lock formatting the exception's own message
/// (<c>Monitor.Enter_Slowpath</c> reached through <c>ResourceManager.GetFirstResourceSet</c>),
/// visible directly in the trace, repeated thousands of times across threads. Fixed by testing
/// the <c>JTokenType</c> first and using <c>double.TryParse</c> for strings — no exception on the
/// hot path at all. Confirmed against a synthetic control (a hand-written Newtonsoft.Json
/// workload doing the same allocation and JSONPath-lookup shape, without TLio's interpreter) that
/// reached ~1250% CPU with no code changes, proving the ceiling was never this machine's
/// hardware or .NET's ThreadPool — both fully capable of near-linear scaling — but this one
/// exception-heavy method. After the fix, parallel CPU usage reaches ~1200-1220%, matching that
/// control's ceiling, and both single-threaded and parallel throughput below improved by roughly
/// 3x and 6x respectively over the numbers first reported.
/// </summary>
[TestFixture]
[Explicit("Benchmark, not a CI gate - run manually for portfolio throughput numbers.")]
public class ActusPam_BenchmarkTests
{
    private static readonly string ScriptPath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Actus", "Scripts", "pam-simple.json");

    private static ScriptEngine<JToken> CreateEngine()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        options.FunctionsProvider.RegisterText<JToken>();
        options.FunctionsProvider.RegisterTimeDate<JToken>();
        options.CommandsProvider.RegisterLooping<JToken>();
        return new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    /// <summary>
    /// A 10-year loan with a quarterly interest-payment cycle -> 41 schedule dates (IED, 39 IP,
    /// MD), representative of a real PAM instrument rather than a 1-2 payment toy contract.
    /// Terms vary slightly per index so the portfolio isn't 1,000,000 copies of one value.
    /// </summary>
    private static JToken BuildContract(int index) =>
        new JObject
        {
            ["contractId"] = $"PAM{index:D7}",
            ["contractRole"] = index % 2 == 0 ? "RPA" : "RPL",
            ["currency"] = "EUR",
            ["notionalPrincipal"] = 10_000 + index % 90_000,
            ["nominalInterestRate"] = 0.01 + index % 50 / 1000.0,
            ["dayCountConvention"] = "A360",
            ["initialExchangeDate"] = "2020-01-01",
            ["maturityDate"] = "2030-01-01",
            ["interestPaymentCycle"] = new JObject { ["count"] = 3, ["unit"] = "months" },
        };

    private static (long ElapsedMs, long EventCount) RunPortfolio(CompiledScript<JToken> script, int contractCount)
    {
        var eventTotal = 0L;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < contractCount; i++)
        {
            var context = JsonExecutionContext.CreateDefault();
            var result = script.Execute(BuildContract(i), context);
            if (!result.Success)
            {
                var reasons = string.Join("; ", context.GetLogEntries().Select(e => $"{e.Level}: {e.Message}"));
                throw new InvalidOperationException($"Contract {i} failed to execute: {reasons}");
            }
            eventTotal += result.Data.SelectToken("$.summary.eventCount")!.Value<int>();
        }
        sw.Stop();
        return (sw.ElapsedMilliseconds, eventTotal);
    }

    [TestCase(100)]
    [TestCase(10_000)]
    [TestCase(100_000)]
    [TestCase(1_000_000)]
    public void Portfolio_Throughput(int contractCount)
    {
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var engine = CreateEngine();
        var script = engine.Compile(File.ReadAllText(ScriptPath), adapter);

        // Warmup: pays for JIT/tiering once, outside the timed run, same as the other
        // performance tests in this folder.
        RunPortfolio(script, System.Math.Min(500, contractCount));

        var (elapsedMs, eventTotal) = RunPortfolio(script, contractCount);
        var perContractUs = elapsedMs * 1000.0 / contractCount;
        var contractsPerSecond = elapsedMs == 0 ? double.PositiveInfinity : contractCount / (elapsedMs / 1000.0);

        TestContext.WriteLine(
            $"portfolio={contractCount:N0} contracts | total={elapsedMs:N0} ms | " +
            $"per-contract={perContractUs:F2} us | throughput={contractsPerSecond:N0} contracts/sec | " +
            $"events={eventTotal:N0}");

        Assert.That(eventTotal, Is.EqualTo(41L * contractCount), "each contract's schedule should be IED + 39 IP + MD = 41 events");
    }

    /// <summary>
    /// Same script, same portfolio shape, but contracts are independent so nothing stops running
    /// them across all cores: the script is compiled exactly once — one shared
    /// <see cref="CompiledScript{JToken}"/>, exactly as <c>Program.cs</c> holds one per endpoint
    /// for the life of the process — and every contract calls
    /// <see cref="CompiledScript{TNode}.Execute"/> on that same shared instance concurrently, each
    /// with its own fresh <see cref="IExecutionContext{TNode}"/> and input <see cref="JToken"/>.
    ///
    /// This only became safe after fixing a real bug this benchmark surfaced: <c>Execute</c> is
    /// documented as safe to call concurrently ("each call returns an independent instance that
    /// owns its own mutable execution state"), but <c>CommandBase&lt;TNode&gt;.Clone()</c> was a
    /// shallow <c>MemberwiseClone()</c>, so a nested block's own <c>TLioScript&lt;TNode&gt;</c>
    /// (<c>While.Commands</c>, <c>ForEach.Commands</c>, <c>ifElse</c>'s
    /// <c>ifScript</c>/<c>elseScript</c>) was a shared reference, not copied, across every
    /// "independent" clone <see cref="CompiledScript{TNode}.CreateExecutable"/> produced — two
    /// threads executing the same compiled script concurrently ran the *same* inner command
    /// instances for any nested block at the same time, racing on
    /// <c>CommandBase</c>'s own per-execution success flag. Reproduced by this exact test before
    /// the fix (intermittent corruption inside the <c>while</c> schedule loop — a
    /// <c>dayCountFraction</c> call failing on an empty date that a single-threaded run never
    /// produces). Fixed by making <c>While</c>/<c>ForEach</c>/<c>IfElse</c> override
    /// <c>Clone()</c> to also deep-clone their nested script(s) via the new
    /// <c>TLioScript&lt;TNode&gt;.Clone()</c>, so no command instance is ever shared across two
    /// executions again — see <c>CommandBase.cs</c>, <c>TLioScript.cs</c>, and the three commands'
    /// own <c>Clone()</c> overrides.
    ///
    /// Two more bugs this same benchmark surfaced, once the above no longer corrupted results —
    /// both about parallel *throughput*, not correctness: a process-wide lock in
    /// <c>JsonPathItemsFetcher</c>'s array-index cache (fixed by scoping it to the
    /// per-execution fetcher instance instead of a process-wide static field), and an
    /// exception-per-comparison bug in <c>JsonNodeAdapter.TryGetDouble</c> that turned out to be
    /// the dominant one — see the class remarks above for both.
    /// </summary>
    [TestCase(100)]
    [TestCase(10_000)]
    [TestCase(100_000)]
    [TestCase(1_000_000)]
    public void Portfolio_Throughput_Parallel(int contractCount)
    {
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;
        var engine = CreateEngine();
        var script = engine.Compile(File.ReadAllText(ScriptPath), adapter);

        // Warmup: pays for JIT/tiering once, outside the timed parallel run.
        RunPortfolio(script, System.Math.Min(500, contractCount));

        // ParallelOptions.MaxDegreeOfParallelism is a ceiling, not a guarantee: the ThreadPool
        // otherwise injects worker threads gradually ("hill-climbing") rather than starting a
        // short-lived, CPU-bound run with as many as it will ever need. Doesn't fully explain this
        // machine's own ceiling (see the class remarks) but is cheap, correct, and removes it as a
        // variable.
        ThreadPool.SetMinThreads(Environment.ProcessorCount, Environment.ProcessorCount);

        var eventTotal = 0L;
        var sw = Stopwatch.StartNew();
        Parallel.For(0, contractCount,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            i =>
            {
                var context = JsonExecutionContext.CreateDefault();
                var result = script.Execute(BuildContract(i), context);
                if (!result.Success)
                {
                    var reasons = string.Join("; ", context.GetLogEntries().Select(e => $"{e.Level}: {e.Message}"));
                    throw new InvalidOperationException($"Contract {i} failed to execute: {reasons}");
                }
                Interlocked.Add(ref eventTotal, result.Data.SelectToken("$.summary.eventCount")!.Value<int>());
            });
        sw.Stop();

        var perContractUs = sw.ElapsedMilliseconds * 1000.0 / contractCount;
        var contractsPerSecond = sw.ElapsedMilliseconds == 0
            ? double.PositiveInfinity
            : contractCount / (sw.ElapsedMilliseconds / 1000.0);

        TestContext.WriteLine(
            $"portfolio={contractCount:N0} contracts | cores={Environment.ProcessorCount} | " +
            $"total={sw.ElapsedMilliseconds:N0} ms | per-contract={perContractUs:F2} us | " +
            $"throughput={contractsPerSecond:N0} contracts/sec | events={eventTotal:N0}");

        Assert.That(eventTotal, Is.EqualTo(41L * contractCount), "each contract's schedule should be IED + 39 IP + MD = 41 events");
    }
}
