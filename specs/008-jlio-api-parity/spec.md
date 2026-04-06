# Feature Specification: JLio API Parity

**Feature Branch**: `008-jlio-api-parity`
**Created**: 2026-04-06
**Status**: Draft
**Reference**: `JLio_AI_Reference.md` (JLio complete reference)

## Context

TLio is the format-agnostic successor to JLio. To enable consumers to migrate from JLio
to TLio without rewriting their scripts, and for AI agents to produce correct TLio scripts
using JLio documentation as a mental model, TLio must align its property names, function
signatures, and C# API with JLio wherever format-agnosticism allows.

---

## User Scenarios & Testing

### User Story 1 — Property Name Alignment (Priority: P1)

A developer migrating a JLio script to TLio copies the JSON script verbatim. When the
script uses `compare`, `merge`, `decisionTable`, or any ETL command, TLio accepts the
same property names that JLio uses without requiring changes.

**Why this priority**: Property name mismatches silently produce wrong output — a script
runs but does nothing because `"fromPath"` is silently ignored when TLio expects
`"firstPath"`. This is the highest-friction migration pain point.

**Independent Test**: A JLio script using all affected commands
(`compare`, `merge`, `decisionTable`, `flatten`, `restore`, `resolve`, `toCsv`) runs
correctly on TLio without any property renaming.

**Acceptance Scenarios**:

1. **Given** a JLio script `{"command":"compare","fromPath":"$.a","toPath":"$.b","resultPath":"$.r"}`,
   **When** executed in TLio, **Then** the comparison runs (TLio currently requires
   `firstPath`/`secondPath`).
2. **Given** a JLio script `{"command":"merge","fromPath":"$.src","toPath":"$.dst"}`,
   **When** executed in TLio, **Then** the merge runs (TLio currently requires
   `path`/`targetPath`).
3. **Given** a JLio script with `"decisionTable": { "inputs": [...] }` (config under the
   `decisionTable` key), **When** executed in TLio, **Then** the table evaluates correctly
   (TLio currently requires the key `config`).
4. **Given** JLio ETL scripts using `flattenSettings`, `restoreSettings`, `resolveSettings`,
   `csvSettings`, **When** executed in TLio, **Then** the settings are applied correctly
   (TLio currently uses the generic `settings` key for all four).

### Edge Cases

- Both old TLio property names AND new JLio-aligned names should be accepted (backward
  compatibility — existing TLio scripts must continue to work).
- When both old and new names are present in the same command object, the JLio-aligned
  name takes precedence.

---

### User Story 2 — Missing Core Functions (Priority: P1)

A developer writing a TLio script uses `=newGuid()`, `=fetch($.path, 'default')`,
`=path()`, or `=promote($.value, 'name')` as in JLio. TLio evaluates these correctly.

**Why this priority**: These functions appear in nearly every JLio script. Their absence
in TLio means every migrated script must be rewritten.

**Independent Test**: A script using `=newGuid()`, `=fetch($.path, 'fallback')`,
`=path()`, and `=promote($.value, 'wrapper')` produces the same output as the equivalent
JLio script.

**Acceptance Scenarios**:

1. **Given** `{"command":"add","path":"$.id","value":"=newGuid()"}`, **When** executed,
   **Then** `$.id` contains a non-empty UUID string.
2. **Given** `{"command":"add","path":"$.name","value":"=fetch($.user.name,'Unknown')"}`,
   **When** `$.user.name` does not exist, **Then** `$.name` = `"Unknown"`.
3. **Given** `{"command":"add","path":"$.items[*].loc","value":"=path()"}`, **When**
   executed, **Then** each item's `loc` field contains that item's JSONPath string.
4. **Given** `{"command":"add","path":"$.wrapped","value":"=promote($.rawValue,'data')"}`,
   **When** `$.rawValue` is `42`, **Then** `$.wrapped` = `{"data": 42}`.

---

### User Story 3 — Fluent Builder API (Priority: P2)

A C# developer builds a TLio script using a fluent API that mirrors JLio's
`new JLioScript().Add(value).OnPath("$.path").Set(value).OnPath("$.path")` pattern.

**Why this priority**: Many JLio consumers build scripts in code using the fluent API.
Providing the same API pattern eliminates the need to rewrite script-building code.

**Independent Test**: C# code using the TLio fluent API produces and executes scripts
that yield the same result as equivalent JSON scripts.

**Acceptance Scenarios**:

