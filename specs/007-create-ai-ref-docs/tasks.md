---
description: "Task list for AI reference documentation"
---

# Tasks: AI Reference Documentation

**Input**: Design documents from `specs/007-create-ai-ref-docs/`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ contracts/ai-ref-template.md ✅

**Organization**: Phase 1 creates directories; Phase 2 creates the blocking overview;
Phases 3–5 create the per-component files (fully parallelisable within each phase).

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

---

## Phase 1: Setup

**Purpose**: Create the `docs/ai-ref/` directory tree.

- [x] T001 Create `docs/ai-ref/commands/`, `docs/ai-ref/functions/`, and `docs/ai-ref/adapters/` directories

---

## Phase 2: Foundational — Overview

**Purpose**: Create `docs/ai-ref/overview.md` — referenced by every command, function,
and adapter file. No other Phase 3+ task may begin until this exists.

**⚠️ CRITICAL**: All User Story phases depend on this file.

- [x] T002 Create `docs/ai-ref/overview.md` with: (1) TLio intro paragraph, (2) adapter selection table (format / project / factory method / path style), (3) JSONPath Newtonsoft vs System.Text.Json comparison table (8+ feature rows), (4) script format section with minimal JSON example, (5) ETL extension pack registration section. Max 150 lines. See `specs/007-create-ai-ref-docs/contracts/ai-ref-template.md`.

**Checkpoint**: `docs/ai-ref/overview.md` exists — User Story phases can now proceed.

---

## Phase 3: User Story 1 — Command Reference (Priority: P1)

**Goal**: Every command has a `docs/ai-ref/commands/<Name>.md` file an AI agent can use
to generate a correct TLio script without reading source code.

**Independent Test**: Given only `docs/ai-ref/commands/`, an AI agent produces a
syntactically and semantically valid script for each of the 14 commands.

Each file must follow the template in `specs/007-create-ai-ref-docs/contracts/ai-ref-template.md`:
one-sentence purpose, Syntax block, Options table (type/required/default/description),
Formats note referencing `../overview.md`, and at least one complete JSON example.
Max 150 lines each.

### Core commands

- [x] T003 [P] [US1] Create `docs/ai-ref/commands/Set.md` — command `"set"`. Purpose: sets value of **existing** node (fails if absent). Properties: `path` (string, required), `property` (string, optional), `value` (TLioValue, required). Show both one-arg form (`path` selects target) and two-arg form (`path` selects parent, `property` names child). Distinguish from `put` (upsert) and `add` (create-only).
- [x] T004 [P] [US1] Create `docs/ai-ref/commands/Add.md` — command `"add"`. Purpose: creates new property or appends to array; **skips if property already exists**. Properties: `path` (string, required), `property` (string, optional), `value` (TLioValue, required). Distinguish from `put` (updates existing) and `set` (errors if absent).
- [x] T005 [P] [US1] Create `docs/ai-ref/commands/Put.md` — command `"put"`. Purpose: **upsert** — sets if exists, creates if absent. Properties: `path` (string, required), `property` (string, optional), `value` (TLioValue, required). Distinguish from `set` (errors if absent) and `add` (skips if exists).
- [x] T006 [P] [US1] Create `docs/ai-ref/commands/Remove.md` — command `"remove"`. Purpose: removes all nodes matched by path. Properties: `path` (string, required). Example: `{ "command": "remove", "path": "$.tempField" }`.
- [x] T007 [P] [US1] Create `docs/ai-ref/commands/Copy.md` — command `"copy"`. Purpose: copies nodes from source path to destination path. Properties: `fromPath` (string, required), `toPath` (string, required), `destinationAsArray` (boolean, optional, default false). When `destinationAsArray` is true, aligns results by array index.
- [x] T008 [P] [US1] Create `docs/ai-ref/commands/Move.md` — command `"move"`. Purpose: moves nodes (copy + remove source). Properties: `fromPath` (string, required), `toPath` (string, required), `destinationAsArray` (boolean, optional, default false). Same as Copy but removes source nodes.
- [x] T009 [P] [US1] Create `docs/ai-ref/commands/IfElse.md` — command `"ifElse"`. Purpose: evaluates a condition and executes one of two script branches. Properties: `condition` (TLioValue, required — true/false literal or function), `ifScript` (array of commands, required), `elseScript` (array of commands, optional). Example showing literal `true` condition and nested script array.
- [x] T010 [P] [US1] Create `docs/ai-ref/commands/Compare.md` — command `"compare"`. Purpose: compares two nodes and writes the comparison result string. Properties: `firstPath` (string, required), `secondPath` (string, required), `resultPath` (string, required). Result values: `"equal"`, `"greater"`, `"less"`, `"different"`.
- [x] T011 [P] [US1] Create `docs/ai-ref/commands/Merge.md` — command `"merge"`. Purpose: deep-merges source nodes into target nodes. Properties: `path` (string, required — source), `targetPath` (string, required — destination), `arrayMergeMode` (string, optional, default `"concat"` — values: `"concat"`, `"replace"`).
- [x] T012 [P] [US1] Create `docs/ai-ref/commands/DecisionTable.md` — command `"decisionTable"`. Purpose: applies a decision table — matches input conditions to output results with configurable strategy. Properties: `path` (string, required), `config` (object, required). Config object contains: `inputs` (array of `{name, path}`), `outputs` (array of `{name, path}`), `rules` (array of `{priority?, conditions: {name: value}, results: {name: value}}`), `strategy` (object: `{mode: "firstMatch"|"bestMatch"|"allMatches", conflictResolution: "priority"|"lastWins"|"merge"}`), `defaultResults` (object of `{name: value}`, optional). Include a complete example with at least 2 rules.

