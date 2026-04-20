# Tasks: Unified Script Notation

**Input**: Design documents from `specs/010-unify-script-notation/`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ contracts/ ✅ quickstart.md ✅

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to
- Exact file paths included in every description

---

## Phase 1: Foundational (Blocking Prerequisite)

**Purpose**: Create the notation reference document shell and its two foundational sections (§1 JSON-First Rule, §2 Value Types). All subsequent phases and all documentation updates will reference and point to this document — it must exist first.

**⚠️ CRITICAL**: No user story documentation work can begin until T001–T002 are complete.

- [x] T001 Create `docs/ai-ref/notation-reference.md` with §1 JSON-First Rule: state that a TLio script is always a valid JSON array; all expressions are JSON strings; include one concrete multi-command JSON snippet
- [x] T002 Add §2 Value Types table to `docs/ai-ref/notation-reference.md`: ScriptValue taxonomy (NullValue, JsonLiteralValue, FunctionValue, PathValue, FixedValue subtypes) with a "When it applies" column and one example per type

**Checkpoint**: `docs/ai-ref/notation-reference.md` exists and covers §1–§2. All user story work can now begin.

---

## Phase 2: User Story 1 — JSON-First Context and Quoting Rules (Priority: P1) 🎯 MVP

**Goal**: Deliver the complete quoting-rules section of the notation reference and update the function `ai-ref.md` files most affected by quoting ambiguity.

**Independent Test**: A developer reads `notation-reference.md` §3–§4 and the updated `Concat.md` and can correctly write `=concat($.first, '-', $.last)` — unquoted paths, single-quoted literal — without error.

### Implementation for User Story 1

- [x] T003 [US1] Add §3 Function Expressions to `docs/ai-ref/notation-reference.md`: define the `=name(arg, ...)` syntax; state that the outer JSON string `"..."` is the string boundary; the `=` prefix marks a function expression; no outer single-quoting of function expressions; include a right/wrong pair example
- [x] T004 [US1] Add §4 Quoting Rules to `docs/ai-ref/notation-reference.md`: explain that `'...'` is used for literal string arguments because the outer JSON uses `"..."`; path arguments (`$.f`, `@.f`) are never quoted; plain string literals at the value level need no quotes; include a right/wrong example table
- [x] T005 [P] [US1] Update `docs/ai-ref/functions/Concat.md`: replace abstract syntax placeholders `a, b` with `<value1>, <value2>`; verify example uses unquoted paths and single-quoted literal `' '`; add notation note `> See [Notation Reference](../notation-reference.md) for quoting rules.`
- [x] T006 [P] [US1] Update `docs/ai-ref/functions/Format.md`: same placeholder + notation note treatment as T005; ensure template literal argument is shown single-quoted
- [x] T007 [P] [US1] Update `docs/ai-ref/functions/Replace.md`: same placeholder + notation note; ensure search and replace arguments are shown single-quoted where literal
- [x] T008 [P] [US1] Update `docs/ai-ref/functions/Fetch.md`: clarify that the default-value argument `'Unknown'` is a single-quoted literal; add notation note
- [x] T009 [P] [US1] Update `docs/ai-ref/functions/Substring.md`: replace index/length abstract placeholders with `<index>`, `<length>`; add notation note
- [x] T010 [US1] Add fixture triplet `TLio.UnitTests/Fixtures/Notation/outer-quoted-function/`: `script.json` uses `"value": "'=concat($.a, $.b)'"` (outer-quoted); `result.json` must show the value is treated as the literal string `=concat(...)`, not a function result; add corresponding NUnit Theory case to existing notation test class (create `TLio.UnitTests/NotationTests/NotationEdgeCaseTests.cs` if it does not exist)

**Checkpoint**: US1 independently verifiable — quoting rules documented, key function pages updated, outer-quote fixture proves the rule.

---

## Phase 3: User Story 2 — Unambiguous `@` Symbol (Priority: P1)

**Goal**: Document all three roles of `@` in one place and add a parser warning when `@propertyName` (no dot) is used in a JSON/YAML script.