1. **Given** fluent code `new TLioScript<JToken>().Add(new JValue("hello")).OnPath("$.greeting")`,
   **When** executed against `{}`, **Then** result = `{"greeting":"hello"}`.
2. **Given** a chain `.Set(...).OnPath(...).Remove().OnPath(...).Copy().From(...).To(...)`,
   **When** executed, **Then** all operations apply in order.
3. **Given** `TLioConvert.Parse<JToken>(scriptText)` static method, **When** called with
   a valid JSON script string, **Then** it returns a parsed `TLioScript<JToken>` ready to execute.

---

### User Story 4 — Text Extension Functions (Priority: P2)

A developer uses `=concat(...)`, `=toString(...)`, `=parse(...)`, `=format(...)`,
`=length(...)`, `=substring(...)`, `=replace(...)`, `=toLower(...)`, `=toUpper(...)`,
`=trim(...)` in TLio scripts, registered via a text extension pack.

**Why this priority**: Text manipulation is required in virtually all real-world data
transformation scripts. JLio provides these via `JLio.Extensions.Text`.

**Independent Test**: A script using all 10 text functions against string data in each
supported format (JSON, XML, YAML) produces expected string results.

**Acceptance Scenarios**:

1. **Given** `=concat($.first,' ',$.last)`, **When** executed, **Then** returns the
   concatenated full name string.
2. **Given** `=toLower($.email)`, **When** executed, **Then** returns the lowercase email.
3. **Given** `=substring($.code,0,3)`, **When** executed, **Then** returns the first 3
   characters.
4. **Given** `=length($.name)`, **When** executed, **Then** returns the character count.

---

### User Story 5 — AI Reference Format Alignment (Priority: P3)

An AI agent using the TLio `docs/ai-ref/` documentation to generate scripts receives
the same quality of guidance as from the JLio reference: each command/function file
includes an **Intent** line, a **Supports functions** flag, **C# Fluent API** examples,
and JLio-compatible property names throughout.

**Why this priority**: The JLio reference format has proven effective. Updating TLio's
ai-ref.md files to match it makes TLio documentation immediately useful to developers
already familiar with JLio.

**Independent Test**: Given only `docs/ai-ref/`, an AI agent correctly generates TLio
scripts including C# fluent builder code without consulting any other source.

**Acceptance Scenarios**:

1. **Given** `docs/ai-ref/commands/Set.md`, **When** read by an AI agent, **Then** the
   agent produces both a JSON script example and a correct C# fluent equivalent.
2. **Given** `docs/ai-ref/commands/Compare.md`, **When** read, **Then** the doc shows
   `fromPath`/`toPath` (JLio-aligned) as the primary property names.
3. **Given** `docs/ai-ref/functions/Fetch.md`, **When** read, **Then** the doc shows
   both `=fetch(path)` and `=fetch(path, defaultValue)` signatures.

---

## Requirements

### Functional Requirements

**Property Name Alignment (US1)**

- **FR-001**: The `compare` command MUST accept `fromPath`/`toPath` as aliases for the
  existing `firstPath`/`secondPath` properties. Both names MUST work; JLio names take
  precedence when both are present.
- **FR-002**: The `merge` command MUST accept `fromPath`/`toPath` as aliases for
  `path`/`targetPath`. Both names MUST work.
- **FR-003**: The `decisionTable` command MUST accept the config object under the key
  `"decisionTable"` in addition to the existing `"config"` key.
- **FR-004**: The ETL `flatten` command MUST accept `"flattenSettings"` as an alias for
  `"settings"`.
- **FR-005**: The ETL `restore` command MUST accept `"restoreSettings"` as an alias for
  `"settings"`.
- **FR-006**: The ETL `resolve` command MUST accept `"resolveSettings"` as an alias for
  `"settings"`.
- **FR-007**: The ETL `toCsv` command MUST accept `"csvSettings"` as an alias for
  `"settings"`.
- **FR-008**: All property name changes MUST be documented in
  `specs/002-migration-from-jlio/porting-guide.md` as a migration note (constitution §VIII).

**Missing Core Functions (US2)**

- **FR-009**: A `newGuid` function MUST be added to `TLio.Functions` that returns a new
  random UUID string. Registered as `"newGuid"`. Takes no arguments.
- **FR-010**: The `fetch` function MUST accept an optional second argument (the default
  value returned when the path resolves to nothing). Existing single-argument behaviour
  is unchanged.
- **FR-011**: A `path` function MUST be added to `TLio.Functions` as an alias for
  `scriptpath` — registered as `"path"` and producing identical output. Both names MUST
  remain registered.