### ETL extension commands

**Note**: ETL commands require `options.CommandsProvider.RegisterETL<TNode>()` in addition
to `ParseOptions<TNode>.CreateDefault()`. Document this in each ETL file.

- [x] T013 [P] [US1] Create `docs/ai-ref/commands/Flatten.md` — command `"flatten"`. Purpose: flattens a nested object to single-level with dot-separated keys; stores metadata for reconstruction. Properties: `path` (string, required), `settings` (object, optional) containing key options: `delimiter` (string, default `"."`), `maxDepth` (integer, optional), `excludePaths` (array of strings, optional), `metadataPath` (string, optional). Note ETL registration requirement.
- [x] T014 [P] [US1] Create `docs/ai-ref/commands/Restore.md` — command `"restore"`. Purpose: reconstructs a nested object from data previously flattened by `flatten`. Properties: `path` (string, required), `settings` (object, optional) containing: `delimiter` (string, default `"."`), `metadataKey` (string, optional), `strictMode` (boolean, optional), `removeMetadata` (boolean, optional). Note ETL registration requirement.
- [x] T015 [P] [US1] Create `docs/ai-ref/commands/Resolve.md` — command `"resolve"`. Purpose: looks up reference data and writes resolved values. Properties: `path` (string, required), `settings` (array, required) — each entry: `referencesCollectionPath` (string), `resolveKeys` (array of `{keyPath, referenceKeyPath}`), `values` (array of `{targetPath, value}`). Supports `@.property` relative path notation in `values[].targetPath`. Note ETL registration requirement.
- [x] T016 [P] [US1] Create `docs/ai-ref/commands/ToCsv.md` — command `"tocsv"`. Purpose: converts an object or array of objects to a CSV string. Properties: `path` (string, required — source), `settings` (object, optional) containing: `delimiter` (string, default `","`), `includeHeaders` (boolean, default true), `booleanFormat` (string, optional), `nullValueRepresentation` (string, optional), `quoteAllFields` (boolean, optional). Note ETL registration requirement.

**Checkpoint**: All 14 command files exist. US1 independently testable.

---

## Phase 4: User Story 2 — Function Reference (Priority: P1)

**Goal**: Every function has a `docs/ai-ref/functions/<Name>.md` file an AI agent can
use to produce correct `=function(args)` value expressions.

**Independent Test**: Given only `docs/ai-ref/functions/`, an agent produces correct
function call strings for all 6 built-in functions.

