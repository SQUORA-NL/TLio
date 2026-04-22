# Tasks: Parse-Once Script Reuse and STJ Path Fetcher Optimization

**Input**: Design documents from `/specs/013-parse-once-stj-optimize/`  
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/api.md ✓

**Format**: `- [ ] [ID] [P?] [Story?] Description — file path`  
**[P]** = parallelizable (different files, no unresolved deps) | **[USn]** = user story label

---

## Phase 1: Setup

**Purpose**: Confirm baseline build passes before any changes.

- [x] T001 Confirm `dotnet build` succeeds for TLio.Core, TLio.Client, TLio.Json.SystemText, TLio.Json.SystemText.Tests (baseline before any edits)

---

## Phase 2: Foundational — Core Clone Support

**Purpose**: Minimal Core changes that unblock US1. US2 (STJ fetcher) can proceed in parallel from Phase 3 onward — it has no dependency on clone support.

⚠️ **US1 cannot begin until T002 and T003 are complete. US2 can start in parallel with this phase.**

- [x] T002 Add `ICommand<TNode> Clone()` default interface method (throws `NotSupportedException`) to `TLio.Core/Contracts/ICommand.cs`
- [x] T003 Add `public override ICommand<TNode> Clone() => (ICommand<TNode>)MemberwiseClone()` to `CommandBase<TNode>` in `TLio.Core/Models/CommandBase.cs` (depends on T002)

**Checkpoint**: `dotnet build TLio.Core` passes. All existing tests still pass.

---

## Phase 3: User Story 1 — Parse-Once CompiledScript (Priority: P1) 🎯 MVP

**Goal**: Parse a script text once into a `CompiledScript<TNode>`, then call `Execute()` or `CreateExecutable()` per concurrent request — each execution gets its own independent command state via shallow clone.

**Independent Test**: Call `ScriptEngine.Compile(scriptJson, adapter)` once, then call `compiled.Execute(data, ctx)` 100 times concurrently — every result is correct and no `_executionFailed` state bleeds across instances.

### Fixtures

- [x] T004 [P] [US1] Create fixture triplet `input.json` / `script.json` / `result.json` for a simple set-then-read scenario in `TLio.Json.SystemText.Tests/CompiledScriptTests/Fixtures/simple-set/`

### Tests (write first — must FAIL before T007/T008)

- [x] T005 [P] [US1] Write `CompiledScript_IsolationTests.cs` in `TLio.Json.SystemText.Tests/CompiledScriptTests/` — unit test: call `Clone()` on a command, set `_executionFailed` via reflection on one clone, assert the other clone's `IsSuccessful` is unaffected (depends on T003)
- [x] T006 [P] [US1] Write `CompiledScript_ConcurrencyTests.cs` in `TLio.Json.SystemText.Tests/CompiledScriptTests/` — fixture-driven theory: compile once from `simple-set` fixture, run 100 `Parallel.ForEach` executions, assert every `result.json` matches expected output (depends on T004)

### Implementation

- [x] T007 [US1] Implement `CompiledScript<TNode>` sealed class with `internal` ctor, `CreateExecutable()` returning cloned `TLioScript<TNode>`, and `Execute(TNode, IExecutionContext<TNode>)` convenience method in `TLio.Client/CompiledScript.cs` (depends on T003)
- [x] T008 [US1] Add `Compile(string scriptText, INodeAdapter<TNode> adapter)` and `Compile(string scriptText, IExecutionContext<TNode> context)` overloads to `TLio.Client/ScriptEngine.cs` (depends on T007)

**Checkpoint**: T005 and T006 pass. `CompiledScript` is usable from `TLio.Client`. US1 fully functional.

---

## Phase 4: User Story 2 — STJ Mutation-Aware Document Cache (Priority: P2)

**Goal**: `SystemTextJsonPathItemsFetcher` no longer serializes + re-parses on every `SelectNodes` call. Instead it caches the `JsonDocument` and reuses it while content is unchanged, rebuilding automatically after any command mutation.

**Independent Test**: A multi-command script where command 1 writes `$.name` and command 2 reads `$.name` returns the written value (not the original). A single-command script with 20 consecutive `SelectNodes` calls triggers `JsonDocument.Parse` exactly once.

