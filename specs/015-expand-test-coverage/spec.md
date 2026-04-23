# Feature Specification: Expand Test Coverage with Performance Tests

**Feature Branch**: `015-expand-test-coverage`  
**Created**: 2026-04-23  
**Status**: Draft  
**Input**: User description: "make now more cverage on the tests including more perfomance tests"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Adapter and Fetcher Unit Tests (Priority: P1)

A developer working on the XML or YAML adapters today has no dedicated unit test suite to validate adapter behaviour in isolation. They must rely entirely on fixture-based integration tests to catch regressions. This story adds focused unit tests for all adapters and path fetchers that currently lack them.

**Why this priority**: Direct unit tests for `XmlNodeAdapter`, `YamlNodeAdapter`, `SystemTextJsonNodeAdapter`, `SlashPathItemsFetcher`, and `YamlPathItemsFetcher` are the largest structural gap in the test suite. Without them, regressions in fundamental adapter contracts are caught only by accident through higher-level fixture tests.

**Independent Test**: Can be verified by running each new test class in isolation — each class covers a single adapter or fetcher and requires no other test infrastructure.

**Acceptance Scenarios**:

1. **Given** a `XmlNodeAdapter` test class, **When** it runs, **Then** it covers object/array/scalar node access, type coercion, and null-value handling with at least 10 distinct test cases.
2. **Given** a `YamlNodeAdapter` test class, **When** it runs, **Then** it covers scalar types, nested mappings, sequences, and missing-key behaviour with at least 10 distinct test cases.
3. **Given** a `SlashPathItemsFetcher` test class, **When** it runs, **Then** it validates path resolution, root-level access, deep traversal, and missing-node behaviour with at least 8 distinct test cases.
4. **Given** a `YamlPathItemsFetcher` test class, **When** it runs, **Then** it validates dot-notation traversal, array indexing, and missing-path handling with at least 8 distinct test cases.

---

### User Story 2 - Edge Case and Error Handling Tests (Priority: P2)

Developers lack confidence that adapters and the engine behave predictably when given malformed, empty, or boundary input. This story adds systematic edge-case tests covering null inputs, malformed documents, empty collections, and error propagation across all six adapter/test projects.

**Why this priority**: Edge-case coverage prevents silent corruption or uninformative exceptions from reaching production. It is higher priority than new performance tests because correctness defects are harder to diagnose than performance regressions.

**Independent Test**: Each edge-case test class can be run in isolation and must fail before the fix is applied, confirming the test genuinely exercises the boundary.

**Acceptance Scenarios**:

1. **Given** null or empty input to any adapter, **When** a node access is attempted, **Then** the system throws a well-typed exception or returns a defined sentinel value — no `NullReferenceException` leaks.
2. **Given** a malformed XML document, **When** passed to `XmlNodeAdapter`, **Then** a descriptive parse error is surfaced within 100 ms.
3. **Given** a malformed YAML document, **When** passed to `YamlNodeAdapter`, **Then** a descriptive parse error is surfaced within 100 ms.
4. **Given** a JSON path expression that matches zero nodes, **When** evaluated by either JSON fetcher, **Then** the system returns an empty enumerable (not null) and does not throw.

---

### User Story 3 - Performance Baseline Tests for All Adapters (Priority: P3)

Only the `CompiledScript` path in `TLio.Json.SystemText.Tests` has performance tests. All other adapters, fetchers, and the command engine have no performance baselines, making it impossible to detect regressions during code review. This story adds allocation and throughput tests for JSON, XML, and YAML paths plus the command engine.

**Why this priority**: Performance regressions are lower risk than correctness gaps but still important; they can silently degrade real-world usage. With a baseline in place, any future change that regresses throughput beyond an acceptable threshold is detectable.

**Independent Test**: Each performance test is runnable in isolation and produces a pass/fail result based on a defined threshold — no external infrastructure is required.

**Acceptance Scenarios**:

1. **Given** a warm JSON path evaluation loop of 1 000 iterations over a representative document, **When** the test runs, **Then** average GC allocation per iteration does not exceed a documented baseline constant committed in the test code.
2. **Given** a warm XML XPath evaluation loop of 1 000 iterations, **When** the test runs, **Then** average GC allocation per iteration does not exceed the documented baseline.
3. **Given** a warm YAML path evaluation loop of 1 000 iterations, **When** the test runs, **Then** average GC allocation per iteration does not exceed the documented baseline.
4. **Given** a batch of 500 command executions via the script engine, **When** the test runs, **Then** total elapsed wall-clock time does not exceed a documented threshold (e.g., 2 seconds).
5. **Given** a large document (at least 1 MB of JSON, XML, and YAML), **When** parsed and queried once, **Then** peak GC allocation is within a documented limit and the operation completes in under 500 ms.

---

### User Story 4 - TimeDate and ETL Function Coverage (Priority: P4)

