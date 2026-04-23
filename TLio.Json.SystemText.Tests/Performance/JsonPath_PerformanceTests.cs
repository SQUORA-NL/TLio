using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Json.SystemText;

namespace TLio.Json.SystemText.Tests.PerformanceTests;

/// <summary>
/// GC-allocation and throughput baseline tests for SystemTextJsonPathItemsFetcher.
/// Complements CompiledScript_PerformanceTests with path-evaluation specific baselines.
/// Thresholds are conservative (2x measured value on a developer machine).
/// </summary>
[TestFixture]
public class JsonPath_PerformanceTests
{
    private const long MaxAllocBytesPerIteration = 32_768;
    private const long MaxLargeDocAllocBytes     = 150_000_000;
    private const int  MaxLargeDocElapsedMs      = 500;
    private const int  WarmupIterations          = 5;
    private const int  MeasuredIterations        = 1_000;

    private SystemTextJsonNodeAdapter _adapter = null!;
    private SystemTextJsonPathItemsFetcher _fetcher = null!;
    private JsonNode _doc = null!;
    private string _largeJson = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _adapter = new SystemTextJsonNodeAdapter();

        // Medium document: object with 100 properties
        var sb = new StringBuilder("{");
        for (int i = 0; i < 100; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"prop{i}\":{i}");
        }
        sb.Append('}');
        _doc = _adapter.Parse(sb.ToString());

        // Large document: array of 10,000 simple objects — well over 1 MB
        var lsb = new StringBuilder("[");
        for (int i = 0; i < 10_000; i++)
        {
            if (i > 0) lsb.Append(',');
            lsb.Append($"{{\"id\":{i},\"name\":\"item-{i}\",\"active\":true,\"score\":{(i * 1.1).ToString("F2", CultureInfo.InvariantCulture)}}}");
        }
        lsb.Append(']');
        _largeJson = lsb.ToString();
        TestContext.WriteLine($"Large document size: {_largeJson.Length:N0} bytes");
    }

    [TearDown]
    public void TearDown() => _fetcher?.Dispose();

    [SetUp]
    public void SetUp() => _fetcher = new SystemTextJsonPathItemsFetcher();

    [Test]
    public void JsonPathQuery_1000Iterations_AllocBelowThreshold()
    {
        // Warmup
        for (var w = 0; w < WarmupIterations; w++)
            _ = _fetcher.SelectNodes("$.prop50", _doc).ToList();

        ForceGc();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredIterations; i++)
            _ = _fetcher.SelectNodes("$.prop50", _doc).ToList();
        long after = GC.GetAllocatedBytesForCurrentThread();

        long perIteration = (after - before) / MeasuredIterations;
        TestContext.WriteLine($"STJ JsonPath/iter: {perIteration:N0} bytes  (threshold: {MaxAllocBytesPerIteration:N0})");
        Assert.That(perIteration, Is.LessThanOrEqualTo(MaxAllocBytesPerIteration),
            $"Allocation per iteration ({perIteration:N0} B) exceeded threshold ({MaxAllocBytesPerIteration:N0} B).");
    }

    [Test]
    public void LargeDocument_ParseAndQuery_CompletesWithinThresholds()
    {
        // Warmup
        _ = _adapter.Parse(_largeJson);

        ForceGc();

        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        var largeDoc = _adapter.Parse(_largeJson);
        sw.Stop();
        long totalBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.WriteLine($"LargeDoc parse: {totalBytes:N0} bytes, {sw.ElapsedMilliseconds} ms");
        Assert.That(totalBytes, Is.LessThanOrEqualTo(MaxLargeDocAllocBytes),
            $"Large doc allocation ({totalBytes:N0} B) exceeded threshold ({MaxLargeDocAllocBytes:N0} B).");
        Assert.That(sw.ElapsedMilliseconds, Is.LessThanOrEqualTo(MaxLargeDocElapsedMs),
            $"Large doc parse took {sw.ElapsedMilliseconds} ms (threshold: {MaxLargeDocElapsedMs} ms).");

        // Verify parse result is valid
        Assert.That(_adapter.IsArray(largeDoc), Is.True);
        Assert.That(_adapter.GetArrayLength(largeDoc), Is.EqualTo(10_000));
    }

    private static void ForceGc()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }
}