### Fixtures

- [x] T009 [P] [US2] Create fixture triplet for mutation correctness in `TLio.Json.SystemText.Tests/SystemTextJsonPathItemsFetcherTests/Fixtures/mutation-correctness/` — `script.json` contains a `set` command followed by a `copy` from the set path, `result.json` reflects post-mutation value

### Tests (write first — must FAIL before T012–T015)

- [x] T010 [P] [US2] Write `FetcherMutationCorrectnessTests.cs` in `TLio.Json.SystemText.Tests/SystemTextJsonPathItemsFetcherTests/` — fixture-driven theory using `mutation-correctness` triplet: assert `result.json` matches after running the full script (depends on T009)
- [x] T011 [P] [US2] Write `FetcherOptimizationTests.cs` in `TLio.Json.SystemText.Tests/SystemTextJsonPathItemsFetcherTests/` — directly instantiate `SystemTextJsonPathItemsFetcher`, call `SelectNodes` 20 times on the same unmodified `JsonNode`, assert `fetcher.ParseCount == 1` (will use internal test counter added in T013)

### Implementation

- [x] T012 [US2] Add `private static readonly ConcurrentDictionary<string, JsonSelector> _selectorCache` and `private static JsonSelector GetSelector(string path)` to `TLio.Json.SystemText/SystemTextJsonPathItemsFetcher.cs`
- [x] T013 [US2] Add per-instance fields `_cachedJson (string?)`, `_cachedDocument (JsonDocument?)` and `private JsonDocument GetDocument(JsonNode data)` helper (serialize → compare → reuse or rebuild) to `SystemTextJsonPathItemsFetcher.cs`; also add `internal int ParseCount` field incremented on each cache miss (depends on T012)
- [x] T014 [US2] Refactor `SelectNodes()` and `SelectNode()` in `SystemTextJsonPathItemsFetcher.cs` to call `GetDocument(data)` and `GetSelector(path)` instead of their current inline `data.ToJsonString() / JsonDocument.Parse / JsonSelector.Parse` pattern (depends on T013)
- [x] T015 [US2] Implement `IDisposable` on `SystemTextJsonPathItemsFetcher` — add `public void Dispose() => _cachedDocument?.Dispose()` in `TLio.Json.SystemText/SystemTextJsonPathItemsFetcher.cs` (depends on T013)

**Checkpoint**: T010 and T011 pass. Mutation correctness confirmed. Optimization confirmed via `ParseCount`. US2 fully functional.

---

## Phase 5: Performance Tests

**Goal**: NUnit allocation assertions confirm (a) single compiled execution costs fewer managed bytes than parse-and-execute, and (b) 1000-item compiled batch costs ≤50% of 1000-item parse-per-item batch (SC-006, SC-007).

### Fixtures

- [x] T016 [P] Create performance fixture triplet in `TLio.Json.SystemText.Tests/PerformanceTests/Fixtures/perf-script/` — `input.json`: medium-complexity object (5 fields, 1 nested object, 1 array); `script.json`: 4–6 commands (set, copy, conditional); `result.json`: expected output

### Tests

- [x] T017 Write `CompiledScript_PerformanceTests.cs` in `TLio.Json.SystemText.Tests/PerformanceTests/` with:
  - `SingleExecution_CompiledAllocatesLessThanParseAndExecute`: JIT warmup, GC.Collect, measure `GC.GetAllocatedBytesForCurrentThread()` for parse path vs compiled path, `Assert.Less(compiledAlloc, parseAlloc)` (SC-006)
  - `BatchExecution_1000Items_CompiledAllocatesSignificantlyLess`: measure total allocs for 1000-item parse loop vs 1000-item compiled loop, `Assert.Less(compiledTotal, parseTotal * 0.5)` (SC-007)
  - (depends on T008, T016)

