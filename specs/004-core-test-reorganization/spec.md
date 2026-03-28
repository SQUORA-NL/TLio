# Feature Specification: Core Test Coverage and Test Project Reorganization

**Feature Branch**: `004-core-test-reorganization`
**Created**: 2026-03-28
**Status**: Draft
**Input**: User description: "specify a new spec that al core elements needs to be tested fully on the core principals like the commands being an orchestrtator and doing hte rright thing, as well as the manipulators/adapters .these need to be tested on their functionality. these need to stay in the tlio unittests, the json test need to go in their own project so there is consistency on the projects and naming as well as where to find test fo each project"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Verify Command Orchestration Behavior (Priority: P1)

A developer making changes to TLio needs confidence that each command correctly performs its orchestration role: locating the right source nodes via the path fetcher, applying the correct transformation logic, and persisting results via the node adapter. Tests must verify the full read-orchestrate-write cycle for each command, covering both primary flows and edge conditions.

**Why this priority**: Commands are the primary integration boundary in TLio. A bug in command orchestration silently affects every format and document type. Testing command behavior at the core level provides the earliest and most reliable signal of regressions.

**Independent Test**: Can be tested by running TLio.UnitTests with a set of input documents, command configurations, and expected output documents — no external dependencies required.

**Acceptance Scenarios**:

1. **Given** a document with known structure, **When** a command is executed with valid path and value parameters, **Then** the document is modified exactly as specified and the result indicates success.
2. **Given** a command with a path that matches no nodes, **When** the command is executed, **Then** the command completes without error and produces a documented outcome (no-op or logged diagnostic).
3. **Given** a script containing multiple commands, **When** the script is executed, **Then** each command runs in the declared sequence and all mutations are applied cumulatively to produce the final document.
4. **Given** a conditional command (IfElse), **When** the condition evaluates to true, **Then** the true-branch commands are executed; **When** it evaluates to false, **Then** the false-branch commands are executed.

---

### User Story 2 - Verify Adapter and Fetcher Functional Correctness (Priority: P2)

A developer working on a format-specific library needs a comprehensive test suite that verifies every operation defined by the node-adapter and path-fetcher contracts. These tests must live in a dedicated project named after their format library, so failures are immediately traceable to the correct library.

**Why this priority**: Adapter and fetcher correctness is a prerequisite for command correctness. Co-locating tests with their library makes the test ownership unambiguous and enables each format library to be developed and verified in isolation.

**Independent Test**: Can be tested by running the format-specific test project in isolation and confirming all adapter and fetcher contract operations pass.

**Acceptance Scenarios**:

1. **Given** a JSON document, **When** all node-manipulation operations are exercised through the JSON adapter, **Then** each operation produces the correct structural change and the document remains valid.
2. **Given** a path expression, **When** the path fetcher resolves it against a document, **Then** the correct set of nodes is returned for all supported path patterns (simple, wildcard, recursive, filtered).
3. **Given** the System.Text.Json adapter and the Newtonsoft.Json adapter, **When** identical operations are applied to equivalent documents, **Then** the behavioral results are equivalent.

---

### User Story 3 - Consistent Test Project Layout (Priority: P3)

A developer navigating the solution needs to locate the tests for any given library in under ten seconds by applying a single predictable rule: each library has a corresponding test project named `[Library].Tests`. Currently JSON adapter tests break this rule by residing inside TLio.UnitTests alongside unrelated core tests.

**Why this priority**: Discoverability and maintainability. Once the pattern is consistent across all format libraries, adding tests for a new library requires no decisions about where tests should go.

**Independent Test**: Can be verified by confirming that TLio.Json.Tests exists with all JSON adapter tests, TLio.Functions.Tests exists with all function tests, and TLio.UnitTests contains only core, command, and engine tests.

**Acceptance Scenarios**:

1. **Given** the solution, **When** a developer looks for JSON adapter tests, **Then** they are found in TLio.Json.Tests — not in TLio.UnitTests.
2. **Given** TLio.UnitTests, **When** its test contents are reviewed, **Then** only core, command, and engine tests are present; no adapter tests and no function tests remain.
3. **Given** the libraries TLio.Json, TLio.Json.SystemText, TLio.Xml, TLio.Yaml, and TLio.Functions, **When** the solution is opened, **Then** each has a corresponding `[Library].Tests` project present in the solution.

---

### Edge Cases

