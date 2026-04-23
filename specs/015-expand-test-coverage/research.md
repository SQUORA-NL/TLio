# Research: Expand Test Coverage with Performance Tests

**Branch**: `015-expand-test-coverage` | **Date**: 2026-04-23

## Findings

### Decision: Test framework — NUnit (no change)
**Rationale**: All 6 existing test projects already use NUnit. Staying consistent avoids setup overhead and keeps `dotnet test` as the single run command.  
**Alternatives considered**: xUnit (rejected — would require migrating existing tests or mixing frameworks), MSTest (rejected — same reason).

---

### Decision: Performance measurement — GC allocation + Stopwatch (no BenchmarkDotNet)
**Rationale**: `CompiledScript_PerformanceTests.cs` in `TLio.Json.SystemText.Tests` already uses `GC.GetAllocatedBytesForCurrentThread()` with a manual warmup pass. Adopting the same pattern keeps all performance tests consistent and requires no new package references.  
**Alternatives considered**: BenchmarkDotNet (rejected — adds a dependency, requires a separate runner binary, and is heavyweight for in-suite regression guards). Stopwatch-only (insufficient — wall-clock is noisy; GC allocation is deterministic and a better regression signal for micro-benchmarks).

---

### Decision: Large-document generation — programmatic in TestFixtureSetUp (no committed binaries)
**Rationale**: The spec requires "at least 1 MB" documents for large-document performance tests. Generating them in `[OneTimeSetUp]` keeps the repo lean and ensures reproducibility without committing large binary/text assets.  
**Alternatives considered**: Committed fixture files (rejected — binary blobs inflate repo size and are hard to review in PRs). External file download (rejected — creates CI network dependency).

---

### Decision: Adapter/fetcher unit tests use direct instantiation (not fixture triplets)
**Rationale**: Article VI's fixture-triplet requirement applies to "full script execution" tests. Unit tests that directly exercise `INodeAdapter<TNode>` or `IItemsFetcher<TNode>` methods are "validation edge cases" and may use inline `[TestCase]` data. Article II's footnote explicitly exempts test helpers that `new` up adapters.  
**Alternatives considered**: Fixture triplets for all adapter tests (rejected — unnecessarily heavyweight for unit-level contract checks; the triplet format is designed for script-level tests).

---

### Decision: Performance thresholds — committed constants, established empirically
**Rationale**: Thresholds must be stable across normal developer hardware but are not required to hold on severely resource-constrained agents (per spec Assumptions). Constants are set conservatively (e.g., 2× the measured allocation on a dev machine) to avoid flakiness.  
**Pattern**: Each performance test file defines a `private const long MaxAllocBytesPerIteration = <value>;` or `private const int MaxElapsedMs = <value>;` near the top of the class.

---

### Existing performance test pattern (reference)

```csharp
// From CompiledScript_PerformanceTests.cs
[Test]
public void SomePerformance_Test()
{
    // Warmup
    for (int i = 0; i < 5; i++) RunOnce();

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1000; i++) RunOnce();
    long after = GC.GetAllocatedBytesForCurrentThread();

    long perIteration = (after - before) / 1000;
    Assert.That(perIteration, Is.LessThanOrEqualTo(MaxAllocBytesPerIteration));
}
```

All new performance tests MUST follow this pattern (warmup → GC.Collect → measure → assert).