Each file uses the function template from `specs/007-create-ai-ref-docs/contracts/ai-ref-template.md`:
heading with `=` prefix, syntax block, arguments table (positional #), returns section,
example embedded in a `set` command. Max 150 lines each.

- [x] T017 [P] [US2] Create `docs/ai-ref/functions/Fetch.md` — function `fetch`. Syntax: `=fetch(path)`. Arguments: #1 path (string, required) — JSONPath or format-equivalent selecting the source node. Returns: the first matched node's value. Fails with warning if path matches nothing. Example: `"value": "=fetch($.source)"`.
- [x] T018 [P] [US2] Create `docs/ai-ref/functions/Indirect.md` — function `indirect`. Syntax: `=indirect(pathToPath)`. Arguments: #1 (string, required) — path to a node whose **string value** is used as a second path. Two-step resolution: read string at arg path, then evaluate that string as a path. Example: `"value": "=indirect($.pathRef)"` where `$.pathRef` contains `"$.source"`.
- [x] T019 [P] [US2] Create `docs/ai-ref/functions/Promote.md` — function `promote`. Syntax: `=promote(path)`. Arguments: #1 (string, required) — path to a node. Returns: a new object with one key (the matched node's property name) whose value is the matched node. Use when you need to wrap a nested object under its own key. Example: `"value": "=promote($.person)"` → `{ "person": { ... } }`.
- [x] T020 [P] [US2] Create `docs/ai-ref/functions/Partial.md` — function `partial`. Syntax: `=partial(path)` or `=partial(path, index)`. Arguments: #1 (string, required) — multi-match path expression; #2 (integer, optional, default 0) — zero-based index into the matched nodes. Returns: the nth matched node. Use to pick one element from a wildcard match. Example: `"value": "=partial($.items[*])"` → first item.
- [x] T021 [P] [US2] Create `docs/ai-ref/functions/ScriptPath.md` — function `scriptpath`. Syntax: `=scriptpath()` or `=scriptpath(@.child)`. Arguments: #1 (string, optional) — relative path starting with `@`. Returns: absolute path string of current node (or resolved relative path). Useful for writing the current node's path into a field. Example: `"value": "=scriptpath()"` → `"$"` at root.
- [x] T022 [P] [US2] Create `docs/ai-ref/functions/Datetime.md` — function `datetime`. Syntax: `=datetime()` or `=datetime(format)`. Arguments: #1 (string, optional) — .NET date format string; default is ISO 8601 (`yyyy-MM-ddTHH:mm:ss.fffZ`). Returns: current UTC date/time as a formatted string. Example: `"value": "=datetime(yyyy-MM-dd)"` → `"2026-04-06"`.

**Checkpoint**: All 6 function files exist. US2 independently testable.

---

## Phase 5: User Story 3 — Adapter & Format Selection (Priority: P2)

**Goal**: Every adapter variant has a `docs/ai-ref/adapters/<name>.md` covering setup,
path syntax table, and notable limitations.

**Independent Test**: Given only `docs/ai-ref/adapters/` and `overview.md`, an agent
correctly selects the adapter and factory method for each of the 5 variants.

Each file uses the adapter template from `specs/007-create-ai-ref-docs/contracts/ai-ref-template.md`.
Max 150 lines each.

- [x] T023 [P] [US3] Create `docs/ai-ref/adapters/json-newtonsoft.md`. Adapter: `TLio.Json`. Library: `Newtonsoft.Json`. Factory: `JsonExecutionContext.Create(data, script, options)`. Path style: Goessner JSONPath (`$` root). Path table covering: root `$`, child `$.name`, nested `$.a.b`, array index `$.items[0]`, wildcard `$.items[*]`, recursive `$..name`, filter `$.items[?(@.active == true)]`. Notes: supports script expressions `()`, most permissive JSONPath variant. Cross-reference `../overview.md` JSONPath table for full comparison.
- [x] T024 [P] [US3] Create `docs/ai-ref/adapters/json-systemtext.md`. Adapter: `TLio.Json.SystemText`. Library: `System.Text.Json` + `JsonCons.JsonPath`. Factory: `SystemTextJsonExecutionContext.Create(data, script, options)`. Path style: RFC 9535 JSONPath (same `$` root syntax). Path table same as Newtonsoft. Notes: does **not** support script expressions `()`; strict RFC 9535 compliance; use when Newtonsoft dependency is not allowed. Cross-reference `../overview.md` for full comparison table.
- [x] T025 [P] [US3] Create `docs/ai-ref/adapters/xml-slashpath.md`. Adapter: `TLio.Xml` with `SlashPathItemsFetcher`. Factory: `XmlExecutionContext.CreateWithSlashPaths(data, script)`. Path style: slash-separated (`/` root, `/child`, `/a/b/c`, `/items/*`). Path table covering: root `/`, direct child `/name`, nested `/a/b`, wildcard `/items/*`. Notes: simple path model, no predicate support; root element of the XML document is the implicit container (paths start inside it).
- [x] T026 [P] [US3] Create `docs/ai-ref/adapters/xml-xpath.md`. Adapter: `TLio.Xml` with `NativeXPathItemsFetcher`. Factory: `XmlExecutionContext.CreateWithNativeXPath(data, script)`. Path style: XPath with `.` as root. Path table covering: root `.`, direct child `name`, nested `address/city`, recursive `//name`, indexed `items/item[1]`, predicate `items/item[@id='1']`, wildcard `*`. Notes: more expressive than slash-path; use when attribute predicates or recursive descent needed. Root is `.` not `/`.
- [x] T027 [P] [US3] Create `docs/ai-ref/adapters/yaml.md`. Adapter: `TLio.Yaml`. Library: `YamlDotNet`. Factory: `YamlExecutionContext.Create(data, script, options)`. Path style: dot-notation (`$` root, `$.name`, `$.a.b`, `$.items[0]`, `$.items[*]`). Path table covering: root `$`, child `$.name`, nested `$.a.b`, array index `$.items[0]`, wildcard `$.items[*]`. Notes: case-sensitive keys; multi-document YAML (separated by `---`) is parsed as array root; no filter expressions.

**Checkpoint**: All 5 adapter files exist. US3 independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T028 Run PowerShell compliance check from constitution Article XI and confirm zero missing files: `Get-ChildItem TLio.Commands -Filter "*Command.cs" -Recurse` and `Get-ChildItem TLio.Functions -Filter "*Function.cs" -Recurse` — verify each has a matching `docs/ai-ref/` entry. Document result.
- [x] T029 [P] Spot-check 3 command files and 2 function files against actual fixture JSON in `TLio.Json.SystemText.Tests/Fixtures/` and `TLio.Functions.Tests/Fixtures/` to verify examples are correct and match real serialization.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (dir must exist) — **blocks all user stories**
- **User Stories (Phase 3–5)**: All depend on Phase 2 (overview.md must exist for cross-references)
  - Phase 3, 4, and 5 can run **fully in parallel** after Phase 2
  - All tasks within each phase are also parallelisable
- **Polish (Phase 6)**: Depends on all user story phases complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 — no dependency on US2 or US3
- **US2 (P1)**: Can start after Phase 2 — no dependency on US1 or US3
- **US3 (P2)**: Can start after Phase 2 — no dependency on US1 or US2

### Within Each Phase

- All tasks within Phase 3, 4, 5 are independent (different files)
- Polish tasks depend on all prior phases complete

---

## Parallel Opportunities

```text
Phase 1: T001
Phase 2: T002 (depends on T001)

After T002 — run ALL of these in parallel:
  Phase 3 (US1): T003 T004 T005 T006 T007 T008 T009 T010 T011 T012 T013 T014 T015 T016
  Phase 4 (US2): T017 T018 T019 T020 T021 T022
  Phase 5 (US3): T023 T024 T025 T026 T027

Phase 6: T028 T029 (after all above complete)
```

---

## Implementation Strategy

### MVP First (US1 + US2 — both P1)

1. T001: Create directories
2. T002: Create overview.md
3. T003–T016 in parallel: All 14 command files (US1)
4. T017–T022 in parallel: All 6 function files (US2)
5. Validate: US1 + US2 complete — AI agents can write scripts using commands and functions

### Full Delivery (add US3)

6. T023–T027 in parallel: All 5 adapter files (US3)
7. T028–T029: Compliance check + spot-check

### Parallel Team Strategy

With multiple agents:

- Agent A: T002 (overview — unblocks all others)
- Once T002 done:
  - Agent B: T003–T012 (10 core commands)
  - Agent C: T013–T016 (4 ETL commands) + T017–T022 (6 functions)
  - Agent D: T023–T027 (5 adapter files)
- All agents: T028–T029 after everything merges

---

## TLio-Specific Rules (Constitution §VI, §IV, §X, §XI)

### AI reference files (Constitution §XI)

This feature IS the implementation of Article XI. Every task in Phases 3–5 directly
creates the ai-ref.md files the constitution requires.

### No format-specific types in Core/Commands task descriptions

Not applicable — this feature adds no production code.

### Constitutional compliance check before marking done

Before marking T028 `[x]`, run:

```pwsh
# Commands missing ai-ref.md
Get-ChildItem TLio.Commands -Filter "*Command.cs" -Recurse | ForEach-Object {
  $ref = "docs/ai-ref/commands/$($_.BaseName).md"
  if (-not (Test-Path $ref)) { "MISSING: $ref" }
}

# Functions missing ai-ref.md
Get-ChildItem TLio.Functions -Filter "*Function.cs" -Recurse | ForEach-Object {
  $ref = "docs/ai-ref/functions/$($_.BaseName).md"
  if (-not (Test-Path $ref)) { "MISSING: $ref" }
}
```

Both must return zero output before T028 is marked `[x]`.