**Checkpoint**: SC-006 and SC-007 assertions pass. Performance regression guard in place.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T018 [P] Run constitutional compliance grep — Article I/IX: `grep -rn "Newtonsoft\|JToken\|JObject\|JArray\|XElement\|YamlNode" TLio.Core/ TLio.Commands/ TLio.Functions/` must return zero results
- [x] T019 [P] Run constitutional compliance grep — Article II: `grep -rn "new.*Adapter\|new.*Fetcher\|new.*ExecutionContext" TLio.Commands/ TLio.Functions/` must return zero results
- [x] T020 [P] Run full `dotnet test TLio.Json.SystemText.Tests` — all correctness, isolation, concurrency, optimization, mutation, and performance tests must pass
- [x] T021 Run `dotnet test` across entire solution — confirm no regressions in TLio.Core, TLio.Client, TLio.Json, TLio.Xml, TLio.Yaml, TLio.Functions, TLio.UnitTests
- [x] T022 [P] Verify public API shape matches `contracts/api.md` — confirm `CompiledScript<TNode>` is sealed, ctor is internal, `Compile` overloads are on `ScriptEngine`, `IDisposable` is on `SystemTextJsonPathItemsFetcher`

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
  └─→ Phase 2 (Foundational: Core Clone) ─┐
  └─→ Phase 4 (US2: STJ Fetcher) ←────────┘ (parallel with Phase 2)
         Phase 2 completion ─→ Phase 3 (US1: CompiledScript)
         Phase 3 + Phase 4 ─→ Phase 5 (Performance Tests)
         Phase 5 ─→ Phase 6 (Polish)
```

### User Story Dependencies

- **US2 (Phase 4)**: Independent of Core clone work — can start after Phase 1.
- **US1 (Phase 3)**: Requires Phase 2 (T002, T003 must be done). Independent of US2.
- **Performance Tests (Phase 5)**: Require US1 complete (T008) and performance fixture (T016).

### Parallel Opportunities Within Phases

**Phase 2**: T002 → T003 (sequential, same interface hierarchy)  
**Phase 3**: T004, T005, T006 all parallelizable (different files, all write-only); T007 → T008 (sequential)  
**Phase 4**: T009, T010, T011 parallelizable; T012 → T013 → T014, T015 (T014 and T015 parallelizable after T013)  
**Phase 5**: T016 parallelizable with Phase 3/4 work; T017 sequential after T008+T016  
**Phase 6**: T018, T019, T020, T022 all parallelizable (read-only checks); T021 sequential after T020

---

## Parallel Execution Example: Phase 3 + Phase 4

```
# Start both stories in parallel after Phase 2 completes:

Agent A (US1):
  T004 → fixture triplet simple-set
  T005 → isolation tests (parallel with T006)
  T006 → concurrency tests (parallel with T005)
  T007 → CompiledScript implementation
  T008 → ScriptEngine.Compile overloads

Agent B (US2 — can start immediately after Phase 1):
  T009 → fixture triplet mutation-correctness
  T010 → mutation correctness tests (parallel with T011)
  T011 → optimization tests (parallel with T010)
  T012 → selector cache
  T013 → document cache + ParseCount
  T014 → refactor SelectNodes/SelectNode
  T015 → IDisposable
```

---

## Implementation Strategy

### MVP (US1 only — Phase 1–3)

1. Phase 1: baseline build ✓
2. Phase 2: Core clone support (2 files, ~5 lines)
3. Phase 3: `CompiledScript<TNode>` + `ScriptEngine.Compile` + tests
4. **STOP & VALIDATE**: 100-concurrent-execution test passes, isolation test passes
5. Usable compile-once API in `TLio.Client`

### Full Delivery (Phase 1–6)

- Add US2 (STJ cache) in parallel with or after US1
- Add performance tests after both stories complete
- Polish phase confirms no regressions across the full solution

---

## Notes

- `internal int ParseCount` on `SystemTextJsonPathItemsFetcher` is test-support only — a `[Conditional("DEBUG")]` attribute or `#if DEBUG` guard may be used if shipping to NuGet requires a clean release surface.
- `GC.GetAllocatedBytesForCurrentThread()` requires single-threaded test execution; do not use `Parallel.ForEach` inside performance test measurement blocks.
- JIT warmup pass is mandatory before measuring — at minimum one discard call per code path measured.
- The 50% threshold in SC-007 is a minimum regression guard. In practice the reduction is larger (parse cost × N eliminated).
- Before marking any implementation task `[x]`, run the Article I/IX and Article II grep checks from Phase 6.