**Independent Test**: A developer reads `notation-reference.md` §5 and §7 and can correctly distinguish `@.field` (relative path), `@@` (escape), and `@attr` in XPath predicates. The `@` no-dot warning fires and is logged when running a script that uses `@name` without the dot.

### Implementation for User Story 2

- [x] T011 [US2] Add §5 Path References to `docs/ai-ref/notation-reference.md`: define `$.field` (absolute), `@.field` (relative, dot required), `@.<--` (parent nav), `@.<--.sibling` (sibling nav); include a table showing each form with a JSON example; state that `@field` (no dot) is invalid in JSON/YAML and triggers a parser warning
- [x] T012 [US2] Add §7 The Three Roles of `@` to `docs/ai-ref/notation-reference.md`: a three-row table — Role, Symbol, Context — covering `@.` (relative path, JSON/YAML), `@@` (escape, all contexts), `@attr` (XPath attribute, XML inside `[...]` predicates only)
- [x] T013 [US2] Add §9 XPath Special Cases to `docs/ai-ref/notation-reference.md`: state that `@attrName` (no dot) is valid only inside XPath predicate brackets (`[...]`) in XML mode; is not a relative-path reference; cross-reference `docs/ai-ref/adapters/xml-xpath.md`
- [x] T014 [P] [US2] Update `docs/ai-ref/functions/ScriptPath.md`: ensure every example uses `@.child` (with dot); add a note that `@child` (no dot) is not valid in JSON/YAML context; add notation note link
- [x] T015 [P] [US2] Update `docs/ai-ref/commands/Resolve.md`: ensure `@.productId` and `@.productName` examples are accompanied by a note that the dot is always required for relative paths; add notation note link
- [x] T016 [US2] Add `@` no-dot validation warning to `TLio.Client/FunctionConverter.cs`: after the `@`-prefix branch in `ParseValue`, detect when the string starts with `@` and is NOT `@@` (escape), `@.` (valid relative path), or a JSONPath filter context; call `context.LogWarning($"Path '{value}' is missing the required dot — did you mean '@.{value.Substring(1)}'? Relative paths in JSON/YAML require the '@.' prefix.")` then return `PathValue` unchanged (no breaking change)
- [x] T017 [US2] Add fixture triplet `TLio.UnitTests/Fixtures/Notation/dotpath-valid/`: `script.json` uses `@.field` relative path in a `set` or `put` command; `result.json` shows the correct output; add NUnit Theory case to `TLio.UnitTests/NotationTests/NotationEdgeCaseTests.cs`
- [x] T018 [US2] Verify Article I/IX compliance after T016: run `grep -rn "Newtonsoft\|JToken\|JObject" TLio.Core/ TLio.Commands/ TLio.Functions/` and confirm zero results; FunctionConverter.cs is in TLio.Client, not in scope for this check

**Checkpoint**: US2 independently verifiable — `@` roles documented, parser warning fires for `@name` (no dot), `@.name` fixture passes.

---

## Phase 4: User Story 3 — Token Handling Inside Quoted String Arguments (Priority: P2)

**Goal**: Document how `@@`, `$$`, `==` escape sequences work inside `'...'` literal arguments (and at the value level) with concrete examples; add supporting fixture triplets.

**Independent Test**: A developer reads `notation-reference.md` §6 and can write a `concat` expression that produces `user@example.com` without any search.

### Implementation for User Story 3

