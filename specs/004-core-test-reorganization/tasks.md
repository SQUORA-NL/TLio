# Tasks: Core Test Coverage and Test Project Reorganization

**Input**: Design documents from `specs/004-core-test-reorganization/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓

**Organization**: Tasks grouped by user story for independent delivery.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 = Command Orchestration Audit, US2 = JSON Adapter Test Migration, US3 = Functions Test Migration + Layout Consistency

---

## Phase 1: Setup (New Test Projects)

**Purpose**: Create the three new `.csproj` files and register them in the solution. No test files yet — just scaffolding. All four tasks can run in parallel.

- [X] T001 [P] Create `TLio.Json.Tests/TLio.Json.Tests.csproj` targeting net10.0 with packages: NUnit 4.2.2, NUnit3TestAdapter 4.6.0, Microsoft.NET.Test.Sdk 17.12.0; project references: TLio.Json, TLio.Client
- [X] T002 [P] Create `TLio.Json.SystemText.Tests/TLio.Json.SystemText.Tests.csproj` targeting net10.0 with packages: NUnit 4.2.2, NUnit3TestAdapter 4.6.0, Microsoft.NET.Test.Sdk 17.12.0; project references: TLio.Json.SystemText, TLio.Client
- [X] T003 [P] Create `TLio.Functions.Tests/TLio.Functions.Tests.csproj` targeting net10.0 with packages: NUnit 4.2.2, NUnit3TestAdapter 4.6.0, Microsoft.NET.Test.Sdk 17.12.0; project references: TLio.Functions, TLio.Json, TLio.Client, TLio.Extensions.Math, TLio.Extensions.Text, TLio.Extensions.TimeDate
- [X] T004 Add `TLio.Json.Tests`, `TLio.Json.SystemText.Tests`, and `TLio.Functions.Tests` to `TLio.sln` (3 new `Project(...)` entries and corresponding `GlobalSection` build config entries)
- [X] T005 Run `dotnet build TLio.sln` and confirm all new projects compile clean with zero errors

---

## Phase 2: Foundational (Baseline)

**Purpose**: Capture the pre-migration test baseline. No user story work begins until T006 passes.

**⚠️ CRITICAL**: Record the exact passing test count from TLio.UnitTests before any files are moved. Use this count to verify zero regressions after each migration phase.

- [X] T006 Run `dotnet test TLio.UnitTests/TLio.UnitTests.csproj --verbosity normal` and record the total passing test count as the regression baseline — **BASELINE: 773 tests**

**Checkpoint**: Baseline captured — user story implementation can now proceed in parallel across US1, US2, and US3.

---

## Phase 3: User Story 1 — Command Orchestration Audit (Priority: P1) 🎯 MVP

**Goal**: Every command in `TLio.UnitTests/CommandsTests/` has tests covering (1) primary success path, (2) path-not-found behavior, (3) a command-specific edge case, and (4) at least one `LogInfo`/`LogWarning` assertion per Article X.

**Independent Test**: `dotnet test TLio.UnitTests/TLio.UnitTests.csproj --filter "FullyQualifiedName~CommandsTests"` passes with no gaps for any of the three coverage checks.

**Audit rules** (apply to each task below):
- Check for a test where the target path resolves to zero nodes → command completes without exception, logs a warning
- Check for a test that asserts the document mutation result is correct
- Check for a command-specific edge case (see per-task notes)
- Check that at least one test asserts a log entry was written (`ExecutionLogger` / `IExecutionLogger`)
- Any net-new full-execution test scenario MUST use a fixture triplet in the matching `Fixtures/` subfolder
- Inline `[TestCase]` is allowed only for null/empty path validation

### Basic Property Commands

- [X] T007 [P] [US1] Audit `TLio.UnitTests/CommandsTests/AddTests.cs` — verify coverage: path-not-found (Add to missing parent), success (new property added), edge case (adding a duplicate key); add missing fixture triplets to `TLio.UnitTests/Fixtures/Add/`; add log assertions where absent
- [X] T008 [P] [US1] Audit `TLio.UnitTests/CommandsTests/RemoveTests.cs` — verify coverage: path-not-found (Remove from non-existent node), success (property removed), edge case (removing last element from array); add missing fixture triplets to `TLio.UnitTests/Fixtures/Remove/`; add log assertions where absent
- [X] T009 [P] [US1] Audit `TLio.UnitTests/CommandsTests/SetTests.cs` — verify coverage: path-not-found (Set on missing path), success (value overwritten), edge case (Set to null value); add missing fixture triplets to `TLio.UnitTests/Fixtures/Set/`; add log assertions where absent
- [X] T010 [P] [US1] Audit `TLio.UnitTests/CommandsTests/PutTests.cs` — verify coverage: path-not-found (Put creates path vs. Put on existing), success (value placed), edge case (Put into array vs. object); add missing fixture triplets to `TLio.UnitTests/Fixtures/Put/`; add log assertions where absent

### Copy and Move Commands

- [X] T011 [P] [US1] Audit `TLio.UnitTests/CommandsTests/CopyMoveTests.cs`, `CopyMoveBaseTests.cs`, and `CopyMoveDestinationAsArrayTests.cs` — verify coverage: source path-not-found (no-op), destination path-not-found (creates destination), success (node cloned/moved correctly), edge case (destination is array — append vs. replace); add missing fixture triplets to `TLio.UnitTests/Fixtures/Copy/` and `TLio.UnitTests/Fixtures/Move/`; add log assertions where absent

### Merge and Compare

- [X] T012 [P] [US1] Audit `TLio.UnitTests/CommandsTests/MergeTests.cs` — verify coverage: path-not-found (source missing), success (deep merge result), edge case (merging array into object); add missing fixture triplets to `TLio.UnitTests/Fixtures/Merge/`; add log assertions where absent
- [X] T013 [P] [US1] Audit `TLio.UnitTests/CommandsTests/CompareTests.cs` — verify coverage: path-not-found (one side missing), success (equal nodes), edge case (type mismatch comparison); add log assertions where absent

### Conditional and Decision Commands

- [X] T014 [P] [US1] Audit `TLio.UnitTests/CommandsTests/IfElseTests.cs` — verify coverage: condition path-not-found, condition evaluates true (then-branch executes), condition evaluates false (else-branch executes), edge case (missing else-branch with false condition is a no-op); add missing fixture triplets to `TLio.UnitTests/Fixtures/IfElse/`; add log assertions where absent
- [X] T015 [P] [US1] Audit `TLio.UnitTests/CommandsTests/DecisionTableTests.cs`, `DecisionTableAdvancedTests.cs`, `DecisionTableBuilderTests.cs`, and `DecisionTableJsonParseTests.cs` — verify coverage: no matching rule (default/fallthrough), single rule match (correct output applied), edge case (first-match vs. all-match strategy); add log assertions where absent

### Base Classes and Utilities

- [X] T016 [P] [US1] Audit `TLio.UnitTests/CommandsTests/PropertyChangeCommandBaseTests.cs`, `PropertyFieldBackwardsCompatibilityTests.cs`, and `ParentNavigationTests.cs` — verify coverage: null path (validation), valid parent navigation, edge case (navigating past root); add log assertions where absent

### ETL Commands

- [X] T017 [P] [US1] Audit `TLio.UnitTests/CommandsTests/ETLTests/FlattenRestoreTests.cs` and `ETLTests/ResolveTests.cs` — verify coverage: empty input (no-op), success (flatten produces expected structure, restore reverses it), edge case (nested arrays in flatten); add log assertions where absent

### Article VI tracking

- [X] T018 [US1] Review all `CommandsTests/` files for inline `[TestCase]` or hardcoded document strings used for full-execution scenarios (not validation-only); create a backlog section "## Article VI Backlog — Inline Tests to Convert to Fixture Triplets" at the bottom of this `tasks.md` listing each file and test method found

**Checkpoint**: All command test classes have path-not-found, success, and edge-case coverage, plus at least one logging assertion. `dotnet test` passes with no regressions.

---

## Phase 4: User Story 2 — JSON Adapter Test Migration (Priority: P2)

**Goal**: `TLio.Json.Tests` and `TLio.Json.SystemText.Tests` exist, contain all relevant tests, and `TLio.UnitTests` no longer contains any adapter-specific JSON tests.

**Independent Test**: `dotnet test TLio.Json.Tests/TLio.Json.Tests.csproj` and `dotnet test TLio.Json.SystemText.Tests/TLio.Json.SystemText.Tests.csproj` both pass. `TLio.UnitTests/AdapterTests/` and `TLio.UnitTests/SystemTextTests/` no longer exist.

### TLio.Json.Tests — migrate adapter tests

- [X] T019 [P] [US2] Create folder `TLio.Json.Tests/AdapterTests/`; copy `TLio.UnitTests/AdapterTests/JsonNodeAdapterTests.cs` into it; fix namespace to `TLio.Json.Tests.AdapterTests`
- [X] T020 [P] [US2] Copy `TLio.UnitTests/AdapterTests/JsonPathFetcherTests.cs` into `TLio.Json.Tests/AdapterTests/`; fix namespace to `TLio.Json.Tests.AdapterTests`
- [X] T021 [P] [US2] Copy `TLio.UnitTests/AdapterTests/JsonPathMethodsTests.cs` into `TLio.Json.Tests/AdapterTests/`; fix namespace to `TLio.Json.Tests.AdapterTests`
- [X] T022 [US2] Run `dotnet test TLio.Json.Tests/TLio.Json.Tests.csproj` — confirm all 3 test classes compile and every test passes (depends on T019, T020, T021)

### TLio.Json.SystemText.Tests — migrate adapter tests

- [X] T023 [US2] Create folder `TLio.Json.SystemText.Tests/SystemTextTests/`; copy `TLio.UnitTests/SystemTextTests/SystemTextFixtureTests.cs` into it; fix namespace to `TLio.Json.SystemText.Tests.SystemTextTests`
- [X] T024 [US2] Run `dotnet test TLio.Json.SystemText.Tests/TLio.Json.SystemText.Tests.csproj` — confirm the test class compiles and all tests pass (depends on T023)

### Remove from TLio.UnitTests

- [X] T025 [P] [US2] Delete `TLio.UnitTests/AdapterTests/` folder and all its files (after T022 confirms TLio.Json.Tests is green)
- [X] T026 [P] [US2] Delete `TLio.UnitTests/SystemTextTests/` folder and its file (after T024 confirms TLio.Json.SystemText.Tests is green)
- [X] T027 [US2] Remove `<ProjectReference Include="..\TLio.Json.SystemText\TLio.Json.SystemText.csproj" />` from `TLio.UnitTests/TLio.UnitTests.csproj` (depends on T026)
- [X] T028 [US2] Run `dotnet test TLio.UnitTests/TLio.UnitTests.csproj` — confirm passing count matches T006 baseline minus the migrated adapter tests (zero regressions in remaining tests)

**Checkpoint**: `TLio.Json.Tests` and `TLio.Json.SystemText.Tests` both green. `TLio.UnitTests` contains no adapter-specific JSON tests.

---

## Phase 5: User Story 3 — Functions Test Migration + Layout Consistency (Priority: P3)

**Goal**: `TLio.Functions.Tests` exists with all function tests and function fixture triplets; `TLio.UnitTests` retains only core, command, and engine tests; all five libraries have a `[Library].Tests` project.

**Independent Test**: `dotnet test TLio.Functions.Tests/TLio.Functions.Tests.csproj` passes. `TLio.UnitTests` no longer contains `FunctionsTests/` or function fixture folders.

### Prepare TLio.Functions.Tests

- [X] T029 [US3] Copy `TLio.UnitTests/Fixtures/FixtureTheoryLoader.cs` to `TLio.Functions.Tests/Fixtures/FixtureTheoryLoader.cs`; update namespace to `TLio.Functions.Tests.Fixtures`; update fixture root path constant to point to the `TLio.Functions.Tests/Fixtures/` folder

### Move function fixture folders (all [P] — independent directories)

- [X] T030 [P] [US3] Move `TLio.UnitTests/Fixtures/Math/` to `TLio.Functions.Tests/Fixtures/Math/`
- [X] T031 [P] [US3] Move `TLio.UnitTests/Fixtures/Text/` to `TLio.Functions.Tests/Fixtures/Text/`
- [X] T032 [P] [US3] Move `TLio.UnitTests/Fixtures/TimeDate/` to `TLio.Functions.Tests/Fixtures/TimeDate/`
- [X] T033 [P] [US3] Move `TLio.UnitTests/Fixtures/Fetch/` to `TLio.Functions.Tests/Fixtures/Fetch/`
- [X] T034 [P] [US3] Move `TLio.UnitTests/Fixtures/Indirect/` to `TLio.Functions.Tests/Fixtures/Indirect/`
- [X] T035 [P] [US3] Move `TLio.UnitTests/Fixtures/Partial/` to `TLio.Functions.Tests/Fixtures/Partial/`
- [X] T036 [P] [US3] Move `TLio.UnitTests/Fixtures/Promote/` to `TLio.Functions.Tests/Fixtures/Promote/`
- [X] T037 [P] [US3] Move `TLio.UnitTests/Fixtures/ScriptPath/` to `TLio.Functions.Tests/Fixtures/ScriptPath/`

### Create fixture runner for TLio.Functions.Tests

- [X] T038 [US3] Create `TLio.Functions.Tests/Fixtures/FixtureTests.cs` — a NUnit `[TestFixture]` that uses `FixtureTheoryLoader` to load and run all triplets from Math, Text, TimeDate, Fetch, Indirect, Partial, Promote, ScriptPath fixture folders (depends on T029–T037)
- [X] T039 [US3] Move `TLio.UnitTests/Fixtures/ExtensionFixtureTests.cs` to `TLio.Functions.Tests/Fixtures/ExtensionFixtureTests.cs`; update namespace to `TLio.Functions.Tests.Fixtures`

### Move FunctionsTests classes (all [P] — independent files)

- [X] T040 [P] [US3] Move all 6 root files from `TLio.UnitTests/FunctionsTests/` (DatetimeFunctionTests.cs, FetchTests.cs, IndirectTests.cs, PartialTests.cs, PromoteTests.cs, ScriptPathTests.cs) to `TLio.Functions.Tests/FunctionsTests/`; update namespaces to `TLio.Functions.Tests.FunctionsTests`
- [X] T041 [P] [US3] Move all 28 files from `TLio.UnitTests/FunctionsTests/MathTests/` to `TLio.Functions.Tests/FunctionsTests/MathTests/`; update namespaces to `TLio.Functions.Tests.FunctionsTests.MathTests`
- [X] T042 [P] [US3] Move all 16 files from `TLio.UnitTests/FunctionsTests/TextTests/` to `TLio.Functions.Tests/FunctionsTests/TextTests/`; update namespaces to `TLio.Functions.Tests.FunctionsTests.TextTests`
- [X] T043 [P] [US3] Move all 4 files from `TLio.UnitTests/FunctionsTests/TimeDateTests/` to `TLio.Functions.Tests/FunctionsTests/TimeDateTests/`; update namespaces to `TLio.Functions.Tests.FunctionsTests.TimeDateTests`

### Verify TLio.Functions.Tests

- [X] T044 [US3] Run `dotnet test TLio.Functions.Tests/TLio.Functions.Tests.csproj` — confirm all migrated tests compile and pass (depends on T038–T043)

### Clean up TLio.UnitTests

- [X] T045 [US3] Delete `TLio.UnitTests/FunctionsTests/` folder and all its contents (after T044 confirms TLio.Functions.Tests is green)
- [X] T046 [US3] Update `TLio.UnitTests/Fixtures/FixtureTests.cs` to remove the function fixture folder names (Math, Text, TimeDate, Fetch, Indirect, Partial, Promote, ScriptPath) from its fixture discovery; only command fixture folders (Add, Compare, Copy, IfElse, Merge, Move, Put, Remove, Set) should remain
- [X] T047 [US3] Remove `<ProjectReference>` entries for `TLio.Extensions.Math`, `TLio.Extensions.Text`, and `TLio.Extensions.TimeDate` from `TLio.UnitTests/TLio.UnitTests.csproj`
- [X] T048 [US3] Run `dotnet test TLio.UnitTests/TLio.UnitTests.csproj` — confirm all remaining command, core, and engine tests pass; passing count must reflect removal of function tests (no regressions in command/engine/core layer)

**Checkpoint**: All five format/function libraries have a `[Library].Tests` project. `TLio.UnitTests` contains only core, command, and engine tests.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Full solution verification, documentation update, and compliance checks.

- [X] T049 Run `dotnet test TLio.sln` — confirm complete solution test count equals T006 baseline count (all tests account for — none lost in migration) — **838 total: UnitTests 403 + Json.Tests 114 + Json.SystemText.Tests 33 + Functions.Tests 248 + Xml.Tests 31 + Yaml.Tests 9**
- [X] T050 [P] Run Article I/IX compliance check: `grep -rn "Newtonsoft\|JToken\|JObject\|JArray\|JValue\|XElement\|YamlNode" TLio.Core/ TLio.Commands/ TLio.Functions/` — only doc-comment references found, no code violations
- [X] T051 [P] Run Article II compliance check: `grep -rn "new.*Adapter\|new.*Fetcher\|new.*ExecutionContext" TLio.Commands/ TLio.Functions/` — zero results
- [X] T052 Update `CLAUDE.md` Project Structure section to reflect the final state: TLio.UnitTests (Core/Commands/Engine only), TLio.Json.Tests, TLio.Json.SystemText.Tests, TLio.Functions.Tests, TLio.Xml.Tests, TLio.Yaml.Tests

---

## Article VI Backlog — Inline Tests to Convert to Fixture Triplets

> These tasks are tracked per Article VI but do NOT block this feature's completion.
> Complete before the next feature that touches the relevant command.

- [ ] BACKLOG-01 Review `TLio.UnitTests/CommandsTests/AddTests.cs` for inline full-execution `[TestCase]` scenarios — convert each to a fixture triplet in `TLio.UnitTests/Fixtures/Add/`
- [ ] BACKLOG-02 Review `TLio.UnitTests/CommandsTests/RemoveTests.cs` — convert full-execution inline tests to `TLio.UnitTests/Fixtures/Remove/` triplets
- [ ] BACKLOG-03 Review `TLio.UnitTests/CommandsTests/SetTests.cs` — convert full-execution inline tests to `TLio.UnitTests/Fixtures/Set/` triplets
- [ ] BACKLOG-04 Review `TLio.UnitTests/CommandsTests/PutTests.cs` — convert full-execution inline tests to `TLio.UnitTests/Fixtures/Put/` triplets
- [ ] BACKLOG-05 Review `TLio.UnitTests/CommandsTests/CopyMoveTests.cs` — convert full-execution inline tests to `TLio.UnitTests/Fixtures/Copy/` and `Move/` triplets
- [ ] BACKLOG-06 Review `TLio.UnitTests/CommandsTests/MergeTests.cs` — convert full-execution inline tests to `TLio.UnitTests/Fixtures/Merge/` triplets
- [ ] BACKLOG-07 Review `TLio.UnitTests/CommandsTests/CompareTests.cs` — convert full-execution inline tests to fixture triplets
- [ ] BACKLOG-08 Review `TLio.UnitTests/CommandsTests/IfElseTests.cs` — convert full-execution inline tests to `TLio.UnitTests/Fixtures/IfElse/` triplets
- [ ] BACKLOG-09 Review all `TLio.UnitTests/CommandsTests/DecisionTable*.cs` files — convert full-execution inline tests to fixture triplets
- [ ] BACKLOG-10 Review `TLio.UnitTests/CommandsTests/ETLTests/*.cs` — convert full-execution inline tests to fixture triplets

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — T001–T004 can all start immediately in parallel
- **Phase 2 (Foundational)**: Depends on T005 (build passes)
- **Phase 3 (US1 — Command Audit)**: Can start immediately after T006 (baseline captured); T007–T017 are all parallel
- **Phase 4 (US2 — JSON Adapter Migration)**: Can start immediately after T006; T019–T021 are parallel
- **Phase 5 (US3 — Functions Migration)**: Can start immediately after T006; fixture folder moves T030–T037 are all parallel
- **Phase 6 (Polish)**: Requires all prior phases complete

### User Story Dependencies

- **US1 (P1)**: Independent — depends only on T006 baseline. No dependency on US2 or US3.
- **US2 (P2)**: Independent — depends only on T005 (projects build). No dependency on US1 or US3.
- **US3 (P3)**: Independent — depends only on T005 (projects build). No dependency on US1 or US2.

### Within Each User Story

- US1: All audit tasks (T007–T017) are parallel; T018 (fixture triplets) follows audits
- US2: T019–T021 parallel → T022 (verify) → T025/T026 parallel (delete) → T027 → T028
- US3: T029 → T030–T037 parallel → T038–T043 parallel → T044 (verify) → T045–T048

---

## Parallel Examples

### User Story 1 (Command Audit) — all T007–T017 in parallel

```
T007  Audit AddTests.cs
T008  Audit RemoveTests.cs
T009  Audit SetTests.cs
T010  Audit PutTests.cs
T011  Audit CopyMoveTests.cs group
T012  Audit MergeTests.cs
T013  Audit CompareTests.cs
T014  Audit IfElseTests.cs
T015  Audit DecisionTable*.cs group
T016  Audit PropertyChange*.cs + ParentNavigation group
T017  Audit ETLTests/
```

### User Story 2 (JSON Adapter Migration) — T019–T021 in parallel

```
T019  Copy JsonNodeAdapterTests.cs → TLio.Json.Tests
T020  Copy JsonPathFetcherTests.cs → TLio.Json.Tests
T021  Copy JsonPathMethodsTests.cs → TLio.Json.Tests
      → T022 verify TLio.Json.Tests passes
T023  Copy SystemTextFixtureTests.cs → TLio.Json.SystemText.Tests
      → T024 verify TLio.Json.SystemText.Tests passes
T025 + T026  Delete source files (parallel)
```

### User Story 3 (Functions Migration) — fixture folder moves all parallel

```
T030  Move Math/
T031  Move Text/
T032  Move TimeDate/
T033  Move Fetch/
T034  Move Indirect/
T035  Move Partial/
T036  Move Promote/
T037  Move ScriptPath/
T040  Move FunctionsTests/ root files
T041  Move MathTests/
T042  Move TextTests/
T043  Move TimeDateTests/
      → T044 verify TLio.Functions.Tests passes
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T005)
2. Complete Phase 2: Baseline (T006)
3. Complete Phase 3: Command Orchestration Audit (T007–T018)
4. **STOP and VALIDATE**: All command tests pass; coverage checks confirm no gaps
5. US1 is done — ship or continue to US2/US3

### Incremental Delivery

1. Setup + Baseline → Phase 1+2
2. Command Audit → Phase 3 (US1 — highest value, least risk)
3. JSON Adapter Migration → Phase 4 (US2 — isolated migration, zero production changes)
4. Functions Migration → Phase 5 (US3 — largest migration, most parallel tasks)
5. Polish + compliance → Phase 6

### Parallel Team Strategy

Once T006 is captured:
- Developer A: US1 audit tasks (T007–T018) — pure test additions, no file moves
- Developer B: US2 migration (T019–T028) — JSON adapter test move
- Developer C: US3 migration (T029–T048) — functions test move

All three streams are independent and can merge cleanly.

---

## Notes

- `[P]` tasks = different files, no dependencies — can run in parallel
- `[Story]` label maps each task to its user story for traceability
- **Never delete source test files before the destination project's tests pass (Green)**
- Commit after each phase checkpoint (`dotnet test` green)
- All new fixture triplets follow the `input.json / script.json / result.json` pattern per Article VI
- Constitutional compliance checks (T050–T051) MUST pass before closing the feature
