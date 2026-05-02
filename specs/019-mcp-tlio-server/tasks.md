# Tasks: TLio MCP Server

**Input**: Design documents from `/specs/019-mcp-tlio-server/`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ contracts/ ✅ quickstart.md ✅

**Organization**: Grouped by user story — each phase is independently testable and deliverable.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: Which user story owns this task (US1 / US2 / US3)
- Setup and Foundational phases have no story label

---

## Phase 1: Setup

**Purpose**: Create new project scaffolding. No existing code touched.

- [ ] T001 Create `TLio.Mcp/TLio.Mcp.csproj` targeting net10.0 with NuGet references: `ModelContextProtocol`, `Microsoft.Extensions.Hosting`, `TLio.Client`, `TLio.Json`, `TLio.Json.SystemText`, `TLio.Xml`, `TLio.Yaml`, `TLio.Commands`, `TLio.Functions`, `TLio.Extensions.ETL`, `TLio.Extensions.Text`, `TLio.Extensions.Math`, `TLio.Extensions.TimeDate`
- [ ] T002 [P] Create `TLio.Mcp.Tests/TLio.Mcp.Tests.csproj` targeting net10.0 with NuGet references: `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, `ModelContextProtocol` (client), project reference to `TLio.Mcp`
- [ ] T003 Add `TLio.Mcp` and `TLio.Mcp.Tests` to `TLio.sln` via `dotnet sln add`; run `dotnet build` to confirm scaffold builds

**Checkpoint**: `dotnet build` succeeds with zero errors on both new projects.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that ALL user stories depend on. Must be complete before any US phase begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T004 [P] Create `TLio.Core/Models/TraceOutcome.cs` — `public enum TraceOutcome { Success, NoOp, Failure }` — no format-specific types, primitives only
- [ ] T005 [P] Create `TLio.Core/Models/TraceEntry.cs` — `public record TraceEntry(string CommandName, string Path, TraceOutcome Outcome, int MatchedCount, string Detail)` — primitives only, no TNode
- [ ] T006 [P] Create `TLio.Core/Contracts/ITraceCollector.cs` — `public interface ITraceCollector { void Record(TraceEntry entry); }` — no TNode in signature
- [ ] T007 Add `ITraceCollector? TraceCollector { get; set; }` to `TLio.Core/Contracts/IExecutionContext.cs` — additive property only; verify Article I/IX grep still returns zero after change
- [ ] T008 [P] Add `public ITraceCollector? TraceCollector { get; set; } = null;` to the concrete ExecutionContext class(es) in `TLio.Json/` implementing `IExecutionContext<TNode>` (depends on T007)
- [ ] T009 [P] Add `public ITraceCollector? TraceCollector { get; set; } = null;` to the concrete ExecutionContext class(es) in `TLio.Json.SystemText/` (depends on T007)
- [ ] T010 [P] Add `public ITraceCollector? TraceCollector { get; set; } = null;` to the concrete ExecutionContext class(es) in `TLio.Xml/` (depends on T007)
- [ ] T011 [P] Add `public ITraceCollector? TraceCollector { get; set; } = null;` to the concrete ExecutionContext class(es) in `TLio.Yaml/` (depends on T007)
- [ ] T012 [P] Create `TLio.Mcp/Configuration/McpConfiguration.cs` — `McpConfiguration(ObservabilityConfig Observability, RateLimitConfig RateLimit, string AiRefRoot)`, `ObservabilityConfig(bool Enabled)`, `RateLimitConfig(int RequestsPerMinute, int WindowCount)` records
- [ ] T013 [P] Create `TLio.Mcp/appsettings.json` with defaults: `Observability.Enabled=true`, `RateLimit.RequestsPerMinute=20`, `RateLimit.WindowCount=6`, `AiRefRoot=""`
- [ ] T014 Implement `TLio.Mcp/Services/RateLimiterService.cs` — wraps `SlidingWindowRateLimiter` (System.Threading.RateLimiting); exposes `TryAcquire(out int retryAfterSeconds)` returning bool; reads `RateLimitConfig` from `IOptions<McpConfiguration>` (depends on T012)
- [ ] T015 [P] Implement `TLio.Mcp/Services/DocumentService.cs` — `ParseJson(string)→JToken`, `ParseXml(string)→XElement`, `ParseYaml(string)→YamlNode`, and corresponding `Serialize*` methods; returns structured error on malformed input
- [ ] T016 Create `TLio.Mcp/Program.cs` — GenericHost with `AddMcpServer().WithStdioServerTransport()`, bind `McpConfiguration` from config + env vars (`TLIO_MCP_TRACE`, `TLIO_MCP_RATELIMIT`), register `RateLimiterService` and `DocumentService` as singletons; placeholder tool registration comments for US1/US2/US3 (depends on T012, T013, T014, T015)

**Checkpoint**: `dotnet build` succeeds; `dotnet test` of all existing test projects passes with zero failures and zero regressions.

---

## Phase 3: User Story 1 — Discover Commands and Functions (Priority: P1) 🎯 MVP

**Goal**: Agent can list all TLio commands/functions and retrieve full documentation for any of them by name.

**Independent Test**: Run the MCP server, call `tlio_list_commands` — verify all commands appear. Call `tlio_describe name=Set` — verify full Set.md content returned. Call `tlio_describe name=Sett` — verify not-found + suggestions response.

- [ ] T017 [P] [US1] Implement `TLio.Mcp/Services/AiRefReader.cs` — `ListCommands()→IReadOnlyList<(string Name, string Intent)>` and `ListFunctions()` by globbing `{AiRefRoot}/commands/*.md` and `functions/*.md`; extract filename as name and first non-heading prose line as intent; `GetContent(string name, string? type)→string?` returns full file text; `GetSuggestions(string name)→string[]` returns names with Levenshtein distance ≤ 2
- [ ] T018 [P] [US1] Implement `TLio.Mcp/Tools/DiscoveryTools.cs` — `[McpServerToolType]` class with three `[McpServerTool]` methods: `ListCommands()`, `ListFunctions()`, `Describe(string name, string? type)`; each calls `RateLimiterService.TryAcquire` first and returns rate-limit error object if denied; not-found returns `{ error, name, suggestions[] }` per contracts/mcp-tools.md
- [ ] T019 [US1] Register `AiRefReader` (singleton) and `DiscoveryTools` in `TLio.Mcp/Program.cs` DI; resolve `AiRefRoot` path relative to `AppContext.BaseDirectory` when config value is empty (depends on T016, T017, T018)
- [ ] T020 [P] [US1] Write `TLio.Mcp.Tests/DiscoveryToolsTests.cs` — test `tlio_list_commands` returns non-empty list with known entries (Set, Add, Copy); test `tlio_list_functions` returns known functions (concat, format); test `tlio_describe` for exact name returns markdown content containing `## Syntax`; test unknown name returns `error=not_found` with non-empty suggestions for near-misses
- [ ] T021 [P] [US1] Write `TLio.Mcp.Tests/RateLimiterTests.cs` — test `TryAcquire` allows up to configured limit then denies with positive `retryAfterSeconds`; test limit resets after window expires

**Checkpoint**: US1 fully functional independently. `dotnet test --filter Category=US1` (or equivalent) passes. Running the server and calling `tlio_list_commands` + `tlio_describe` returns correct responses.

---

## Phase 4: User Story 2 — Execute Script and Observe Results (Priority: P2)

**Goal**: Agent can run a TLio script against a document and receive the transformed output plus a per-command trace showing success/noop/failure with detail.

**Independent Test**: Submit `[{"command":"set","path":"$.name","value":"Bob"}]` against `{"name":"Alice"}` (JSON). Verify output is `{"name":"Bob"}` and trace has one entry with `outcome=success`, `matched_count=1`. Submit same script against `{}` — verify trace entry shows `outcome=noop`, `matched_count=0`.

- [ ] T022 [P] [US2] Implement `TLio.Mcp/Services/McpTraceCollector.cs` — `ITraceCollector` implementation; holds `List<TraceEntry>` (per-instance, not static); exposes `IReadOnlyList<TraceEntry> Entries { get; }` and `void Record(TraceEntry)` thread-safely
- [ ] T023 [P] [US2] Add `context.TraceCollector?.Record(new TraceEntry(nameof(AddCommand), path, outcome, matchedCount, detail))` after existing `LogInfo`/`LogWarning`/`LogError` calls in `TLio.Commands/AddCommand.cs`, `SetCommand.cs`, `PutCommand.cs` — additive only; determine `outcome` from existing log level (Info→Success, Warning→NoOp, Error→Failure)
- [ ] T024 [P] [US2] Same TraceCollector?.Record() pattern in `TLio.Commands/RemoveCommand.cs`, `CopyCommand.cs`, `MoveCommand.cs` (depends on T006, T007)
- [ ] T025 [P] [US2] Same pattern in `TLio.Commands/IfElseCommand.cs`, `CompareCommand.cs`, `DecisionTableCommand.cs`, `MergeCommand.cs` (depends on T006, T007)
- [ ] T026 [P] [US2] Same pattern in `TLio.Extensions.ETL/FlattenCommand.cs`, `RestoreCommand.cs`, `ResolveCommand.cs`, `ToCsvCommand.cs` (depends on T006, T007)
- [ ] T027 [US2] Implement `TLio.Mcp/Tools/ExecutionTools.cs` — `[McpServerTool] tlio_execute(ExecuteRequest)`: rate-limit check first; parse document via `DocumentService`; select execution context (JsonExecutionContext / XmlExecutionContext / YamlExecutionContext) per format and xml_path_style; if `Observability.Enabled`, create `McpTraceCollector` and assign to `context.TraceCollector`; call `ScriptEngine<TNode>.Execute(script, document, context)`; serialize output; map `context.TraceCollector?.Entries` to `CommandTraceRecord[]`; return `ExecuteResult` per contracts/mcp-tools.md (depends on T015, T022, T023–T026)
- [ ] T028 [US2] Register `ExecutionTools` in `TLio.Mcp/Program.cs` DI (depends on T016, T027)
- [ ] T029 [P] [US2] Create fixture triplets in `TLio.Mcp.Tests/Fixtures/Execute_SetSuccess/` — `input.json`, `script.json`, `result.json` for a successful Set command (name Alice → Bob); also `Execute_NoOp/` for path with zero matches; also `Execute_TypeMismatch/` for failure (set on wrong-type node)
- [ ] T030 [US2] Write `TLio.Mcp.Tests/ExecutionToolsTests.cs` using fixture triplets from T029 — verify: (a) success case output correct + trace entry outcome=success matched_count=1; (b) noop case output unchanged + trace entry outcome=noop matched_count=0; (c) failure case success=false + trace entry outcome=failure; (d) observability-disabled case trace=[] (depends on T027, T029)
- [ ] T031 [P] [US2] Run Article I/IX compliance grep after all T023–T026 changes; verify zero results; run `dotnet test` on all existing projects to verify zero regressions from command instrumentation

**Checkpoint**: US2 fully functional independently. `tlio_execute` returns correct output and structured trace for all three outcomes across JSON, XML, and YAML inputs.

---

## Phase 5: User Story 3 — Analyze Transformation Gap (Priority: P3)

**Goal**: Agent can provide an input document and a desired target document; server returns a deterministic gap report listing every structural change needed.

**Independent Test**: Call `tlio_analyze` with `input={"name":"Alice","age":30}`, `target={"fullName":"Alice","age":31}`, `intent="rename name to fullName and increment age"`. Verify response has 2 change items: one Rename ($.name → $.fullName) and one Mutate ($.age, 30 → 31). Call with identical input/target — verify empty changes list.

- [ ] T032 [P] [US3] Implement JSON diff in `TLio.Mcp/Services/StructuralDiffService.cs` — `DiffJson(JToken input, JToken target)→List<ChangeItem>`: recursively flatten both trees to `Dictionary<string, string?>` (leaf JSONPath → scalar value); compute symmetric diff for Add/Remove/Mutate; apply rename heuristic (Remove+Add pair where path-segment names differ by Levenshtein distance ≤ 1 and values are identical → collapse to Rename); detect array Reorder (same leaf values, different index positions)
- [ ] T033 [P] [US3] Add XML diff to `TLio.Mcp/Services/StructuralDiffService.cs` — `DiffXml(XElement input, XElement target)→List<ChangeItem>`: flatten XElement trees to `Dictionary<string, string?>` using slash-path notation (element names and @attribute notation); same symmetric diff + rename heuristic as T032
- [ ] T034 [P] [US3] Add YAML diff to `TLio.Mcp/Services/StructuralDiffService.cs` — `DiffYaml(YamlNode input, YamlNode target)→List<ChangeItem>`: flatten YamlMappingNode/YamlSequenceNode/YamlScalarNode trees to dot-notation path dictionary; same symmetric diff logic
- [ ] T035 [US3] Add intent annotation and refinement mode to `TLio.Mcp/Services/StructuralDiffService.cs` — `AnnotateWithIntent(List<ChangeItem> changes, string intent)`: for each change item, if intent string contains words matching the source or target path segment, set `IntentAnnotation`; add `ApplyRefinement(List<ChangeItem> changes, IReadOnlyList<CommandTraceRecord> priorTrace)`: mark each change as Resolved if a prior success-outcome trace entry targets the same path, else Unresolved (depends on T032, T033, T034)
- [ ] T036 [US3] Implement `TLio.Mcp/Tools/AnalysisTools.cs` — `[McpServerTool] tlio_analyze(AnalyzeRequest)`: rate-limit check; parse input and target documents via `DocumentService`; call format-appropriate `StructuralDiffService.Diff*` method; if `intent` supplied call `AnnotateWithIntent`; if `prior_trace` supplied call `ApplyRefinement`; build summary string ("N changes: X rename, Y mutate, Z add"); return `AnalyzeResult` per contracts/mcp-tools.md (depends on T015, T035)
- [ ] T037 [US3] Register `StructuralDiffService` (singleton) and `AnalysisTools` in `TLio.Mcp/Program.cs` DI (depends on T016, T036)
- [ ] T038 [US3] Write `TLio.Mcp.Tests/AnalysisToolsTests.cs` — test cases: (a) Rename detection (name→fullName same value); (b) Mutate detection (value changed); (c) Add detection (field present in target only); (d) Remove detection (field present in input only); (e) identical documents → empty changes; (f) intent annotation populates IntentAnnotation when intent matches; (g) refinement mode marks resolved vs unresolved based on prior trace (depends on T036)

**Checkpoint**: US3 fully functional independently. `tlio_analyze` produces correct gap reports for JSON, XML, and YAML document pairs.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: End-to-end validation, regression guard, and final verification.

- [ ] T039 Write `TLio.Mcp.Tests/EndToEndWorkflowTests.cs` — full agent workflow: (1) `tlio_list_commands` → pick Set; (2) `tlio_describe name=Set` → verify syntax section present; (3) `tlio_execute` first attempt with imperfect script → trace shows noop; (4) `tlio_analyze` to get gap report; (5) `tlio_execute` with corrected script → success within 2 iterations
- [ ] T040 [P] Run full `dotnet test` across all solution projects; confirm zero regressions; attach output to PR description
- [ ] T041 [P] Run Article I/IX compliance grep across `TLio.Core/`, `TLio.Commands/`, `TLio.Functions/`; run Article II grep across `TLio.Commands/`, `TLio.Functions/`; both MUST return zero results
- [ ] T042 [P] Validate `specs/019-mcp-tlio-server/quickstart.md` steps: build, configure, run server, connect via Claude Code `mcp_servers.json`; correct any stale paths or package names
- [ ] T043 [P] Update `CLAUDE.md` project structure section to include `TLio.Mcp/` and `TLio.Mcp.Tests/` in the project tree
- [ ] T044 [P] Verify rate-limit error response matches contracts/mcp-tools.md exactly — `{ "error": "rate_limit_exceeded", "retry_after_seconds": N, "message": "..." }` — by inspecting the live server response when limit is exceeded in T039 workflow

**Checkpoint**: All tests green, compliance greps clean, quickstart validated, server successfully used by an agent in the end-to-end test.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Phase 2 — no dependency on US2 or US3
- **US2 (Phase 4)**: Depends on Phase 2 — no dependency on US1 or US3
- **US3 (Phase 5)**: Depends on Phase 2 — no dependency on US1 or US2
- **Polish (Phase 6)**: Depends on all desired story phases being complete

### Within Phase 2 (Foundational)

```
T004, T005, T006 [P] → T007 → T008, T009, T010, T011 [P]
T012, T013 [P] → T014
T015 [P]
T008–T011 + T014 + T015 → T016
```

### Within Phase 4 (US2)

```
T022 [P] (McpTraceCollector — depends only on T006)
T023, T024, T025, T026 [P] (command instrumentation — depends on T006, T007)
T023–T026 + T022 → T027 (ExecutionTools)
T027 → T028 (DI registration)
T029 [P] (fixture files — independent)
T027 + T029 → T030 (tests)
```

### Within Phase 5 (US3)

```
T032, T033, T034 [P] → T035 → T036 → T037
T036 → T038 (tests)
```

### User Story Independence

- **US1 (Phase 3)**: Standalone — AiRefReader and DiscoveryTools have no dependency on US2 or US3 components
- **US2 (Phase 4)**: Standalone — ExecutionTools depends only on foundational services (DocumentService, McpTraceCollector, ITraceCollector infrastructure)
- **US3 (Phase 5)**: Standalone — StructuralDiffService and AnalysisTools depend only on DocumentService

---

## Parallel Opportunities

### Phase 2 — Parallel batch 1 (start immediately after T001-T003)
```
T004 (TraceOutcome), T005 (TraceEntry), T006 (ITraceCollector), T012 (McpConfiguration), T013 (appsettings.json), T015 (DocumentService) — all parallel
```

### Phase 2 — Parallel batch 2 (after T007 completes)
```
T008, T009, T010, T011 — all parallel (one per adapter project)
```

### Phase 3 — Parallel start (after Phase 2)
```
T017 (AiRefReader), T018 (DiscoveryTools) — parallel
```

### Phase 4 — Parallel start (after Phase 2; concurrent with Phase 3)
```
T022 (McpTraceCollector), T023, T024, T025, T026 (command instrumentation) — all parallel
```

### Phase 5 — Parallel start (after Phase 2; concurrent with Phase 3 and 4)
```
T032, T033, T034 (JSON/XML/YAML diff) — all parallel
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1)
2. **STOP and VALIDATE**: call `tlio_list_commands`, `tlio_list_functions`, `tlio_describe`
3. Confirm agent can identify the right command from the discovery tools alone

### Incremental Delivery

1. Setup + Foundational → confirm `dotnet build` clean
2. Add US1 (Discovery) → test independently → **MVP working**
3. Add US2 (Execution) → test independently → agent can now execute and debug scripts
4. Add US3 (Analysis) → test independently → full generate-execute-refine loop enabled
5. Polish → end-to-end workflow validated

### Parallel Team Strategy

With two developers after Foundational completes:
- Dev A: US1 (Phase 3) + US3 (Phase 5 — less code, starts after US1 if solo)
- Dev B: US2 (Phase 4 — most code, command instrumentation)

---

## Notes

- `[P]` = parallelizable (different files, no incomplete dependency)
- `[US1/US2/US3]` = traceability to user story
- All command instrumentation tasks (T023–T026) are purely additive — one null-guarded line per Execute() body. Verify Article I/IX grep after each batch.
- Fixture triplets for T029 live in `TLio.Mcp.Tests/Fixtures/` — not in existing test projects — because they test the MCP execution layer, not the core engine
- Rate-limit check is the **first** statement in every tool handler before any processing
- `McpTraceCollector` is created per-request (not singleton) to avoid cross-request contamination
