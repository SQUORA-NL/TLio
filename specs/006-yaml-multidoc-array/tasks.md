---

description: "Task list for 006-yaml-multidoc-array"
---

# Tasks: YAML Multi-Document as Array Root

**Input**: Design documents from `specs/006-yaml-multidoc-array/`
**Prerequisites**: plan.md ✅, spec.md ✅
**Status**: ✅ COMPLETE

**Note**: The existing YAML test infrastructure uses a single `fixture.yaml` with
three `---`-separated sections (input / script / expected). Multi-document YAML
*inputs* cannot be encoded in that format, so parse-level tests use inline NUnit
`[Test]` methods (unit-level, valid per constitution §VI). Full-pipeline tests
use the existing fixture format with a YAML sequence as the input document.

---

## Phase 1: User Story 1 — Multi-Document Parse (Priority: P1) 🎯 MVP ✅

**Goal**: `YamlNodeAdapter.Parse()` returns a `YamlSequenceNode` containing N
roots when the input has N YAML documents separated by `---`.

### Fixtures for User Story 1 (array-root pipeline tests)

- [x] T001 [P] [US1] Create `01-set-on-first-element` fixture in `TLio.Yaml.Tests/Fixtures/YamlArrayRoot/01-set-on-first-element/fixture.yaml` — input is a YAML sequence of two mappings; script sets `$[0].name` to "new"; expected shows first item updated
- [x] T002 [P] [US1] Create `02-set-on-all-elements` fixture in `TLio.Yaml.Tests/Fixtures/YamlArrayRoot/02-set-on-all-elements/fixture.yaml` — input is a YAML sequence; script sets `$[*].active` to true via `property` syntax; expected shows all items updated

### Test Class for User Story 1

- [x] T003 [US1] Write `YamlMultiDocParseTests` NUnit class in `TLio.Yaml.Tests/Yaml/YamlMultiDocParseTests.cs` — inline `[Test]` methods calling `YamlNodeAdapter.Parse()` directly: 0-doc → empty sequence, 1-doc → mapping (unchanged), 2-doc → sequence of 2, 3-doc → sequence of 3, content of each element verified
- [x] T004 [US1] Add `ArrayRoot` test method to `TLio.Yaml.Tests/Yaml/YamlFixtureTests.cs` — loads fixture triplets from `Fixtures/YamlArrayRoot/` via `YamlFixtureLoader`, exercises full pipeline with sequence as root
- [x] T005 [US1] Confirmed FAIL (Red) — 5/6 `YamlMultiDocParseTests` failed before fix; only `Parse_SingleDocument_ReturnsRootNodeDirectly` passed

### Implementation for User Story 1

- [x] T006 [US1] Updated `YamlNodeAdapter.Parse()` in `TLio.Yaml/YamlNodeAdapter.cs` — branches on `yaml.Documents.Count`: 0 → empty `YamlSequenceNode`, 1 → `Documents[0].RootNode`, N > 1 → `YamlSequenceNode` wrapping all roots
- [x] T007 [US1] Confirmed PASS (Green) — all 17 `TLio.Yaml.Tests` pass including new multi-doc and array-root fixture tests

**Checkpoint**: ✅ User Story 1 complete. Array-root pipeline fixtures confirm full
execution; unit parse tests confirm all 3 document-count branches.

---

## Phase 2: User Story 2 — Serialize Array Root (Priority: P2) ✅

**Goal**: A `YamlSequenceNode` root serializes to a valid YAML document with a
top-level sequence. No code change needed.

### Test for User Story 2

- [x] T008 [P] [US2] Write `YamlSerializeRoundTripTests` NUnit class in `TLio.Yaml.Tests/Yaml/YamlSerializeRoundTripTests.cs` — 4 inline tests: serializes a sequence root, verifies re-parse produces equivalent sequence, verifies field values survive round-trip, verifies empty sequence serializes cleanly
- [x] T009 [US2] Confirmed PASS (Green) — all 4 serialize round-trip tests pass with zero code changes to `Serialize()`

**Checkpoint**: ✅ User Story 2 complete. `Serialize()` already handles
`YamlSequenceNode` root. Round-trip coverage locked in.

---

## Phase 3: Polish & Constitutional Compliance ✅

- [x] T010 Article I / IX compliance grep — zero code references to format types in Core/Commands/Functions (two comments mentioning type names are acceptable)
- [x] T011 Article II compliance grep — zero results for `new.*Adapter|new.*Fetcher|new.*ExecutionContext` in Commands/Functions
- [x] T012 Full test suite — `dotnet test` — **850 tests pass, 0 failures** across all 6 test projects

| Project | Tests |
|---|---|
| TLio.UnitTests | 403 |
| TLio.Json.Tests | 114 |
| TLio.Json.SystemText.Tests | 33 |
| TLio.Functions.Tests | 248 |
| TLio.Xml.Tests | 31 |
| TLio.Yaml.Tests | 21 (+11 new) |

---

## Implementation Notes

- Fixture `02-set-on-all-elements` uses the `property` syntax (`path: $[*]`, `property: active`)
  rather than the legacy `path: $[*].active` syntax. The legacy path is broken for wildcards in
  `SplitParentAndLeaf` (tracked as a separate pre-existing bug).
- Parse-level tests use inline NUnit `[Test]` methods because the YAML fixture format
  uses `---` separators itself and cannot encode multi-document inputs.
- The fixture runner `YamlFixtureLoader` loads `Documents[0]` as input — when input is a
  YAML sequence (single document), it correctly feeds a `YamlSequenceNode` to the engine.