- **FR-012**: The `promote` function MUST accept an optional second argument: an explicit
  property name for the wrapping key. When provided, the second argument overrides the
  auto-derived property name. Existing single-argument behaviour is unchanged.

**Fluent Builder API (US3)**

- **FR-013**: A fluent script builder class MUST be provided in `TLio.Client` allowing
  `new TLioScript<TNode>().Add(value).OnPath(path).Set(value).OnPath(path)` chains.
- **FR-014**: Copy, Move, Compare, Merge, Remove, IfElse MUST expose fluent entry points:
  `.Copy().From(path).To(path)`, `.Move().From(path).To(path)`,
  `.Compare().From(path).To(path).Result(path)`, `.Merge().From(path).To(path)`,
  `.Remove().OnPath(path)`, `.IfElse(condition).If(script).Else(script)`.
- **FR-015**: A `TLioConvert` static class MUST be provided with:
  `TLioConvert.Parse<TNode>(string, ParseOptions<TNode>)` and
  `TLioConvert.Serialize<TNode>(TLioScript<TNode>)`.

**Text Extension Functions (US4)**

- **FR-016**: A `TLio.Extensions.Text` project MUST be created providing the following
  functions: `concat`, `toString`, `parse`, `format`, `length`, `substring`, `replace`,
  `toLower`, `toUpper`, `trim`, `trimStart`, `trimEnd`.
- **FR-017**: Registration MUST follow the existing ETL pattern: an extension method
  `RegisterTextPack<TNode>()` on `IFunctionsProviderRegistrar<TNode>`.
- **FR-018**: Text functions MUST work with all TLio adapters (JSON/XML/YAML) — all
  string operations are performed on values extracted through `context.NodeAdapter`.

**AI Reference Format (US5)**

- **FR-019**: Every `docs/ai-ref/commands/*.md` file MUST be updated to include:
  an **Intent** line (one sentence), a **Supports functions** flag (✅/❌), a
  **C# Fluent API** section with a working code example, and updated property names
  matching JLio alignment.
- **FR-020**: Every `docs/ai-ref/functions/*.md` file MUST be updated to include:
  an **Intent** line, all supported signatures (including new ones from FR-010–FR-012),
  and **C# Fluent API** usage example.
- **FR-021**: New function files MUST be created: `docs/ai-ref/functions/NewGuid.md` and
  `docs/ai-ref/functions/Path.md`, following the same format.
- **FR-022**: Text function ai-ref files MUST be created under
  `docs/ai-ref/functions/` for each of the 12 text functions.

### Key Entities

- **TLioScript<TNode>**: The fluent script builder — exposes command entry points that
  return builder objects with `OnPath()`, `From()`, `To()`, `Result()` methods.
- **TLioConvert**: Static helper for parse and serialize operations.
- **TLio.Extensions.Text**: New project containing the text function pack.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% of JLio scripts in the JLio test suite using the aligned commands
  (`compare`, `merge`, `decisionTable`, all ETL commands) run without modification on
  TLio.
- **SC-002**: All new functions (`newGuid`, `path`, extended `fetch`, extended `promote`)
  pass fixture tests across all three adapters (JSON, XML, YAML).
- **SC-003**: All 12 text functions pass fixture tests against at least JSON and YAML
  adapters.
- **SC-004**: Fluent builder API produces scripts byte-identical to the equivalent
  hand-written JSON (verified via `TLioConvert.Serialize`).
- **SC-005**: All existing TLio tests pass without modification — no regressions.
- **SC-006**: The PowerShell constitution Article XI compliance check returns zero
  missing files after all new functions and their ai-ref.md files are added.

## Assumptions

- Property name alignment is additive (aliases added, old names preserved) — no existing
  TLio scripts break.
- TLio's `compare` command (scalar comparison: equal/greater/less/different) is retained
  alongside the JLio-aligned `fromPath`/`toPath` property names. JLio's structural diff
  semantics are not ported in this feature.
- `partial` semantics are NOT changed in this feature (JLio's `partial` is field
  projection; TLio's `partial` is index selection — these have different use cases and
  both are valid; a separate feature may address this).
- `TLio.Extensions.Text` is a new project in the solution alongside the existing
  `TLio.Extensions.ETL`.
- The fluent API uses the same generic `TNode` pattern as all other TLio APIs.
- JLio's `ifElse` `first`/`second` comparison mode is documented as a future scope item
  only; the `condition` mode is sufficient for this feature.
