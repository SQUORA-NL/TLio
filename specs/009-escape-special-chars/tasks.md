# Tasks: Special Character Escaping in Value and Path Parsing

**Input**: Design documents from `/specs/009-escape-special-chars/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Setup

**Purpose**: Confirm the codebase compiles and all existing tests pass before any changes.

- [x] T001 Run `dotnet build` from repo root and confirm zero errors
- [x] T002 Run `dotnet test` from repo root and confirm all tests pass (record baseline count)

---

## Phase 2: Foundational — Remove Dead Code Block

**Purpose**: Remove the duplicate quoted-string block in `FunctionConverter.cs` so the file has a clean single-branch structure. All US1–US3 work builds on this baseline.

**⚠️ CRITICAL**: US1 and US2 cannot be implemented correctly until this is done.

- [x] T003 Remove the dead duplicate quoted-string `if` block (lines 50–53 in current WIP) from `TLio.Client/FunctionConverter.cs`, keeping only the escape-aware block that does `.Replace("@@", "@")`
- [x] T004 Run `dotnet build` and `dotnet test` — confirm zero regressions after dead-code removal

**Checkpoint**: `FunctionConverter.cs` compiles cleanly with a single quoted-string branch.

---

## Phase 3: User Story 1 — Escape Trigger Characters in Top-Level Value Expressions (Priority: P1) 🎯 MVP

**Goal**: `ParseValue` correctly handles `@@`, `$$`, and `==` double-prefix escapes for unquoted values AND decodes `$$` and `==` (in addition to existing `@@`) inside quoted strings.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~TextHandlingTests"` — all new escape-sequence tests pass.

### Implementation for User Story 1

- [x] T005 [US1] Extend the unquoted-prefix check in `TLio.Client/FunctionConverter.cs` `ParseValue`: before the `$`/`@` path check, add a `$$` → `FixedValue("$…")` branch; before the `=` function check, add a `==` → `FixedValue("=…")` branch (the `@@` branch from T003 already covers `@`)
- [x] T006 [US1] Extend the quoted-string branch in `TLio.Client/FunctionConverter.cs` to also replace `$$` → `$` and `==` → `=` (in addition to the existing `@@` → `@` replace)
- [x] T007 [P] [US1] Add inline `[Test]` cases to `TLio.UnitTests/EngineTests/TextHandlingTests.cs` covering:
  - `@@admin` → `FixedValue` with string `@admin`
  - `$$total` → `FixedValue` with string `$total`
  - `==formula` → `FixedValue` with string `=formula`
  - `'user@@example.com'` → `FixedValue` with string `user@example.com`
  - `'$$ref'` → `FixedValue` with string `$ref`
  - `'==expr'` → `FixedValue` with string `=expr`
  - `@.property` still returns `PathValue` (regression guard)
  - `$.path` still returns `PathValue` (regression guard)
  - `=fetch($.x)` still returns `FunctionSupportedValue` (regression guard)
- [x] T008 [P] [US1] Write fixture triplet for end-to-end `@@` escape: `TLio.UnitTests/EngineTests/EscapeCharFixtures/AtEscape/input.json`, `script.json`, `result.json` — script uses `set` with `value: "@@adminRole"`, result has literal string `@adminRole`
- [x] T009 [P] [US1] Write fixture triplet for end-to-end `$$` escape: `TLio.UnitTests/EngineTests/EscapeCharFixtures/DollarEscape/input.json`, `script.json`, `result.json`
- [x] T010 [P] [US1] Write fixture triplet for end-to-end `==` escape: `TLio.UnitTests/EngineTests/EscapeCharFixtures/EqualsEscape/input.json`, `script.json`, `result.json`
- [x] T011 [US1] Add a `Theory`-based test class `EscapeCharFixtureTests` (or extend existing fixture runner) in `TLio.UnitTests/EngineTests/` that loads and executes all triplets from `EscapeCharFixtures/`
- [x] T012 [US1] Run `dotnet test --filter "FullyQualifiedName~TextHandlingTests|FullyQualifiedName~EscapeCharFixture"` — all green

**Checkpoint**: Setting a value to `@@x`, `$$x`, or `==x` in a TLio script stores the literal string without path or function interpretation.

---

## Phase 4: User Story 2 — Escape Inside Function Arguments (Priority: P2)

**Goal**: The same `@@`/`$$`/`==` escapes work correctly when they appear as function arguments, giving a `FixedValue` rather than a path or function call.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~TextHandlingTests"` — new function-argument escape tests pass.

### Implementation for User Story 2

- [x] T013 [US2] Review `ParseFunctionExpression` arg loop in `TLio.Client/FunctionConverter.cs` — the loop already calls `ParseValue` for non-path args, so `@@`/`$$`/`==` escapes from T005–T006 apply automatically; confirm no extra code is needed
- [x] T014 [P] [US2] Add inline `[Test]` cases to `TLio.UnitTests/EngineTests/TextHandlingTests.cs` covering function-argument escapes:
  - `=concat('@@prefix', @$.name)` — first arg is string `@prefix`, second is path
  - `=concat('$$ref', 'literal')` — first arg is string `$ref`
  - `=concat('==expr', '1')` — first arg is string `=expr`
  - `@@foo` as a bare function arg (no quotes) → `FixedValue("@foo")`
- [x] T015 [US2] Run `dotnet test --filter "FullyQualifiedName~TextHandlingTests"` — all green including new arg-escape tests

**Checkpoint**: Function arguments follow the same escape rules as top-level values; no separate mental model needed.

---

## Phase 5: User Story 3 — Escape Special Characters in Path Property Names (Priority: P3)

**Goal**: Property names containing the path delimiter or other special characters can be addressed in each adapter via bracket notation or documented equivalent.

