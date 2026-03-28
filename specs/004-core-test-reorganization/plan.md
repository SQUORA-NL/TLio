# Implementation Plan: Core Test Coverage and Test Project Reorganization

**Branch**: `004-core-test-reorganization` | **Date**: 2026-03-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/004-core-test-reorganization/spec.md`

## Summary

Two related deliverables:

1. **Test project reorganization** — create three new test projects (`TLio.Json.Tests`, `TLio.Json.SystemText.Tests`, `TLio.Functions.Tests`) following the established `[Library].Tests` naming convention. Migrate the corresponding test files and fixture folders from `TLio.UnitTests` into their new homes. After migration, `TLio.UnitTests` retains only core, command, and engine tests.

2. **Command orchestration coverage audit** — audit each existing command test class in `CommandsTests/` against the three-point orchestration contract (path-not-found scenario, primary success path, command-specific edge case). Fill any gaps inline without rewriting existing tests.

---

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: NUnit 4.2.2, Microsoft.NET.Test.Sdk 17.12.0, NUnit3TestAdapter 4.6.0
**Storage**: N/A
**Testing**: NUnit 4 (same framework as all existing test projects)
**Target Platform**: Test assemblies (.NET 10)
**Project Type**: 3 new test `.csproj` files; no new production assemblies
**Performance Goals**: N/A — developer tooling
**Constraints**: All existing passing tests must continue to pass after reorganization; no new production code
**Scale/Scope**: 3 new projects; ~57 test files migrated; ~19 command test classes audited

---

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing a format-specific type (JToken, XElement, …)? | **No.** No production code in `TLio.Core`, `TLio.Commands`, or `TLio.Functions` changes. New test projects reference only their own format libraries. |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? No `new ConcreteAdapter()` in commands or functions? | **Yes.** No command/function code is altered; test helpers in test projects are exempt per Article II. |
| Generic-First | III | Does every new public API carry `<TNode>` as a generic parameter? No `object` in signatures? | **Yes.** No public production APIs are added or changed. |
| Process/Execution separation | IV | Do all node reads, type-checks, and mutations go through `context.NodeAdapter` or `context.ItemsFetcher`? No direct method calls on `TNode` variables? | **Yes.** No changes to command or function execution paths. |
| Swappable Selection | V | Are all path expressions supplied by callers? No path strings hard-coded inside commands or functions? | **Yes.** No path logic is altered. |
| Test-First + Fixture Triplets | VI | Will every full-script-execution test use file-based fixture triplets? Inline `[TestCase]` only for validation edge cases? | **Yes for new fixture tests.** Inline tests that predate this rule in `CommandsTests/` will be identified during the audit and tracked under "Refactor inline tests → fixture triplets" in `tasks.md`. |
| Simplicity Gate | VII | Could this be done with fewer projects and still satisfy Articles I–V? | **No simpler option exists.** The three new projects are the minimum required by the `[Library].Tests` convention. Combining `TLio.Json.Tests` and `TLio.Json.SystemText.Tests` was explicitly rejected in clarification Q1. The alternative (keeping all tests in `TLio.UnitTests`) satisfies no principle and contradicts the spec's stated consistency requirement. |
| No Leaking Internals | IX | Do `TLio.Core` public APIs expose only `TNode`-parameterised types? No format types in `Contracts/` or `Models/`? | **Yes.** No changes to `TLio.Core`. |
| Logging as Observability | X | Does every `Execute()` path call `LogInfo` on success and `LogWarning` on graceful skips? No exceptions thrown for expected conditions? | **No new `Execute()` methods added.** The command orchestration audit (Phase 1) will flag any existing command test that lacks an assertion for logging behaviour and add it as a gap. |

---

## Project Structure

### Documentation (this feature)

```text
specs/004-core-test-reorganization/
├── plan.md           ← this file
├── research.md       ← Phase 0 output
├── data-model.md     ← Phase 1 output (N/A — no new entities)
└── tasks.md          ← /speckit.tasks output (NOT created here)
```

### New test projects

```text
TLio.Json.Tests/
├── TLio.Json.Tests.csproj
│     refs: TLio.Json, TLio.Client, NUnit 4.2.2, NUnit3TestAdapter, Microsoft.NET.Test.Sdk
└── AdapterTests/
    ├── JsonNodeAdapterTests.cs     ← MOVED from TLio.UnitTests/AdapterTests/
    ├── JsonPathFetcherTests.cs     ← MOVED from TLio.UnitTests/AdapterTests/
    └── JsonPathMethodsTests.cs     ← MOVED from TLio.UnitTests/AdapterTests/

