# Tasks: Expand Test Coverage with Performance Tests

**Input**: Design documents from `specs/015-expand-test-coverage/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to
- All paths are relative to the repo root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify the build is clean and subdirectory structure is ready before adding test files.

- [X] T001 Verify `dotnet build` passes cleanly with zero errors and zero warnings across all six test projects
- [X] T002 [P] Create folder `TLio.Xml.Tests/Adapters/` (will hold XmlNodeAdapterTests.cs)
- [X] T003 [P] Create folder `TLio.Xml.Tests/Fetchers/` (will hold SlashPathItemsFetcherTests.cs)
- [X] T004 [P] Create folder `TLio.Xml.Tests/EdgeCases/` (will hold XmlMalformedInputTests.cs)
- [X] T005 [P] Create folder `TLio.Xml.Tests/Performance/` (will hold XmlPath_PerformanceTests.cs)
- [X] T006 [P] Create folder `TLio.Yaml.Tests/Adapters/` (will hold YamlNodeAdapterTests.cs)
- [X] T007 [P] Create folder `TLio.Yaml.Tests/Fetchers/` (will hold YamlPathItemsFetcherTests.cs)
- [X] T008 [P] Create folder `TLio.Yaml.Tests/EdgeCases/` (will hold YamlMalformedInputTests.cs)
- [X] T009 [P] Create folder `TLio.Yaml.Tests/Performance/` (will hold YamlPath_PerformanceTests.cs)
- [X] T010 [P] Create folder `TLio.Json.SystemText.Tests/Adapters/` (will hold SystemTextJsonNodeAdapterTests.cs)
- [X] T011 [P] Create folder `TLio.Json.SystemText.Tests/EdgeCases/` (will hold SystemTextJsonEdgeCaseTests.cs)
- [X] T012 [P] Create folder `TLio.Json.Tests/EdgeCases/` (will hold JsonEdgeCaseTests.cs)
- [X] T013 [P] Create folder `TLio.UnitTests/Performance/` (will hold CommandEngine_PerformanceTests.cs)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: No shared infrastructure tasks needed — all test projects already exist and are independently buildable.

**⚠️ CRITICAL**: Phase 1 must complete (build is green) before Phase 3+ work begins.

**Checkpoint**: Build verified clean → all user story phases can proceed in parallel.

---

## Phase 3: User Story 1 — Adapter and Fetcher Unit Tests (Priority: P1) 🎯 MVP

**Goal**: Every adapter and fetcher class currently lacking a dedicated unit test suite gets one, covering happy-path, missing-key, null-value, and boundary scenarios.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~Adapters|FullyQualifiedName~Fetchers"` — all new tests in this phase pass, and no existing test regresses.

### Implementation for User Story 1