The `TimeDate` and `ETL` function extension sets have the fewest test methods relative to their surface area. This story fills the gap with dedicated test cases covering common and boundary-value inputs.

**Why this priority**: Undocumented edge behaviours in date-time handling (timezones, leap years, invalid formats) and ETL transformations cause hard-to-diagnose data bugs, but these are lower priority than structural adapter and correctness gaps.

**Independent Test**: Each function test is standalone — no fixture infrastructure needed, input data is supplied inline.

**Acceptance Scenarios**:

1. **Given** the `TimeDate` extension functions, **When** tested across at least 20 distinct scenarios (valid dates, invalid strings, timezone offsets, leap-year boundaries), **Then** all pass without throwing unhandled exceptions.
2. **Given** the `ETL` extension functions, **When** exercised with at least 15 distinct input combinations including nulls and boundary values, **Then** output matches documented transformation behaviour.

---

### Edge Cases

- What happens when a path expression contains special characters (spaces, dots inside keys, Unicode)?
- How does the engine handle a document that is syntactically valid but semantically empty (e.g., a JSON `{}` or YAML `~`)?
- How does performance hold when the same script is cloned and executed concurrently by 10 threads?
- What happens when a performance test runs on a severely resource-constrained machine — should it be skipped or tolerate a wider threshold?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The test suite MUST include at least one dedicated unit test class for each of `XmlNodeAdapter`, `YamlNodeAdapter`, and `SystemTextJsonNodeAdapter`.
- **FR-002**: The test suite MUST include at least one dedicated unit test class for each of `SlashPathItemsFetcher` and `YamlPathItemsFetcher`.
- **FR-003**: Each new adapter/fetcher test class MUST cover: happy-path access, missing-key/node scenarios, null-value handling, and at least one boundary-value scenario.
- **FR-004**: The test suite MUST include edge-case tests for malformed input (invalid XML, invalid YAML, invalid JSON) in the appropriate test projects.
- **FR-005**: The test suite MUST include performance baseline tests (GC allocation and/or wall-clock) for JSON path evaluation, XML XPath evaluation, YAML path evaluation, and batch command execution.
- **FR-006**: Performance tests MUST define explicit pass/fail thresholds as constants in the test code so regressions are detectable without manual inspection.
- **FR-007**: Performance tests MUST include a warmup phase before measurement to avoid JIT bias, consistent with the existing pattern in the project.
- **FR-008**: The `TLio.Functions.Tests` project MUST contain at least 20 TimeDate test cases and at least 15 ETL test cases.
- **FR-009**: All new tests MUST integrate with the existing NUnit test runner and pass under `dotnet test` without additional tooling.
- **FR-010**: New test files MUST follow the naming and folder conventions already established in each test project (e.g., `{Subject}Tests.cs` in the relevant project folder).

### Key Entities

- **Adapter**: A component that wraps a document node (XML, JSON, YAML) and exposes a uniform access interface; each has its own test project.
- **Fetcher**: A component that resolves a path expression against an adapter to retrieve values.
- **Performance Baseline**: A numeric constant (bytes or milliseconds) committed to source and used as the upper-bound threshold in a performance test assertion.
- **Edge Case**: An input at or beyond the boundary of normal operation — null, empty, malformed, or oversized.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Total test method count increases from ~547 to at least 650 — a net addition of at least 100 new test cases.
- **SC-002**: Every adapter class (`XmlNodeAdapter`, `YamlNodeAdapter`, `SystemTextJsonNodeAdapter`) has at least 10 dedicated unit test cases that can be run independently.
- **SC-003**: Every fetcher class lacking tests (`SlashPathItemsFetcher`, `YamlPathItemsFetcher`) has at least 8 dedicated unit test cases.
- **SC-004**: At least 5 performance baseline tests exist across the adapter and engine layer, each with a defined, committed threshold constant.
- **SC-005**: All tests pass under `dotnet test` with zero failures and zero skips on the developer's machine.
- **SC-006**: No existing passing test is broken by the additions — zero regressions.
- **SC-007**: TimeDate function coverage reaches at least 20 test cases; ETL function coverage reaches at least 15 test cases.

## Assumptions

- The existing test infrastructure (NUnit, project references, fixture patterns) is retained unchanged; no new test frameworks are introduced.
- Performance thresholds are established empirically on a representative development machine and committed as constants — they are not required to pass on severely resource-constrained CI agents, but should be stable across normal developer hardware.
- BenchmarkDotNet is out of scope; the existing pattern of GC allocation measurement via `GC.GetAllocatedBytesForCurrentThread()` and wall-clock via `Stopwatch` is sufficient and consistent with the established approach.
- "Large document" for performance tests is defined as at least 1 MB of JSON/XML/YAML, generated programmatically in test setup rather than committed as a binary asset.
- Test additions target the six existing test projects; no new test project is created.
- Cross-adapter integration tests (exercising XML + YAML + JSON together in a single script) are out of scope for this feature.
