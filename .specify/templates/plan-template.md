# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]  
**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]  
**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]  
**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]  
**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]
**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]  
**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]  
**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]  
**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
*Source: `specs/constitution.md` and `.specify/memory/constitution.md`.*

Answer each gate explicitly. "Yes/No + one sentence" is enough.

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing a format-specific type (JToken, XElement, …)? | |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? No `new ConcreteAdapter()` in commands or functions? | |
| Generic-First | III | Does every new public API carry `<TNode>` as a generic parameter? No `object` in signatures? | |
| Process/Execution separation | IV | Do all node reads, type-checks, and mutations go through `context.NodeAdapter` or `context.ItemsFetcher`? No direct method calls on `TNode` variables? | |
| Swappable Selection | V | Are all path expressions supplied by callers? No path strings hard-coded inside commands or functions? | |
| Test-First + Fixture Triplets | VI | Will every full-script-execution test use file-based fixture triplets (`input.json / script.json / result.json`)? Inline `[TestCase]` only for validation edge cases? | |
| Simplicity Gate | VII | Could this be done with fewer projects/layers and still satisfy Articles I–V? (If yes, explain why the simpler option was rejected.) | |
| No Leaking Internals | IX | Do `TLio.Core` public APIs expose only `TNode`-parameterised types? No format types in `Contracts/` or `Models/`? | |
| Logging as Observability | X | Does every `Execute()` path call `LogInfo` on success and `LogWarning` on graceful skips? No exceptions thrown for expected conditions? | |
| AI Component Reference | XI | Does every new command, function, and adapter have a corresponding `ai-ref.md` under `docs/ai-ref/`? Does `docs/ai-ref/overview.md` exist with the adapter selection and JSONPath compatibility tables? | |

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
