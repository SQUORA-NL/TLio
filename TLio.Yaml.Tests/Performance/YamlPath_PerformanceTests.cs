using System.Diagnostics;
using NUnit.Framework;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Performance;

/// <summary>
/// GC-allocation baseline tests for YamlPathItemsFetcher against YamlNode documents.
/// Thresholds are conservative (2x measured value on a developer machine).
/// </summary>
[TestFixture]
public class YamlPath_PerformanceTests
{
    private const long MaxAllocBytesPerIteration = 16_384;
    private const long MaxLargeDocAllocBytes     = 50_000_000;
    private const int  MaxBatchElapsedMs         = 2_000;
    private const int  WarmupIterations          = 5;
    private const int  MeasuredIterations        = 1_000;

    private YamlParentTracker _tracker = null!;
    private YamlPathItemsFetcher _fetcher = null!;
    private YamlMappingNode _doc = null!;
    private YamlMappingNode _largeDoc = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _tracker = new YamlParentTracker();
        _fetcher = new YamlPathItemsFetcher(_tracker);

        // Medium document: mapping with 50 named scalar entries
        _doc = new YamlMappingNode();
        for (int i = 0; i < 50; i++)
            _doc.Add(new YamlScalarNode($"key{i}"), new YamlScalarNode($"value{i}"));
        _doc.Add(new YamlScalarNode("target"), new YamlScalarNode("found"));

        // Large document: 200-entry mapping with nested values
        _largeDoc = new YamlMappingNode();
        for (int i = 0; i < 200; i++)
        {
            var nested = new YamlMappingNode
            {
                { new YamlScalarNode("id"), new YamlScalarNode(i.ToString()) },
                { new YamlScalarNode("name"), new YamlScalarNode($"item-{i}") }
            };
            _largeDoc.Add(new YamlScalarNode($"entry{i}"), nested);
        }
    }

    [Test]
    public void YamlPathQuery_1000Iterations_AllocBelowThreshold()
    {
        // Warmup
        for (var w = 0; w < WarmupIterations; w++)
            _ = _fetcher.SelectNodes("$.target", _doc).ToList();

        ForceGc();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredIterations; i++)
            _ = _fetcher.SelectNodes("$.target", _doc).ToList();
        long after = GC.GetAllocatedBytesForCurrentThread();

        long perIteration = (after - before) / MeasuredIterations;
        TestContext.WriteLine($"YamlPath/iter: {perIteration:N0} bytes  (threshold: {MaxAllocBytesPerIteration:N0})");
        Assert.That(perIteration, Is.LessThanOrEqualTo(MaxAllocBytesPerIteration),
            $"Allocation per iteration ({perIteration:N0} B) exceeded threshold ({MaxAllocBytesPerIteration:N0} B).");
    }

    [Test]
    public void DeepPath_1000Iterations_AllocBelowThreshold()
    {
        // Warmup
        for (var w = 0; w < WarmupIterations; w++)
            _ = _fetcher.SelectNodes("$.entry0.name", _largeDoc).ToList();

        ForceGc();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredIterations; i++)
            _ = _fetcher.SelectNodes("$.entry0.name", _largeDoc).ToList();
        long after = GC.GetAllocatedBytesForCurrentThread();

        long perIteration = (after - before) / MeasuredIterations;
        TestContext.WriteLine($"YamlDeepPath/iter: {perIteration:N0} bytes  (threshold: {MaxAllocBytesPerIteration:N0})");
        Assert.That(perIteration, Is.LessThanOrEqualTo(MaxAllocBytesPerIteration));
    }

    [Test]
    public void LargeDocument_Query_CompletesWithinThresholds()
    {
        // Warmup
        _ = _fetcher.SelectNodes("$.entry100.id", _largeDoc).ToList();

        ForceGc();

        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _ = _fetcher.SelectNodes("$.entry100.id", _largeDoc).ToList();
        sw.Stop();
        long totalBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        TestContext.WriteLine($"LargeDoc YAML query: {totalBytes:N0} bytes, {sw.ElapsedMilliseconds} ms");
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