- [x] T019 [US3] Add §6 Escape Sequences to `docs/ai-ref/notation-reference.md`: a table showing all three escapes (`@@`, `$$`, `==`) with columns: Escape, Context (value level / inside `'...'`), Result, Example; explicitly confirm they work identically in both contexts
- [x] T020 [P] [US3] Add fixture triplet `TLio.Functions.Tests/Fixtures/Notation/escape-in-quoted-arg/`: `script.json` uses a concat with argument `'user@@example.com'`; `result.json` shows output `user@example.com`; add NUnit Theory case to `TLio.Functions.Tests` notation test class (create `TLio.Functions.Tests/NotationTests/EscapeSequenceTests.cs` if it does not exist)
- [x] T021 [P] [US3] Add fixture triplet `TLio.UnitTests/Fixtures/Notation/escape-at-value/`: `script.json` uses `"value": "@@admin"` (escape at value level, not inside a function); `result.json` shows output `@admin`; add NUnit Theory case to `TLio.UnitTests/NotationTests/NotationEdgeCaseTests.cs`
- [x] T022 [US3] Update `docs/ai-ref/overview.md` Escape Sequences section: add cross-reference link `> Full escape-sequence rules: [Notation Reference §6](notation-reference.md#6-escape-sequences)`; fix the bracket-write issue reference from `"010-bracket-write"` to `fix/bracket-write` branch name

**Checkpoint**: US3 independently verifiable — escape section documented, two fixtures prove identical behaviour in both contexts.

---

## Phase 5: User Story 4 — Consistent Notation Across All Documentation (Priority: P2)

**Goal**: Audit every remaining function and command `ai-ref.md` file and apply the unified conventions (placeholder form, notation note, correct quoting in examples).

**Independent Test**: Pick any 5 function pages and any 3 command pages; all use `<argN>` placeholder form in syntax, all examples apply quoting correctly, all have the notation note.

### Implementation for User Story 4

- [x] T023 [P] [US4] Audit and update `docs/ai-ref/functions/Datetime.md`: replace abstract placeholders with `<argN>` form; verify example quoting; add notation note
- [x] T024 [P] [US4] Audit and update `docs/ai-ref/functions/Indirect.md`: same convention treatment
- [x] T025 [P] [US4] Audit and update `docs/ai-ref/functions/Length.md`: same convention treatment
- [x] T026 [P] [US4] Audit and update `docs/ai-ref/functions/NewGuid.md`: verify no argument quoting issues (no-arg function); add notation note if applicable
- [x] T027 [P] [US4] Audit and update `docs/ai-ref/functions/Parse.md`: same convention treatment
- [x] T028 [P] [US4] Audit and update `docs/ai-ref/functions/Partial.md`: same convention treatment
- [x] T029 [P] [US4] Audit and update `docs/ai-ref/functions/Path.md`: same convention treatment
- [x] T030 [P] [US4] Audit and update `docs/ai-ref/functions/Promote.md`: same convention treatment
- [x] T031 [P] [US4] Audit and update `docs/ai-ref/functions/ToLower.md`: same convention treatment
- [x] T032 [P] [US4] Audit and update `docs/ai-ref/functions/ToString.md`: same convention treatment
- [x] T033 [P] [US4] Audit and update `docs/ai-ref/functions/ToUpper.md`: same convention treatment
- [x] T034 [P] [US4] Audit and update `docs/ai-ref/functions/Trim.md`: same convention treatment
- [x] T035 [P] [US4] Audit and update `docs/ai-ref/functions/TrimEnd.md`: same convention treatment
- [x] T036 [P] [US4] Audit and update `docs/ai-ref/functions/TrimStart.md`: same convention treatment
- [x] T037 [P] [US4] Audit and update `docs/ai-ref/commands/Add.md`: replace abstract placeholders; verify example quoting; add notation note
- [x] T038 [P] [US4] Audit and update `docs/ai-ref/commands/Compare.md`: same convention treatment
- [x] T039 [P] [US4] Audit and update `docs/ai-ref/commands/Copy.md`: same convention treatment
- [x] T040 [P] [US4] Audit and update `docs/ai-ref/commands/DecisionTable.md`: same convention treatment
- [x] T041 [P] [US4] Audit and update `docs/ai-ref/commands/Flatten.md`: same convention treatment
- [x] T042 [P] [US4] Audit and update `docs/ai-ref/commands/IfElse.md`: same convention treatment
- [x] T043 [P] [US4] Audit and update `docs/ai-ref/commands/Merge.md`: same convention treatment
- [x] T044 [P] [US4] Audit and update `docs/ai-ref/commands/Move.md`: same convention treatment
- [x] T045 [P] [US4] Audit and update `docs/ai-ref/commands/Put.md`: same convention treatment
- [x] T046 [P] [US4] Audit and update `docs/ai-ref/commands/Remove.md`: same convention treatment
- [x] T047 [P] [US4] Audit and update `docs/ai-ref/commands/Restore.md`: same convention treatment
- [x] T048 [P] [US4] Audit and update `docs/ai-ref/commands/Set.md`: same convention treatment
- [x] T049 [P] [US4] Audit and update `docs/ai-ref/commands/ToCsv.md`: same convention treatment

