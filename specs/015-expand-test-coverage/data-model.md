# Data Model: Expand Test Coverage with Performance Tests

**Branch**: `015-expand-test-coverage` | **Date**: 2026-04-23

## Overview

This feature introduces no new production data models. The only "data" artefacts are test inputs, test constants, and performance threshold values. The entities below describe the conceptual model for the test layer.

---

## Test Entities

### PerformanceThreshold
A compile-time constant in a performance test class that defines the upper bound for a measured metric.

| Field | Type | Description |
|---|---|---|
| `MaxAllocBytesPerIteration` | `long const` | Maximum GC-allocated bytes per loop iteration across 1 000 iterations |
| `MaxElapsedMs` | `int const` | Maximum wall-clock milliseconds for a batch operation |
| `WarmupIterations` | `int const` | Number of warm-up iterations before measurement begins (typically 5) |
| `MeasuredIterations` | `int const` | Number of iterations in the measured window (typically 1 000) |

---

### LargeDocument
A programmatically generated document used by large-document performance tests.

| Field | Type | Description |
|---|---|---|
| `SizeBytes` | long | Target size — at least 1 048 576 bytes (1 MB) |
| `Format` | enum | `Json` / `Xml` / `Yaml` |
| `Structure` | string | Describes the schema shape (e.g., array of 10 000 objects with 5 scalar fields each) |

---

### AdapterTestCase (inline)
Represents one row in an inline `[TestCase]` for adapter unit tests.

| Field | Type | Description |
|---|---|---|
| `InputDocument` | string | Raw document content (small, inlined) |
| `Path` | string | Path expression to evaluate |
| `ExpectedValue` | object? | Expected scalar value or `null` for missing-node tests |
| `ExpectedException` | Type? | Exception type expected, or `null` if success expected |

---

## Fixture Triplet Layout (full-script tests only)

For any new test that exercises a complete TLio script execution, the fixture triplet lives in the test project that matches the adapter layer (Article VI):

```
<TestProject>/
  <Category>Tests/
    Fixtures/
      <ScenarioName>/
        input.json    ← starting document
        script.json   ← TLioScript to execute
        result.json   ← expected output
```

No new fixture triplets are mandated by this feature, but if a test author chooses to write a full-script test (e.g., to verify a complex edge case end-to-end), the triplet pattern MUST be followed.
