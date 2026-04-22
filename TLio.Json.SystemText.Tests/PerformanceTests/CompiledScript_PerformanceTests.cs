using System.Diagnostics;
using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Json.SystemText;
using TLio.Json.SystemText.Tests.Fixtures;

namespace TLio.Json.SystemText.Tests.PerformanceTests;

/// <summary>
/// Allocation-based performance assertions for CompiledScript (SC-006, SC-007).
/// Tests run single-threaded with GC collection before each measurement.
/// A JIT warmup pass is executed before every measurement to exclude first-call overhead.
/// </summary>
[TestFixture]
public class CompiledScript_PerformanceTests
{
    private ScriptEngine<JsonNode> _engine = null!;
    private SystemTextJsonNodeAdapter _adapter = null!;
    private CompiledScript<JsonNode> _compiled = null!;
    private string _inputJson = null!;
    private string _scriptJson = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fixture = FixtureTheoryLoader.LoadRaw("PerformanceScript").FirstOrDefault()
            ?? throw new InvalidOperationException("PerformanceScript fixture not found.");

        _inputJson  = (string)fixture.Arguments[0];
        _scriptJson = (string)fixture.Arguments[1];

        var options = ParseOptions<JsonNode>.CreateDefault();
        _engine   = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
        _adapter  = new SystemTextJsonNodeAdapter();
        _compiled = _engine.Compile(_scriptJson, _adapter);
    }

    private static ExecutionContext<JsonNode> Ctx() => SystemTextJsonExecutionContext.CreateDefault();

    // ── SC-006: Single execution ──────────────────────────────────────────────

    [Test]
    public void SingleExecution_CompiledAllocatesLessThanParseAndExecute()
    {
        // Warmup (exclude JIT overhead from measurement)
        _ = _engine.Execute(_scriptJson, _adapter.Parse(_inputJson), Ctx());
        _ = _compiled.Execute(_adapter.Parse(_inputJson), Ctx());

        ForceGc();

        long parseBefore = GC.GetAllocatedBytesForCurrentThread();
        _ = _engine.Execute(_scriptJson, _adapter.Parse(_inputJson), Ctx());
        long parseAlloc = GC.GetAllocatedBytesForCurrentThread() - parseBefore;

        ForceGc();

        long compiledBefore = GC.GetAllocatedBytesForCurrentThread();
        _ = _compiled.Execute(_adapter.Parse(_inputJson), Ctx());
        long compiledAlloc = GC.GetAllocatedBytesForCurrentThread() - compiledBefore;

        TestContext.WriteLine($"Parse+Execute: {parseAlloc:N0} bytes  |  Compiled: {compiledAlloc:N0} bytes");

        Assert.That(compiledAlloc, Is.LessThan(parseAlloc),
            $"Compiled path ({compiledAlloc:N0} B) should allocate less than parse-and-execute ({parseAlloc:N0} B). " +
            "The difference represents the eliminated script-parsing overhead.");
    }

    // ── SC-007: Script-initialization overhead, 1000 iterations ─────────────
    // Measures ONLY the script-setup cost (parse vs clone), not shared execution cost.
    // Input parsing is deliberately excluded — it is the same in both paths and would
    // obscure the specific optimization being validated.

    [Test]
    public void BatchScriptInit_1000Items_CloneAllocatesLessThan50PercentOfParseCost()
    {
        const int N = 1000;

        // Warmup
        for (var w = 0; w < 5; w++)
        {
            _ = _engine.Compile(_scriptJson, _adapter);
            _ = _compiled.CreateExecutable();
        }

        ForceGc();

        var sw = Stopwatch.StartNew();
        long parseBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < N; i++)
            _ = _engine.Compile(_scriptJson, _adapter);
        long parseTotal = GC.GetAllocatedBytesForCurrentThread() - parseBefore;
        long parseMs    = sw.ElapsedMilliseconds;

        ForceGc();

        sw.Restart();
        long cloneBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < N; i++)
            _ = _compiled.CreateExecutable();
        long cloneTotal = GC.GetAllocatedBytesForCurrentThread() - cloneBefore;
        long cloneMs    = sw.ElapsedMilliseconds;

        TestContext.WriteLine(
            $"Compile×{N}: {parseTotal:N0} bytes ({parseMs} ms)  |  " +
            $"Clone×{N}: {cloneTotal:N0} bytes ({cloneMs} ms)  |  " +
            $"Ratio: {(double)cloneTotal / parseTotal * 100:F1}%");

        Assert.That(cloneTotal, Is.LessThan(parseTotal * 0.5),
            $"Clone batch ({cloneTotal:N0} B) should be <50% of compile batch ({parseTotal:N0} B). " +
            "Creating an executable from a pre-compiled script should be significantly cheaper than re-parsing.");
    }

    private static void ForceGc()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }
}