**Checkpoint**: US4 independently verifiable — all ai-ref.md files follow unified conventions.

---

## Phase 6: User Story 5 — Authoritative Notation Reference Document (Priority: P2)

**Goal**: Complete the two remaining sections of `docs/ai-ref/notation-reference.md` (§8 Bracket Notation, §9 is already done in Phase 3), integrate overview.md link, and verify the document satisfies all success criteria.

**Independent Test**: A developer opens only `docs/ai-ref/notation-reference.md` and can answer any of: "when to quote?", "what does `@.` mean?", "how do I escape `@`?", "what is `@.<--`?" — without opening any other file.

### Implementation for User Story 5

- [x] T050 [US5] Add §8 Bracket Notation to `docs/ai-ref/notation-reference.md`: document `$['property.with.dot']` syntax for properties containing path delimiters; include a table per adapter; note that reading is fully supported and writing is tracked on `fix/bracket-write`
- [x] T051 [US5] Add notation reference link to `docs/ai-ref/overview.md` Script Format section: add a line `See [Notation Reference](notation-reference.md) for complete quoting and escape rules.` immediately after the function-call example block
- [x] T052 [US5] Final review of `docs/ai-ref/notation-reference.md`: verify all 9 sections are present and complete; verify total length ≤ 150 lines (Article XI); verify every section has at least one concrete JSON snippet; verify no prose section exceeds 2 sentences

**Checkpoint**: US5 independently verifiable — notation reference is complete, self-contained, and linked from overview.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Verification, compliance checks, and cleanup.

- [x] T053 Run `dotnet test` from repository root and confirm zero test failures (all existing tests pass, all 4 new notation fixtures pass)
- [x] T054 [P] Run Article I compliance check: `grep -rn "Newtonsoft\|System\.Xml\|YamlDotNet\|JToken\|JObject\|JArray\|JValue\|XElement\|YamlNode" TLio.Core/ TLio.Commands/ TLio.Functions/` — must return zero results
- [x] T055 [P] Run Article IX compliance check: `grep -rn "JToken\|XElement\|YamlNode" TLio.Core/Contracts/ TLio.Core/Models/ TLio.Commands/` — must return zero results
- [x] T056 Run Article XI compliance check: `Get-ChildItem TLio.Commands -Filter "*Command.cs" -Recurse` and verify every command has a corresponding file in `docs/ai-ref/commands/`; same for functions — must return zero missing entries
- [x] T057 Update `specs/010-unify-script-notation/checklists/requirements.md` to mark all items verified after T053–T056 pass
- [x] T058 Validate `docs/ai-ref/notation-reference.md` against the quickstart.md: confirm every example in `quickstart.md` is consistent with the notation rules in `notation-reference.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — start immediately
- **US1 (Phase 2)**: Depends on T001–T002 (notation-reference.md shell must exist)
- **US2 (Phase 3)**: Depends on T001–T002; T016 (parser change) is independent of doc tasks
- **US3 (Phase 4)**: Depends on T001–T002; fixture triplets (T020, T021) can start after T002
- **US4 (Phase 5)**: Depends on T001–T002 (notation note must link to an existing file)
- **US5 (Phase 6)**: T050 depends on T001–T002 being written; T051–T052 depend on all earlier notation-reference.md sections being complete
- **Polish (Phase 7)**: Depends on all phases complete

### User Story Dependencies

- **US1 (P1)**: Start after T001–T002. No dependency on US2–US5.
- **US2 (P1)**: Start after T001–T002. T016 (code change) is fully independent of doc tasks.
- **US3 (P2)**: Start after T001–T002. Fully independent of US1 and US2.
- **US4 (P2)**: Start after T001–T002. The `[P]`-marked tasks within US4 are all independent of each other.
- **US5 (P2)**: T050 can start after T001–T002. T051–T052 should be done after US1–US4 are complete to verify completeness.

### Within Each User Story

- Notation-reference.md sections must be written before the `ai-ref.md` files can link to them
- Parser change (T016) is independent and can be developed in parallel with all doc tasks
- Fixture triplets must be written before their NUnit Theory cases are wired up

### Parallel Opportunities

- T005–T009 (function page updates for US1) can all run in parallel
- T011–T013 (notation-reference.md sections for US2) can be done together in one edit
- T020 and T021 (escape fixtures) can run in parallel
- T023–T049 (US4 audit tasks) can all run in parallel — each is a different file
- T054 and T055 (compliance checks) can run in parallel

---

## Parallel Example: User Story 4 (doc audit)

```text
# All US4 function updates run in parallel (different files):
T023: docs/ai-ref/functions/Datetime.md
T024: docs/ai-ref/functions/Indirect.md
T025: docs/ai-ref/functions/Length.md
... (T026–T036)