TLio.Json.SystemText.Tests/
├── TLio.Json.SystemText.Tests.csproj
│     refs: TLio.Json.SystemText, TLio.Client, NUnit 4.2.2, NUnit3TestAdapter, Microsoft.NET.Test.Sdk
└── SystemTextTests/
    └── SystemTextFixtureTests.cs   ← MOVED from TLio.UnitTests/SystemTextTests/

TLio.Functions.Tests/
├── TLio.Functions.Tests.csproj
│     refs: TLio.Functions, TLio.Json, TLio.Client,
│           TLio.Extensions.Math, TLio.Extensions.Text,
│           TLio.Extensions.TimeDate, NUnit 4.2.2, NUnit3TestAdapter,
│           Microsoft.NET.Test.Sdk
├── FunctionsTests/                 ← MOVED from TLio.UnitTests/FunctionsTests/ (all 54 files)
│   ├── DatetimeFunctionTests.cs
│   ├── FetchTests.cs
│   ├── IndirectTests.cs
│   ├── PartialTests.cs
│   ├── PromoteTests.cs
│   ├── ScriptPathTests.cs
│   ├── MathTests/     (28 files)
│   ├── TextTests/     (16 files)
│   └── TimeDateTests/ (4 files)
├── Fixtures/                       ← function fixture triplets
│   ├── FixtureTests.cs             ← NEW: fixture loader scoped to function fixtures
│   ├── FixtureTheoryLoader.cs      ← COPIED/ADAPTED from TLio.UnitTests/Fixtures/
│   ├── ExtensionFixtureTests.cs    ← MOVED from TLio.UnitTests/Fixtures/
│   ├── Math/                       ← MOVED from TLio.UnitTests/Fixtures/Math/
│   ├── Text/                       ← MOVED from TLio.UnitTests/Fixtures/Text/
│   ├── TimeDate/                   ← MOVED from TLio.UnitTests/Fixtures/TimeDate/
│   ├── Fetch/                      ← MOVED from TLio.UnitTests/Fixtures/Fetch/
│   ├── Indirect/                   ← MOVED from TLio.UnitTests/Fixtures/Indirect/
│   ├── Partial/                    ← MOVED from TLio.UnitTests/Fixtures/Partial/
│   ├── Promote/                    ← MOVED from TLio.UnitTests/Fixtures/Promote/
│   └── ScriptPath/                 ← MOVED from TLio.UnitTests/Fixtures/ScriptPath/
```

### `TLio.UnitTests` after migration

```text
TLio.UnitTests/
├── TLio.UnitTests.csproj
│     refs: TLio.Core, TLio.Commands, TLio.Functions, TLio.Json, TLio.Client,
│           TLio.Extensions.ETL, NUnit 4.2.2, NUnit3TestAdapter,
│           Microsoft.NET.Test.Sdk
│     REMOVED refs: TLio.Json.SystemText, TLio.Extensions.Math,
│                   TLio.Extensions.Text, TLio.Extensions.TimeDate
├── CommandsTests/        ← ALL KEPT (19 files incl. ETLTests/)
├── CoreTests/            ← ALL KEPT (ArchitectureTests, ValuePipelineTests)
├── EngineTests/          ← ALL KEPT (4 files)
└── Fixtures/
    ├── FixtureTests.cs          ← UPDATED: scope reduced to command fixtures only
    ├── FixtureTheoryLoader.cs   ← KEPT (shared utility)
    ├── Add/, Compare/, Copy/,   ← KEPT (command fixture triplets)
    ├── IfElse/, Merge/, Move/,
    ├── Put/, Remove/, Set/
    # REMOVED: AdapterTests/, SystemTextTests/, FunctionsTests/
    # MOVED OUT: Fixtures/Math/, Text/, TimeDate/, Fetch/,
    #             Indirect/, Partial/, Promote/, ScriptPath/
    #             ExtensionFixtureTests.cs → TLio.Functions.Tests
```

### Solution file updates

```text
TLio.sln
  ADD: TLio.Json.Tests
  ADD: TLio.Json.SystemText.Tests
  ADD: TLio.Functions.Tests
  (no projects removed — TLio.UnitTests stays)