- [X] T014 [P] [US1] Create `TLio.Xml.Tests/Adapters/XmlNodeAdapterTests.cs` — namespace `TLio.Xml.Tests.Adapters`. Add `[TestFixture]` class `XmlNodeAdapterTests` that directly instantiates `XmlNodeAdapter` (from `TLio.Xml`) and includes ≥10 `[Test]` or `[TestCase]` methods covering: (1) `IsObject` returns true for an element with child elements, (2) `IsArray` returns true for a repeated-sibling collection, (3) `IsValue` returns true for a text node, (4) `GetStringValue` returns the element's text content, (5) `GetBoolValue` parses "true"/"false" text nodes, (6) `GetIntValue` parses a numeric text node, (7) `GetProperties` returns all child element names, (8) `GetProperty` returns the correct child by name, (9) `GetProperty` returns null/sentinel for a non-existent child, (10) `DeepClone` produces a structurally equal but reference-distinct node. Each test builds its input via `XElement.Parse(...)`. Use `Assert.That(...)` constraint model throughout.
- [X] T015 [P] [US1] Create `TLio.Xml.Tests/Fetchers/SlashPathItemsFetcherTests.cs` — namespace `TLio.Xml.Tests.Fetchers`. Add `[TestFixture]` class `SlashPathItemsFetcherTests`. Instantiate `SlashPathItemsFetcher` (from `TLio.Xml`) using its public constructor. Include ≥8 `[TestCase]` methods covering: (1) root `/` returns the document root, (2) single-segment `/address` returns matching child, (3) multi-segment `/address/city` returns deep child, (4) `/items/item` where multiple `<item>` siblings exist returns all matching nodes, (5) path to a non-existent element returns an empty collection (not null), (6) path `/` on an empty `<root/>` element returns the root itself, (7) path with a numeric index segment (if supported), (8) path with a wildcard `*` segment (if supported, skip if not). Each test builds a simple `XElement` tree inline.
- [X] T016 [P] [US1] Create `TLio.Yaml.Tests/Adapters/YamlNodeAdapterTests.cs` — namespace `TLio.Yaml.Tests.Adapters`. Add `[TestFixture]` class `YamlNodeAdapterTests` that directly instantiates `YamlNodeAdapter` (from `TLio.Yaml`). Include ≥10 `[Test]` or `[TestCase]` methods covering: (1) `IsObject` returns true for a YAML mapping node, (2) `IsArray` returns true for a YAML sequence node, (3) `IsValue` returns true for a YAML scalar node, (4) `GetStringValue` returns the scalar's string value, (5) `GetBoolValue` parses `true`/`false` YAML scalars, (6) `GetIntValue` parses an integer scalar, (7) `GetProperties` returns all key names for a mapping, (8) `GetProperty` returns the correct child mapping by key, (9) `GetProperty` returns null/sentinel for a missing key, (10) `DeepClone` produces a structurally equal but reference-distinct node. Build test YAML via `new YamlMappingNode(...)` / `new YamlScalarNode(...)` from `YamlDotNet.RepresentationModel`.
- [X] T017 [P] [US1] Create `TLio.Yaml.Tests/Fetchers/YamlPathItemsFetcherTests.cs` — namespace `TLio.Yaml.Tests.Fetchers`. Add `[TestFixture]` class `YamlPathItemsFetcherTests`. Instantiate `YamlPathItemsFetcher` (from `TLio.Yaml`) using its public constructor or via `YamlExecutionContext.Create()`. Include ≥8 `[TestCase]` methods covering: (1) single-key path `"address"` returns the mapping value, (2) dot-notation path `"address.city"` returns a deep scalar, (3) array-index path `"items[0]"` (or equivalent syntax) returns the first sequence element, (4) path that references a non-existent key returns an empty collection, (5) path on an empty YAML document returns an empty collection, (6) path to a nested mapping returns the sub-mapping node, (7) path on a sequence root with index `[1]` returns the second element, (8) path with a key containing a special character (if escaping is supported). Build test YAML inline using `YamlDotNet` node constructors.
- [X] T018 [P] [US1] Create `TLio.Json.SystemText.Tests/Adapters/SystemTextJsonNodeAdapterTests.cs` — namespace `TLio.Json.SystemText.Tests.Adapters`. Add `[TestFixture]` class `SystemTextJsonNodeAdapterTests` that directly instantiates `SystemTextJsonNodeAdapter` (from `TLio.Json.SystemText`). Include ≥10 `[Test]` or `[TestCase]` methods covering: (1) `IsObject` returns true for a `JsonObject`, (2) `IsArray` returns true for a `JsonArray`, (3) `IsValue` returns true for a `JsonValue`, (4) `GetStringValue` returns the string from a string `JsonValue`, (5) `GetBoolValue` returns the bool from a boolean `JsonValue`, (6) `GetIntValue` returns the integer from a numeric `JsonValue`, (7) `GetProperties` returns all property names from a `JsonObject`, (8) `GetProperty` returns the correct child by name, (9) `GetProperty` returns null/sentinel for a missing key, (10) `DeepClone` produces a structurally equal but reference-distinct `JsonNode`. Build inputs via `JsonNode.Parse(...)` or inline `new JsonObject { ... }`.

**Checkpoint**: User Story 1 complete — run `dotnet test` targeting the five new test files. All ≥46 new test cases pass.

---

## Phase 4: User Story 2 — Edge Case and Error Handling Tests (Priority: P2)

**Goal**: All adapters surface predictable, typed errors for null, empty, and malformed inputs — no silent `NullReferenceException` leaks.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~EdgeCases|FullyQualifiedName~Malformed"` — all new edge-case tests pass.

### Implementation for User Story 2