# All US4 command updates run in parallel (different files):
T037: docs/ai-ref/commands/Add.md
T038: docs/ai-ref/commands/Compare.md
... (T039–T049)
```

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1 (Foundational): T001–T002
2. Complete Phase 2 (US1): T003–T010
3. Complete Phase 3 (US2): T011–T018
4. **STOP and VALIDATE**: Quoting rules and `@` rules are documented and enforced. Core value delivered.
5. Proceed to US3–US5 as time allows.

### Incremental Delivery

1. T001–T002 → Notation reference shell ready
2. T003–T010 → Quoting rules complete (US1 ✅)
3. T011–T018 → `@` disambiguation complete (US2 ✅)
4. T019–T022 → Escape sequences complete (US3 ✅)
5. T023–T049 → All ai-ref.md files consistent (US4 ✅)
6. T050–T052 → Notation reference complete (US5 ✅)
7. T053–T058 → Verified and clean

---

## Notes

- `[P]` tasks = different files, no dependencies — can run in parallel
- `[Story]` label maps task to specific user story for traceability
- No new C# projects or classes are introduced — one method-level change in `TLio.Client/FunctionConverter.cs`
- The parser change (T016) is a `LogWarning` only — `PathValue` is still returned; no breaking change
- All fixture triplets follow Article VI routing: core/command tests → `TLio.UnitTests/`; function tests → `TLio.Functions.Tests/`
- Article XI: No new commands or functions are introduced; the notation reference is a documentation file, not a component file — no new `ai-ref.md` is required for code components
- Commit after each phase checkpoint

---

## TLio-Specific Rules (Constitution §VI, §IV, §X)

### Test tasks use file-based fixture triplets

All 4 notation test scenarios (T010, T017, T020, T021) use the fixture triplet pattern:

```
<TestProject>/Fixtures/Notation/<scenario-name>/
  input.json    ← starting document
  script.json   ← TLioScript (serialised command)
  result.json   ← expected output after execution
```

### Constitutional compliance checks before marking done

Before checking off T016 (parser change):

```sh
# Article I / IX: no format types in Core/Commands/Functions
grep -rn "Newtonsoft\|JToken\|JObject\|JArray\|JValue\|XElement\|YamlNode" \
  TLio.Core/ TLio.Commands/ TLio.Functions/

# Article II: no direct adapter construction
grep -rn "new.*Adapter\|new.*Fetcher\|new.*ExecutionContext" \
  TLio.Commands/ TLio.Functions/
```

Both must return zero results. Full checklist: `.specify/templates/speckit.implement.md`.