**Independent Test**: Per-adapter test for bracket notation resolves the correct node.

### Implementation for User Story 3

- [x] T016 [P] [US3] Add fixture triplet for JSON bracket-notation path in `TLio.Json.Tests/PathEscapeTests/Fixtures/BracketNotationDot/`: `input.json` = `{"version.major": 2}`, `script.json` selects `$['version.major']`, `result.json` = the value `2` is read correctly by a `fetch` or `set` command
- [x] T017 [P] [US3] Add bracket-notation fixture test class `PathEscapeTests` in `TLio.Json.Tests/PathEscapeTests/` that loads fixtures and verifies correct node selection via `JsonPathItemsFetcher`
- [x] T018 [P] [US3] Add fixture triplet for XML NativeXPath in `TLio.Xml.Tests/PathEscapeTests/Fixtures/NativeXpathAttribute/`: demonstrate `@` attribute syntax and `=` in a predicate (e.g., `item[@id='1']`) selecting the correct node
- [x] T019 [P] [US3] Add fixture triplet for Slash-path XML in `TLio.Xml.Tests/PathEscapeTests/Fixtures/SlashPathEscapedSegment/`: document and test how a segment name containing `/` is handled (URL-encoding or rejection with clear error message)
- [x] T020 [US3] Add bracket-notation segment parsing to `TLio.Yaml/YamlPathItemsFetcher.cs`: when a path segment matches `['<key>']`, use the inner key verbatim (including dots) for node lookup; update `SplitArgs`-style splitting in the fetcher's path parser
- [x] T021 [P] [US3] Add fixture triplet for YAML bracket-notation path in `TLio.Yaml.Tests/PathEscapeTests/Fixtures/QuotedSegmentDot/`: `input.yaml` has key `server.host`, `script.json` uses `$['server.host']`, `result.json` verifies the correct value
- [x] T022 [US3] Add `PathEscapeTests` fixture test class in `TLio.Yaml.Tests/PathEscapeTests/`
- [x] T023 [US3] Run `dotnet test --filter "FullyQualifiedName~PathEscape"` across all adapter test projects — all green

**Checkpoint**: Each adapter has at least one passing test confirming a property name with delimiter characters is addressable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T024 [P] Update `docs/ai-ref/overview.md` — add an "Escape Sequences" section documenting `@@`, `$$`, `==` for values and bracket notation for path property names (per Article XI; keep under 150 lines)
- [x] T025 [P] Update `specs/002-migration-from-jlio/porting-guide.md` — note that `@@` inside unquoted values now produces a string literal (new in 009), `$$` and `==` prefixes are TLio extensions (per Article VIII)
- [x] T026 Run full `dotnet test` — confirm total test count ≥ baseline from T002 with zero failures
- [x] T027 Run Article I/IX compliance grep: `grep -rn "Newtonsoft\|JToken\|JObject\|JArray\|XElement\|YamlNode" TLio.Core/ TLio.Commands/ TLio.Functions/` — must return zero results
- [x] T028 Run Article II compliance grep: `grep -rn "new.*Adapter\|new.*Fetcher\|new.*ExecutionContext" TLio.Commands/ TLio.Functions/` — must return zero results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1** (Setup): No dependencies — start immediately
- **Phase 2** (Dead-code removal): Depends on Phase 1 — **BLOCKS Phase 3 and Phase 4**
- **Phase 3** (US1 - top-level escapes): Depends on Phase 2
- **Phase 4** (US2 - function arg escapes): Depends on Phase 3 (T013 confirms no extra code needed)
- **Phase 5** (US3 - path bracket notation): Depends on Phase 2; **independent of Phase 3 and 4**
- **Phase 6** (Polish): Depends on all story phases complete

### User Story Dependencies

- **US1 (P1)**: Requires Phase 2 complete. No dependency on US2 or US3.
- **US2 (P2)**: Requires US1 complete (verifies `ParseValue` escape fix applies automatically).
- **US3 (P3)**: Requires Phase 2 complete. Can run in parallel with US1 and US2.

### Parallel Opportunities

- T008, T009, T010 (fixture files) can all be written at the same time
- T016, T017, T018, T019, T021 (adapter fixture work) can all run in parallel
- T024, T025 (docs) can run in parallel with any implementation phase

---

## Parallel Example: US3 Path Escaping

```text
In parallel once Phase 2 is done:
  Task T016: JSON bracket-notation fixture
  Task T018: XML NativeXPath attribute fixture
  Task T019: XML Slash-path escaped segment fixture
  Task T021: YAML bracket-notation fixture
```

---

## Implementation Strategy

### MVP (US1 only)

1. Phase 1: confirm baseline
2. Phase 2: remove dead code (T003–T004)
3. Phase 3: fix `FunctionConverter` top-level escapes + tests (T005–T012)
4. **STOP and VALIDATE**: run `dotnet test` — all escape tests green
5. Merge if unblocked

### Incremental Delivery

1. Phases 1–2 → clean baseline
2. Phase 3 (US1) → `@@`/`$$`/`==` escaping works at top level
3. Phase 4 (US2) → confirmed same escapes work inside function args
4. Phase 5 (US3) → bracket notation documented and tested per adapter
5. Phase 6 → docs updated, full compliance pass

---

## Notes

- No new projects or assemblies are needed for this feature
- `TLio.Client/FunctionConverter.cs` is the only production file changed for US1 and US2
- `TLio.Yaml/YamlPathItemsFetcher.cs` is the only production file changed for US3
- All test additions are in existing test projects
- Constitution Article I/IX: `TLio.Client` already references `Newtonsoft.Json` and is an adapter-adjacent project, so its use of `JToken` in test helpers is acceptable