- What outcome is expected when a command receives an empty document?
- How does each command behave when the target path resolves to zero nodes?
- What happens when two commands in a script write to the same path?
- What is the expected result when an adapter operation receives a null or structurally invalid node?
- How should tests that use JSON documents as test data (but are testing command behavior) be categorized during migration?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each command (Add, Remove, Set, Put, Copy, Move, Merge, Compare, IfElse, DecisionTable) MUST have tests that verify its orchestration contract: it correctly selects source nodes, correctly delegates to the adapter, and produces the right output document. Existing tests in CommandsTests MUST be audited and any gaps filled inline; a full rewrite is not required.
- **FR-002**: Command tests MUST cover the primary success path, a path-not-found scenario, and at least one edge condition specific to the command's semantics (e.g., merging into an existing value, comparing unequal nodes).
- **FR-003**: A dedicated TLio.Json.Tests project MUST be created, containing all functional tests for the INodeAdapter and IItemsFetcher implementations in TLio.Json (Newtonsoft.Json).
- **FR-004**: A dedicated **TLio.Json.SystemText.Tests** project MUST be created, containing behavioral tests for the System.Text.Json adapter.
- **FR-005**: All JSON adapter and path-fetcher tests currently residing in TLio.UnitTests MUST be migrated to the appropriate dedicated JSON test project without any loss of coverage.
- **FR-006**: After migration, TLio.UnitTests MUST contain only core, command, and engine tests — adapter tests and function tests will have moved to their dedicated projects.
- **FR-007**: Each library (TLio.Json, TLio.Json.SystemText, TLio.Xml, TLio.Yaml, TLio.Functions) MUST have a corresponding dedicated test project following the naming convention `[Library].Tests`.
- **FR-009**: A dedicated TLio.Functions.Tests project MUST be created, containing all function tests migrated from TLio.UnitTests, without loss of coverage.
- **FR-008**: The complete test suite MUST pass after reorganization with no regressions, no skipped tests, and no coverage gaps introduced by the migration.

### Key Entities

- **Command**: An orchestration unit that reads nodes from a document via a path fetcher, applies transformation logic, and writes results via a node adapter. Examples: Add, Copy, Merge, IfElse.
- **Node Adapter (INodeAdapter)**: The format-specific implementation of all structural node operations: create, read, update, delete, clone, merge, and type-inspection operations.
- **Path Fetcher (IItemsFetcher)**: The format-specific implementation of path resolution, returning a set of matching nodes from a document given a path expression.
- **Execution Context**: The runtime binding that associates a specific node adapter and path fetcher with a document for the duration of script execution.
- **Test Project**: A dedicated test project responsible for one library, named `[Library].Tests`. Each format library has exactly one corresponding test project.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every command has tests covering its primary orchestration flow, its path-not-found behavior, and at least one edge case — zero commands have zero tests.
- **SC-002**: Every operation defined in the INodeAdapter and IItemsFetcher contracts is exercised by at least one test in the format's dedicated test project.
- **SC-003**: Zero JSON-adapter-specific tests and zero function tests remain in TLio.UnitTests after migration is complete.
- **SC-004**: A developer can identify which test project covers a given library in under 10 seconds by reading project names in the solution.
- **SC-005**: The full test suite passes after reorganization with no regressions compared to the pre-migration baseline.

## Clarifications

### Session 2026-03-28

- Q: Where should System.Text.Json adapter tests live — separate TLio.Json.SystemText.Tests project or combined within TLio.Json.Tests? → A: Separate TLio.Json.SystemText.Tests project (follows `[Library].Tests` naming convention exactly).
- Q: Command test coverage approach — audit and fill gaps in existing tests, new orchestration layer, or full rewrite? → A: Audit existing CommandsTests and fill identified gaps inline.
- Q: Should the `[Library].Tests` consistency rule extend to TLio.Functions (in scope) or stay limited to format libraries (out of scope)? → A: In scope — create TLio.Functions.Tests and migrate FunctionsTests from TLio.UnitTests.

## Assumptions

- TLio.Json.Tests, TLio.Json.SystemText.Tests, and TLio.Functions.Tests are new projects to be created as part of this feature.
- Existing tests in TLio.Xml.Tests and TLio.Yaml.Tests are correct and complete; they are not being changed by this feature.
- Command tests that happen to use a JSON document as their test data remain in TLio.UnitTests, provided they are testing command behavior — not adapter behavior.
- Extension library tests (TLio.Extensions.Math, TLio.Extensions.Text, TLio.Extensions.TimeDate, TLio.Extensions.ETL) are out of scope for this feature; only TLio.Functions (the core functions library) is in scope.
- System.Text.Json adapter tests live in a dedicated TLio.Json.SystemText.Tests project (separate from TLio.Json.Tests), consistent with the `[Library].Tests` naming convention.
