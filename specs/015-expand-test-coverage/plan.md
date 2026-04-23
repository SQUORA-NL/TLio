# Implementation Plan: Expand Test Coverage with Performance Tests

**Branch**: `015-expand-test-coverage` | **Date**: 2026-04-23 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `specs/015-expand-test-coverage/spec.md`

## Summary

Add at least 100 new test cases to the six existing test projects, targeting the three completely untested adapters (`XmlNodeAdapter`, `YamlNodeAdapter`, `SystemTextJsonNodeAdapter`), two untested fetchers (`SlashPathItemsFetcher`, `YamlPathItemsFetcher`), malformed/null edge cases across all projects, performance baselines for JSON/XML/YAML path evaluation and batch command execution, and expanded TimeDate/ETL function coverage — all using NUnit and the existing GC-allocation measurement pattern, with no new projects or frameworks introduced.

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: NUnit 4.x, Newtonsoft.Json (TLio.Json.Tests), System.Text.Json (TLio.Json.SystemText.Tests), YamlDotNet (TLio.Yaml.Tests), System.Xml (TLio.Xml.Tests)  
**Storage**: N/A — test fixtures are file-based (input/script/result triplets) or programmatically generated  
**Testing**: NUnit — `dotnet test`  
**Target Platform**: Developer workstation / CI agent (.NET 10)  
**Project Type**: Test-only expansion — no new production projects or assemblies  
**Performance Goals**: Each new performance test must pass on a typical developer machine; thresholds committed as constants in source  
**Constraints**: No new NuGet packages; no BenchmarkDotNet; performance measurement via `GC.GetAllocatedBytesForCurrentThread()` and `Stopwatch` (consistent with existing `CompiledScript_PerformanceTests.cs`)  
**Scale/Scope**: ~547 → ≥ 650 test methods across 6 existing test projects

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing a format-specific type? | **N/A** — this feature adds tests only; adapter test projects are explicitly permitted to reference format-specific types (Newtonsoft, System.Xml, YamlDotNet). No Core/Commands/Functions code is changed. |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? | **N/A** — no new commands or functions introduced. Test helpers that `new` up adapters are exempt (Article II footnote). |
| Generic-First | III | Does every new public API carry `<TNode>`? | **N/A** — no new public APIs. |
| Process/Execution separation | IV | Do all node reads go through `context.NodeAdapter`? | **N/A** — test code is not subject to this rule; tests call adapters directly to verify their contracts. |
| Swappable Selection | V | Are all path expressions supplied by callers? | **N/A** — no commands or functions changed. |
| Test-First + Fixture Triplets | VI | Will every full-script-execution test use file-based fixture triplets? | **Yes** — any new test that exercises a full TLio script execution will use fixture triplets in the corresponding test project. Unit-level adapter/fetcher tests and performance tests use inline data as permitted (edge-case / validation exceptions). |
| Simplicity Gate | VII | Could this be done with fewer projects? | **Yes — and it is**: no new projects are created. All additions land in the 6 existing test projects. |
| Backward Migration Path | VIII | Are any JLio-equivalent behaviours changed? | **No** — no production code changes. |
| No Leaking Internals | IX | Do Core public APIs expose format types? | **N/A** — no Core changes. |
| Logging as Observability | X | Does every Execute() path log appropriately? | **N/A** — no Execute() paths changed. |
| AI Component Reference | XI | Does every new command/function/adapter have ai-ref.md? | **N/A** — no new commands, functions, or adapters introduced. |

**Constitution result: PASS — no violations. Ready to proceed.**

## Project Structure

### Documentation (this feature)

```text
specs/015-expand-test-coverage/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
└── tasks.md             ← Phase 2 output (/speckit.tasks)
```

### Source Code (test projects only — no production changes)

```text
TLio.Xml.Tests/
├── Adapters/
│   └── XmlNodeAdapterTests.cs             ← NEW (FR-001)
├── Fetchers/
│   └── SlashPathItemsFetcherTests.cs      ← NEW (FR-002)
├── EdgeCases/
│   └── XmlMalformedInputTests.cs          ← NEW (FR-004)
└── Performance/
    └── XmlPath_PerformanceTests.cs        ← NEW (FR-005)

TLio.Yaml.Tests/
├── Adapters/
│   └── YamlNodeAdapterTests.cs            ← NEW (FR-001)
├── Fetchers/
│   └── YamlPathItemsFetcherTests.cs       ← NEW (FR-002)
├── EdgeCases/
│   └── YamlMalformedInputTests.cs         ← NEW (FR-004)
└── Performance/
    └── YamlPath_PerformanceTests.cs       ← NEW (FR-005)

TLio.Json.SystemText.Tests/
├── Adapters/
│   └── SystemTextJsonNodeAdapterTests.cs  ← NEW (FR-001)
├── EdgeCases/
│   └── SystemTextJsonEdgeCaseTests.cs     ← NEW (FR-004)
└── Performance/
    └── JsonPath_PerformanceTests.cs       ← NEW (FR-005, extends existing)

TLio.Json.Tests/
└── EdgeCases/
    └── JsonEdgeCaseTests.cs               ← NEW (FR-004)

TLio.Functions.Tests/
├── TimeDateTests/
│   └── TimeDate_ExtendedTests.cs          ← NEW (FR-008, ≥20 cases)
└── ETLTests/
    └── ETL_Tests.cs                       ← NEW (FR-008, ≥15 cases)

TLio.UnitTests/
└── Performance/
    └── CommandEngine_PerformanceTests.cs  ← NEW (FR-005, batch 500 commands)
```

**Structure Decision**: All new files land in the 6 existing test projects — exactly matching the Article VI layer-to-project mapping. New subdirectories (`Adapters/`, `Fetchers/`, `EdgeCases/`, `Performance/`) follow the existing convention of grouping tests by concern within a project.

## Complexity Tracking

No constitution violations — complexity tracking not required.