- [X] T019 [P] [US2] Create `TLio.Xml.Tests/EdgeCases/XmlMalformedInputTests.cs` — namespace `TLio.Xml.Tests.EdgeCases`. Add `[TestFixture]` class `XmlMalformedInputTests`. Include tests covering: (1) passing a completely invalid XML string (e.g., `"not xml"`) to `XmlNodeAdapter.Parse(string)` — expect an `XmlException` or equivalent typed exception, not a `NullReferenceException`; (2) passing an empty string `""` to `XmlNodeAdapter.Parse(string)` — expect a typed parse exception; (3) passing a well-formed but empty document `<root/>` to `SlashPathItemsFetcher` with a deep path — expect an empty result collection; (4) passing a `null` string to `XmlNodeAdapter.Parse(string)` — expect `ArgumentNullException`; (5) calling `GetStringValue` on a non-existent property (null node) — expect a defined sentinel or typed exception, not `NullReferenceException`. Use `Assert.Throws<T>(...)` for exception assertions.
- [X] T020 [P] [US2] Create `TLio.Yaml.Tests/EdgeCases/YamlMalformedInputTests.cs` — namespace `TLio.Yaml.Tests.EdgeCases`. Add `[TestFixture]` class `YamlMalformedInputTests`. Include tests covering: (1) passing a string with invalid YAML indentation to `YamlNodeAdapter.Parse(string)` — expect a `YamlException` or typed parse exception; (2) passing an empty string `""` to `YamlNodeAdapter.Parse(string)` — expect empty result or typed exception (document the actual behaviour); (3) passing YAML with a tab character where spaces are required — expect a typed parse exception; (4) passing `null` to `YamlNodeAdapter.Parse(string)` — expect `ArgumentNullException`; (5) calling `YamlPathItemsFetcher` with a `null` path string — expect `ArgumentNullException` or typed exception.
- [X] T021 [P] [US2] Create `TLio.Json.SystemText.Tests/EdgeCases/SystemTextJsonEdgeCaseTests.cs` — namespace `TLio.Json.SystemText.Tests.EdgeCases`. Add `[TestFixture]` class `SystemTextJsonEdgeCaseTests`. Include tests covering: (1) `SystemTextJsonNodeAdapter.Parse(string)` with invalid JSON — expect a `JsonException`; (2) `Parse("")` with empty string — expect a typed exception; (3) `Parse("null")` returns a `JsonValue` representing JSON null (not a C# null); (4) `Parse("{}")` returns a `JsonObject` with zero properties; (5) evaluating a JSONPath expression `$.nonexistent` via `SystemTextJsonPathItemsFetcher` returns an empty enumerable (not null); (6) passing a `null` path string to `SystemTextJsonPathItemsFetcher` — expect `ArgumentNullException` or `ArgumentException`; (7) calling `GetStringValue` on a `JsonArray` node — expect a typed exception or defined sentinel, not `NullReferenceException`.
- [X] T022 [P] [US2] Create `TLio.Json.Tests/EdgeCases/JsonEdgeCaseTests.cs` — namespace `TLio.Json.Tests.EdgeCases`. Add `[TestFixture]` class `JsonEdgeCaseTests`. Include tests covering: (1) `JsonNodeAdapter.Parse(string)` with invalid JSON — expect `JsonReaderException` from Newtonsoft; (2) `Parse("")` — expect a typed exception; (3) `Parse("null")` returns a `JValue` of null type; (4) `Parse("{}")` returns a `JObject` with zero properties; (5) a JSONPath expression `$.nonexistent` via `JsonPathItemsFetcher` returns an empty enumerable; (6) `null` path to `JsonPathItemsFetcher` — expect `ArgumentNullException`; (7) `GetStringValue` on a `JArray` node — expect a typed exception or defined sentinel.

**Checkpoint**: User Story 2 complete — run `dotnet test` targeting the four new edge-case files. All tests pass.

---

## Phase 5: User Story 3 — Performance Baseline Tests (Priority: P3)

**Goal**: Every adapter path and the command engine has at least one GC-allocation or wall-clock performance baseline test with a committed threshold constant.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~PerformanceTests"` — all performance tests pass on the developer machine.

### Implementation for User Story 3

- [X] T023 [P] [US3] Create `TLio.Xml.Tests/Performance/XmlPath_PerformanceTests.cs` — namespace `TLio.Xml.Tests.Performance`. Add `[TestFixture]` class `XmlPath_PerformanceTests`. Follow the exact pattern in `TLio.Json.SystemText.Tests/PerformanceTests/CompiledScript_PerformanceTests.cs`: (a) define a `private static void ForceGc()` helper that calls `GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true)` twice with `GC.WaitForPendingFinalizers()` between; (b) in `[OneTimeSetUp]` build a reusable `XElement` tree of ≥100 child elements; (c) add test `XPathQuery_1000Iterations_AllocBelowThreshold` — 5 warmup iterations, `ForceGc()`, then measure `GC.GetAllocatedBytesForCurrentThread()` across 1 000 iterations of `SlashPathItemsFetcher.SelectNodes("/root/item", element)`, assert `perIterationBytes <= MaxAllocBytesPerIteration`; define `private const long MaxAllocBytesPerIteration = 8192;` (set conservatively — adjust to 2× actual measured value after first run). Add `TestContext.WriteLine(...)` to log actual vs threshold.
- [X] T024 [P] [US3] Create `TLio.Yaml.Tests/Performance/YamlPath_PerformanceTests.cs` — namespace `TLio.Yaml.Tests.Performance`. Mirror the structure of T023 but for YAML: (a) `ForceGc()` helper; (b) `[OneTimeSetUp]` builds a `YamlMappingNode` with ≥50 key-value pairs; (c) test `YamlPathQuery_1000Iterations_AllocBelowThreshold` — warmup 5×, `ForceGc()`, measure 1 000 iterations of `YamlPathItemsFetcher.SelectNodes("key", document)`, assert `perIterationBytes <= MaxAllocBytesPerIteration`; use `private const long MaxAllocBytesPerIteration = 8192;` as initial conservative value.
- [X] T025 [P] [US3] Create `TLio.Json.SystemText.Tests/Performance/JsonPath_PerformanceTests.cs` — namespace `TLio.Json.SystemText.Tests.PerformanceTests` (match existing namespace). Add `[TestFixture]` class `JsonPath_PerformanceTests` (distinct from existing `CompiledScript_PerformanceTests`). (a) `ForceGc()` helper; (b) `[OneTimeSetUp]` builds a `JsonObject` with 100 properties via `JsonNode.Parse(...)`; (c) test `JsonPathQuery_1000Iterations_AllocBelowThreshold` — 5 warmup iterations using `SystemTextJsonPathItemsFetcher.SelectNodes("$.prop50", document)`, then `ForceGc()`, then measure 1 000 iterations, assert `perIterationBytes <= MaxAllocBytesPerIteration`; `private const long MaxAllocBytesPerIteration = 16384;`; (d) test `LargeDocument_Parse_AllocBelowLimit` — generate a JSON string of ≥1 MB (array of 50 000 simple objects) in `[OneTimeSetUp]`, parse once after warmup, measure GC allocation for a single `JsonNode.Parse(largeJson)` call, assert `totalBytes <= MaxLargeDocAllocBytes`; `private const long MaxLargeDocAllocBytes = 20_000_000;`.
- [X] T026 [US3] Create `TLio.UnitTests/Performance/CommandEngine_PerformanceTests.cs` — namespace `TLio.UnitTests.Performance`. Add `[TestFixture]` class `CommandEngine_PerformanceTests`. (a) `ForceGc()` helper; (b) `[OneTimeSetUp]` sets up a `ScriptEngine<JToken>` using `JsonExecutionContext.CreateDefault()` and prepares a simple but valid TLio script JSON string (e.g., a `set` command: `{"command":"set","path":"$.x","value":{"value":"hello"}}`); (c) test `BatchExecution_500Commands_CompletesWithinThreshold` — 5 warmup executions, then start a `Stopwatch`, execute the script 500 times each against `JObject.Parse("{}")` with a fresh `JsonExecutionContext.CreateDefault()` context, stop the stopwatch, assert `sw.ElapsedMilliseconds <= MaxBatchElapsedMs`; `private const int MaxBatchElapsedMs = 2000;`; log actual ms via `TestContext.WriteLine(...)`. Requires `using Newtonsoft.Json.Linq;`, `using TLio.Client;`, `using TLio.Json;`, `using System.Diagnostics;`, `using NUnit.Framework;`.

**Checkpoint**: User Story 3 complete — run `dotnet test --filter "FullyQualifiedName~PerformanceTests"` — all 5+ new performance tests pass.

---

## Phase 6: User Story 4 — TimeDate and ETL Function Coverage (Priority: P4)

**Goal**: TimeDate and ETL functions have ≥20 and ≥15 dedicated test cases respectively in `TLio.Functions.Tests`.

**Independent Test**: Run `dotnet test --project TLio.Functions.Tests` — all new TimeDate and ETL tests pass.

### Implementation for User Story 4

- [X] T027 [US4] Create `TLio.Functions.Tests/FunctionsTests/TimeDateTests/TimeDate_ExtendedTests.cs` — namespace `TLio.Functions.Tests.FunctionsTests.TimeDateTests`. Add `[TestFixture]` class `TimeDate_ExtendedTests`. Look at the 4 existing files in `TLio.Functions.Tests/FunctionsTests/TimeDateTests/` (AvgDateTests.cs, DateCompareTests.cs, IsDateBetweenTests.cs, MinMaxDateTests.cs) and `TLio.Functions.Tests/FunctionsTests/DatetimeFunctionTests.cs` to understand which TLio functions are exercised and which inputs are missing. Then add ≥20 distinct `[TestCase]` or `[Test]` methods that are NOT already covered, selecting from these gaps: (1) `formatDate` with an invalid date string — expect graceful failure / sentinel; (2) `formatDate` on a leap-year date (2024-02-29); (3) `formatDate` with a timezone offset argument; (4) `addDays` adding zero days returns the same date; (5) `addDays` adding negative days; (6) `addMonths` crossing a year boundary; (7) `addMonths` from January 31 to February (truncation behaviour); (8) `dateDiff` in days between two same-day dates → 0; (9) `dateDiff` in months; (10) `dateDiff` with swapped from/to dates (negative result or abs); (11) `toDate` parsing ISO 8601 format; (12) `toDate` parsing a custom format string; (13) `toDate` parsing an invalid string → sentinel/error; (14) `now` function returns a non-null date; (15) `dateYear` / `dateMonth` / `dateDay` extraction; (16) `dateHour` / `dateMinute` extraction; (17) date comparison via `isDateBefore` / `isDateAfter`; (18) `isDateBetween` with boundary dates (inclusive/exclusive edge); (19) `minDate` / `maxDate` from a two-element array; (20) `avgDate` for a single-element array equals that element. Use `JsonExecutionContext.CreateDefault()` and `JObject.Parse(...)` following the pattern in existing DatetimeFunctionTests.cs.
- [X] T028 [US4] Create `TLio.Functions.Tests/FunctionsTests/ETLTests/ETL_Tests.cs` — namespace `TLio.Functions.Tests.FunctionsTests.ETLTests`. Add `[TestFixture]` class `ETL_Tests`. Register the ETL extension pack following the pattern in `TLio.UnitTests/EtlFixtureTests.cs` (use `ParseOptions<JToken>.CreateDefault()` + `options.CommandsProvider.RegisterETL<JToken>()`). Add ≥15 distinct `[TestCase]` or `[Test]` methods covering the ETL commands: (1) `flatten` on a nested object → flat key-value array; (2) `flatten` on a deeply nested 3-level object; (3) `flatten` on an already-flat object → no change; (4) `flatten` on an object containing an array; (5) `restore` on a previously flattened result reproduces the original object; (6) `restore` with a missing value key; (7) `toCsv` on an array of objects with identical keys → CSV header + rows; (8) `toCsv` on an empty array → header only; (9) `toCsv` on a single-row array; (10) `toCsv` with a field that contains a comma (quoting behaviour); (11) `resolve` replaces a reference with its target value; (12) `resolve` on a missing reference → sentinel; (13) `flatten` on a null input → graceful failure; (14) `toCsv` on an array of objects where one row has a missing field (null/empty cell); (15) `restore` on a non-flat input → returns input unchanged or throws typed exception. Use `JsonExecutionContext.CreateDefault()` and build input `JToken` inline. Note: `TLio.Functions.Tests.csproj` may need a `<ProjectReference>` to `TLio.Extensions.ETL` — add it if missing.

**Checkpoint**: User Story 4 complete — run `dotnet test --project TLio.Functions.Tests` — ≥20 TimeDate and ≥15 ETL test cases pass.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final verification, threshold calibration, and build hygiene.

- [X] T029 Run `dotnet test` across all projects and confirm total passing test count ≥ 650 (was ~547)
- [X] T030 [P] Calibrate each `MaxAllocBytesPerIteration` constant in the four new performance test files: run each test once with `TestContext.WriteLine` output, capture actual allocation, set the constant to 2× the measured value, commit
- [X] T031 [P] Verify `dotnet build` produces zero warnings in all six test projects (fix any CS warnings introduced by new files)
- [X] T032 Run `dotnet test` one final time — confirm zero failures, zero skips, and that the five original performance tests in `TLio.Json.SystemText.Tests/PerformanceTests/CompiledScript_PerformanceTests.cs` still pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Build verified clean — GATES all user story phases
- **User Stories (Phases 3–6)**: All depend on Phase 1 clean build; phases 3–6 are **mutually independent** — can proceed in parallel
- **Polish (Phase 7)**: Depends on all four user story phases completing

### User Story Dependencies

- **US1 (P1)**: No dependencies on other user stories — start after Phase 1
- **US2 (P2)**: No dependencies on US1 — can run in parallel with US1
- **US3 (P3)**: No dependencies on US1/US2 — can run in parallel
- **US4 (P4)**: No dependencies on other user stories — can run in parallel

### Within Each User Story

- All tasks within US1 are fully parallel (T014–T018 target different files)
- All tasks within US2 are fully parallel (T019–T022 target different files)
- US3 tasks T023–T025 are parallel; T026 is independent of them
- US4 tasks T027–T028 are parallel

### Parallel Opportunities

```text
Phase 1:  T002–T013 all parallel (creating different directories)
Phase 3:  T014, T015, T016, T017, T018 — all parallel
Phase 4:  T019, T020, T021, T022 — all parallel
Phase 5:  T023, T024, T025 parallel; T026 independent
Phase 6:  T027, T028 parallel
Phase 7:  T030, T031 parallel; T029 first; T032 last
```

---

## Parallel Example: User Story 1

```text
# All five adapter/fetcher test files can be written simultaneously:
T014: TLio.Xml.Tests/Adapters/XmlNodeAdapterTests.cs
T015: TLio.Xml.Tests/Fetchers/SlashPathItemsFetcherTests.cs
T016: TLio.Yaml.Tests/Adapters/YamlNodeAdapterTests.cs
T017: TLio.Yaml.Tests/Fetchers/YamlPathItemsFetcherTests.cs
T018: TLio.Json.SystemText.Tests/Adapters/SystemTextJsonNodeAdapterTests.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: verify build + create folders
2. Complete Phase 3: 5 new adapter/fetcher test files, ≥46 test cases
3. **STOP and VALIDATE**: `dotnet test` — all pass, zero regressions
4. Deliver: every adapter now has a dedicated unit test class

### Incremental Delivery

1. Phase 1 → Phase 3 (US1) → validate → **MVP**
2. Add Phase 4 (US2 edge cases) → validate
3. Add Phase 5 (US3 performance) → calibrate thresholds → validate
4. Add Phase 6 (US4 TimeDate/ETL) → validate
5. Phase 7 polish → final `dotnet test` → done

### Parallel Team Strategy

With multiple contributors:
1. All complete Phase 1 together (5 minutes)
2. Once Phase 1 done:
   - Dev A: US1 (T014–T018) — adapter/fetcher unit tests
   - Dev B: US2 (T019–T022) — edge cases
   - Dev C: US3 (T023–T026) — performance
   - Dev D: US4 (T027–T028) — TimeDate/ETL
3. All stories merge independently; Phase 7 polish runs last

---

## Notes

- `[P]` tasks target different files — no merge conflicts when run in parallel
- All new tests use NUnit 4.x: `[TestFixture]`, `[Test]`, `[TestCase]`, `Assert.That(...)` constraint model
- Performance constants (`MaxAllocBytesPerIteration`, `MaxBatchElapsedMs`) are intentionally conservative — set to 2× actual measured value to avoid CI flakiness
- No new NuGet packages required — all dependencies already present in the respective test project `.csproj` files
- Exception: `TLio.Functions.Tests.csproj` may need `<ProjectReference Include="../TLio.Extensions.ETL/TLio.Extensions.ETL.csproj" />` for T028 — verify before implementing T028
- Constitution compliance: since this feature adds only test files, Articles I–V and IX–XI do not apply; Article VI (fixture triplets) applies only if any task introduces full-script execution tests, which none of T014–T028 do (all are unit-level)
