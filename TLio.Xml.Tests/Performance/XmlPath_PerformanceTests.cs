using System.Diagnostics;
using System.Xml.Linq;
using NUnit.Framework;
using TLio.Xml;

namespace TLio.Xml.Tests.Performance;

/// <summary>
/// GC-allocation baseline tests for SlashPathItemsFetcher against XElement documents.
/// Uses the same warmup + ForceGc + measure pattern as CompiledScript_PerformanceTests.
/// Thresholds are conservative (set to 2x measured value on a developer machine).
/// </summary>
[TestFixture]
public class XmlPath_PerformanceTests
{
    private const long MaxAllocBytesPerIteration = 131_072;
    private const long MaxLargeDocAllocBytes = 50_000_000;
    private const int  MaxBatchElapsedMs     = 2_000;
    private const int  WarmupIterations      = 5;
    private const int  MeasuredIterations    = 1_000;

    private SlashPathItemsFetcher _fetcher = null!;
    private XElement _doc = null!;
    private XElement _largeDoc = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fetcher = new SlashPathItemsFetcher();

        // Medium document: 100 <item> children
        var items = new XElement("items",
            Enumerable.Range(0, 100).Select(i =>
                new XElement("item",
                    new XElement("id", i),
                    new XElement("name", $"item-{i}"),
                    new XElement("value", i * 1.5))));
        _doc = new XElement("root", items);

        // Large document: ~200 fields × 500 rows
        var largeRoot = new XElement("data",
            Enumerable.Range(0, 500).Select(row =>
                new XElement("row",
                    Enumerable.Range(0, 20).Select(col =>
                        new XElement($"field{col}", $"value-{row}-{col}")))));
        _largeDoc = largeRoot;
    }

    [Test]
    public void XPathQuery_1000Iterations_AllocBelowThreshold()
    {
        // Warmup
        for (var w = 0; w < WarmupIterations; w++)
            _ = _fetcher.SelectNodes("/items/item", _doc).ToList();

        ForceGc();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredIterations; i++)
            _ = _fetcher.SelectNodes("/items/item", _doc).ToList();
        long after = GC.GetAllocatedBytesForCurrentThread();

        long perIteration = (after - before) / MeasuredIterations;
        TestContext.WriteLine($"XmlPath/iter: {perIteration:N0} bytes  (threshold: {MaxAllocBytesPerIteration:N0})");
        Assert.That(perIteration, Is.LessThanOrEqualTo(MaxAllocBytesPerIteration),
            $"Allocation per iteration ({perIteration:N0} B) exceeded threshold ({MaxAllocBytesPerIteration:N0} B).");
    }

    [Test]
    public void DeepPath_1000Iterations_AllocBelowThreshold()
    {
        for (var w = 0; w < WarmupIterations; w++)
            _ = _fetcher.SelectNodes("/items/item/name", _doc).ToList();

        ForceGc();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredIterations; i++)
            _ = _fetcher.SelectNodes("/items/item/name", _doc).ToList();
        long after = GC.GetAllocatedBytesForCurrentThread();

        long perIteration = (after - before) / MeasuredIterations;
        TestContext.WriteLine($"XmlDeepPath/iter: {perIteration:N0} bytes  (threshold: {MaxAllocBytesPerIteration:N0})");
        Assert.That(perIteration, Is.LessThanOrEqualTo(MaxAllocBytesPerIteration));
    }

    [Test]
    public void LargeDocument_ParseAndQuery_CompletesWithinThresholds()
    {
        // Warmup
        _ = _fetcher.SelectNodes("/data/row/field0", _largeDoc).ToList();

        ForceGc();

        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        _ = _fetcher.SelectNodes("/data/row/field0", _largeDoc).ToList();
        sw.Stop();
        long totalBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.WriteLine($"LargeDoc query: {totalBytes:N0} bytes, {sw.ElapsedMilliseconds} ms");
        Assert.That(totalBytes, Is.LessThanOrEqualTo(MaxLargeDocAllocBytes));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThanOrEqualTo(MaxBatchElapsedMs));
    }

    private static void ForceGc()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }
}
