# Research: Core Test Coverage and Test Project Reorganization

**Feature**: `004-core-test-reorganization` | **Date**: 2026-03-28

No NEEDS CLARIFICATION markers were present in the Technical Context. All decisions
below are derived from existing project patterns and the clarifications resolved
during `/speckit.clarify`.

---

## Decision 1: New test project structure

**Decision**: Mirror `TLio.Xml.Tests` and `TLio.Yaml.Tests` exactly for the three
new projects (`TLio.Json.Tests`, `TLio.Json.SystemText.Tests`, `TLio.Functions.Tests`).

**Rationale**: These two projects are the established reference implementations.
Both use NUnit 4.2.2, NUnit3TestAdapter 4.6.0, Microsoft.NET.Test.Sdk 17.12.0,
and reference only their own adapter library plus `TLio.Client`. Reusing this
pattern requires no new decisions and no new packages.

**Alternatives considered**: A single combined `TLio.JsonAdapter.Tests` project
was rejected in clarification Q1 (violates `[Library].Tests` naming convention).

---

## Decision 2: TLio.Functions.Tests adapter reference

**Decision**: `TLio.Functions.Tests` references `TLio.Json` (not Newtonsoft.Json
directly) as its test execution adapter.

**Rationale**: All existing function fixture triplets use JSON as the document
format. `TLio.Json` wraps Newtonsoft.Json and exposes the adapter through
`JsonExecutionContext.CreateDefault()`. Referencing Newtonsoft.Json directly from
a test project would violate the spirit of Article I — format libraries are
accessed through their TLio adapter wrappers.

**Alternatives considered**: Referencing `Newtonsoft.Json` directly is simpler but
inconsistent with how every other test project in the solution is structured.

---

## Decision 3: FixtureTheoryLoader duplication

**Decision**: Copy `FixtureTheoryLoader.cs` into `TLio.Functions.Tests`; do not
extract a shared utility assembly.

**Rationale**: The loader is approximately 30 lines. A shared assembly adds a
fourth new project (more complexity, not less — Simplicity Gate). The slight
duplication is acceptable at this scale.

**Alternatives considered**: A `TLio.TestUtils` shared project was considered and
rejected on Simplicity Gate grounds.

---

## Decision 4: ETL command tests location

**Decision**: ETL command tests (`FlattenRestoreTests.cs`, `ResolveTests.cs`) stay
in `TLio.UnitTests/CommandsTests/ETLTests/`.

**Rationale**: ETL commands (`TLio.Extensions.ETL`) are orchestrator commands, not
functions. They belong with command tests. Moving them to `TLio.Functions.Tests`
would be semantically incorrect.

---

## Decision 5: Command orchestration audit scope

**Decision**: Audit all 19 command test classes for three gaps: (1) path-not-found
scenario, (2) primary success path, (3) logging assertion (Article X). Fill gaps
inline; do not rewrite.

**Rationale**: Clarification Q2 confirmed audit-and-fill approach. Full rewrite
introduces regression risk with no coverage benefit. Inline additions preserve the
existing test structure and institutional knowledge.

**Alternatives considered**: A new `CommandOrchestrationTests.cs` layer was
rejected in Q2 — it would duplicate existing test intent and add maintenance overhead.