```

---

## Phase 0 — Research

No technical unknowns exist for this feature. The following decisions are pre-resolved:

### Decision: Reference implementation for new test projects

| Question | Decision | Rationale |
|---|---|---|
| What is the correct project template for new test projects? | Mirror `TLio.Xml.Tests.csproj` and `TLio.Yaml.Tests.csproj` structure | They already demonstrate the correct `[Library].Tests` pattern with NUnit 4, NUnit3TestAdapter, and Microsoft.NET.Test.Sdk. No additional packages needed. |
| Should TLio.Functions.Tests also reference Newtonsoft.Json directly? | No — reference `TLio.Json` instead | `TLio.Functions` uses `TNode`-generic interfaces; test fixtures use JSON format, so `TLio.Json` (which wraps Newtonsoft.Json) is the right adapter reference. Directly referencing Newtonsoft.Json would violate Article I in the test project intent. |
| How should the fixture theory loader be shared between TLio.UnitTests and TLio.Functions.Tests? | Each project gets its own `FixtureTheoryLoader.cs` scoped to its fixture root directory | No shared assembly is introduced; the loader is a simple file-based utility (~30 lines). Extracting it into a shared project would add complexity with no benefit (Simplicity Gate). |
| What project does TLio.Functions.Tests reference for ETL functions? | It does not — ETL tests (FlattenRestore, Resolve) stay in TLio.UnitTests/CommandsTests/ETLTests/ | ETL commands are orchestrators in `TLio.Extensions.ETL`; their tests belong with command tests, not function tests. |

### Decision: Command orchestration audit approach

| Question | Decision | Rationale |
|---|---|---|
| What defines "orchestration coverage" for a command test? | Each command MUST have at minimum: (1) a passing test with valid path, (2) a test where the path resolves to zero nodes, (3) one command-specific edge case | Derived from FR-001/FR-002. Mirrors the three-step "Find→Compute→Operate" contract in Article IV. |
| Should new command tests use fixture triplets? | Yes — any net-new full-execution scenario added during the audit MUST use fixture triplets per Article VI | Inline `[TestCase]` is only for validation edge cases (null path, empty path string, argument parsing). |
| What about existing inline command tests? | Track in `tasks.md` under "Refactor inline tests → fixture triplets"; not required to block this feature | Article VI exempts pre-existing inline tests from immediate refactor, but they must be tracked. |

---

## Phase 1 — Design

### Data Model

No new domain entities. This feature creates test infrastructure only.

The contracts under test are the existing interfaces:

- `INodeAdapter<TNode>` — all members must have at least one test in each format's test project
- `IItemsFetcher<TNode>` — all members must have at least one test in each format's test project

### Interface coverage map for TLio.Json.Tests

Every member of `INodeAdapter<JToken>` and `IItemsFetcher<JToken>` already has tests in the existing `JsonNodeAdapterTests.cs`, `JsonPathFetcherTests.cs`, and `JsonPathMethodsTests.cs`. Migration is a file move; no new test methods are required for this project unless the audit reveals gaps.

### Interface coverage map for TLio.Json.SystemText.Tests

`SystemTextFixtureTests.cs` covers behavioral equivalence to the Newtonsoft adapter via fixture triplets. Migration is a file move.

### Interface coverage map for TLio.Functions.Tests

All 54 existing function test files move as-is. No new tests are required at this stage unless the command audit reveals function-level gaps.

### Command orchestration audit plan

For each of the 19 command test classes, verify:

| Check | What to look for | Gap signal |
|---|---|---|
| Path-not-found | A test where the configured path matches zero nodes and the command completes without exception | No such test exists |
| Primary success | At least one test that verifies the document mutation result is correct | Covered by most existing tests |
| Edge case | One scenario unique to the command's semantics | Varies per command |
| Logging assertion | Test asserts `LogInfo` on success or `LogWarning` on skip (Article X) | No logging assertion present |

Commands known to need attention based on Article X (no logging assertions in existing tests):
- All commands — logging assertions are not standard in current test suite; add at minimum one per command.

---

## Complexity Tracking

| Decision | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Three new test projects instead of one | `[Library].Tests` naming convention + clarifications Q1 (separate System.Text.Json) and Q3 (Functions in scope) | A single combined `TLio.Adapter.Tests` project would obscure which library a failing test belongs to and violate the stated naming convention |
| FixtureTheoryLoader duplicated into TLio.Functions.Tests | Each test project must be self-contained | A shared helper assembly would add a fourth new project (worse complexity), and the utility is too small to warrant it |
| TLio.Functions reference retained in TLio.UnitTests | ETL command tests (FlattenRestore, Resolve) rely on `TLio.Functions` execution pipeline | Not removable without breaking ETL command tests that remain in TLio.UnitTests |
