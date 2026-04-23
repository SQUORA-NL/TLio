# Tasks: Deepen Test Coverage

**Input**: Design documents from `/specs/016-deepen-test-coverage/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, quickstart.md ✅

**Note**: This is test-only work — all tasks write new or expanded test code. No production code changes. No new projects needed.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify build is clean and tests pass before adding coverage.

- [X] T001 Verify `dotnet build` and `dotnet test` pass with zero failures from baseline

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Understand current test gaps before writing new tests.  
No blocking infrastructure changes needed — all test infrastructure already exists.

- [X] T002 Read `TLio.Functions.Tests/FunctionsTests/TextTests/ConcatTests.cs` to understand existing test setup pattern for text functions
- [X] T003 Read `TLio.UnitTests/CommandsTests/AddTests.cs` to confirm `context.GetLogEntries()` usage and `LogLevel` import pattern

**Checkpoint**: Patterns confirmed — user story work can begin

---

## Phase 3: User Story 1 - Text Function Coverage (Priority: P1) 🎯 MVP

**Goal**: All 22 text functions have ≥ 3 passing tests each; `ToStringFunction` goes from 0 to ≥ 5 tests.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~TextTests"` — all text function tests pass.

- [X] T004 [P] [US1] Create `TLio.Functions.Tests/FunctionsTests/TextTests/ToStringTests.cs` with tests: integer→string, float→string, bool→string, null→empty-string, object→JSON, array→JSON, no-args→failure
- [X] T005 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/ConcatTests.cs` — add: success test (concatenates 3 strings), empty-string args produce empty result, single arg works
- [X] T006 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/SubstringTests.cs` — add: happy path (start+length), start only (to end), out-of-range start returns empty/fails
- [X] T007 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/CaseTests.cs` — add: toLower on already-lowercase string, toUpper on already-uppercase string, toLower with numbers/symbols unchanged
- [X] T008 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/TrimTests.cs` — add: trimStart only trims left, trimEnd only trims right, trim on string with no whitespace is unchanged
- [X] T009 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/PredicateTests.cs` — add: startsWith match, endsWith match, contains match, all three with empty-string needle
- [X] T010 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/ReplaceTests.cs` — add: replace with empty replacement (deletion), replace non-existent pattern returns original, replace all occurrences
- [X] T011 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/SplitJoinTests.cs` — add: split on multi-char delimiter, join empty array produces empty string, split produces correct count
- [X] T012 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/IndexOfTests.cs` — add: not-found returns -1, found at position 0, case-sensitive (no match on wrong case)
- [X] T013 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/LengthTests.cs` — add: length of empty string is 0, length of unicode string counts chars not bytes
- [X] T014 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/PadTests.cs` — add: padLeft with custom char, padRight to exact length, pad when string already longer than width returns original
- [X] T015 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/IsEmptyTests.cs` — add: non-empty string returns false, whitespace-only string (isEmpty returns true or false per impl), null returns true
- [X] T016 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/FormatTests.cs` — add: format with no placeholders returns template unchanged, format with multiple placeholders
- [X] T017 [P] [US1] Expand `TLio.Functions.Tests/FunctionsTests/TextTests/ParseTests.cs` — add: parse valid integer, parse valid double, parse invalid string returns failure

**Checkpoint**: All 22 text functions have ≥ 3 tests. `dotnet test --filter "FullyQualifiedName~TextTests"` passes.

---

## Phase 4: User Story 2 - Logging Assertion Tests (Priority: P2)

**Goal**: Primary warning/error/info log paths for text functions, math functions, and built-in functions all have explicit log assertions.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~TextTests|FullyQualifiedName~MathTests|FullyQualifiedName~FetchTests|FullyQualifiedName~IndirectTests"` — log assertion tests pass.

- [X] T018 [P] [US2] Add logging assertions to `TLio.Functions.Tests/FunctionsTests/TextTests/ToStringTests.cs` — assert `LogWarning` on no-args path, assert `LogInfo` on success path (integer→string)
- [X] T019 [P] [US2] Add logging assertions to `TLio.Functions.Tests/FunctionsTests/TextTests/ConcatTests.cs` — assert `LogWarning` on no-args, assert `LogError` on path-not-found
- [X] T020 [P] [US2] Add logging assertions to `TLio.Functions.Tests/FunctionsTests/TextTests/SubstringTests.cs` — assert `LogError` on too-few-args
- [X] T021 [P] [US2] Add logging assertions to `TLio.Functions.Tests/FunctionsTests/TextTests/CaseTests.cs` — assert `LogError` on path-not-found (toLower with missing path arg)
- [X] T022 [P] [US2] Add logging assertions to `TLio.Functions.Tests/FunctionsTests/TextTests/PredicateTests.cs` — assert `LogError` on path-not-found for startsWith
- [X] T023 [P] [US2] Add logging assertions to `TLio.Functions.Tests/FunctionsTests/TextTests/IndexOfTests.cs` — assert `LogError` on path-not-found
- [X] T024 [P] [US2] Add logging assertions to `TLio.Functions.Tests/FunctionsTests/TextTests/LengthTests.cs` — assert `LogError` on path-not-found
- [X] T025 [P] [US2] Add logging assertions to `TLio.Functions.Tests/FunctionsTests/TextTests/FormatTests.cs` — assert `LogError` on missing-template path
- [X] T026 [P] [US2] Add logging test to `TLio.Functions.Tests/FunctionsTests/FetchTests.cs` — assert `LogWarning` or `LogError` on ReturnsFalseForNonExistentPath test
- [X] T027 [P] [US2] Add logging test to `TLio.Functions.Tests/FunctionsTests/IndirectTests.cs` — assert `LogWarning` or `LogError` on missing-key path

**Checkpoint**: Log assertions added. All tests pass.

---

## Phase 5: User Story 3 - ToCsv Command Tests (Priority: P3)

**Goal**: Dedicated `ToCsvTests.cs` covers edge cases not already in `ETL_Tests.cs`.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~ToCsvTests"` — all pass.

- [X] T028 [US3] Read `TLio.Functions.Tests/FunctionsTests/ETLTests/ETL_Tests.cs` lines 113-220 to inventory which ToCsv scenarios are already covered (array, single-row, comma-in-field, single-object, missing-field)
- [X] T029 [US3] Read `TLio.Extensions.ETL/Commands/ToCsv.cs` and `TLio.Extensions.ETL/Commands/Models/CsvSettings.cs` to understand the full `CsvSettings` API (delimiter, includeHeaders, etc.)
- [X] T030 [US3] Create `TLio.Functions.Tests/FunctionsTests/ETLTests/ToCsvTests.cs` with tests for: empty array → headers only, null field → empty cell, custom tab delimiter, custom semicolon delimiter, array with inconsistent fields (missing key in some rows), validation failure (empty path), validation failure (null CsvSettings)

**Checkpoint**: `ToCsvTests.cs` created with ≥ 7 passing tests.

---

## Phase 6: User Story 4 - Math Function Edge Cases (Priority: P3)

**Goal**: Math aggregate functions handle empty arrays, zero, negatives, and single-element inputs.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~MathEdgeCaseTests"` — all pass.

- [X] T031 [US4] Read `TLio.Functions.Tests/FunctionsTests/MathTests/SumTests.cs` and `AvgTests.cs` to understand the existing test setup pattern for math functions
- [X] T032 [US4] Read `TLio.Functions.Tests/FunctionsTests/MathTests/MathNullHandlingTests.cs` to confirm what null/empty handling is already covered
- [X] T033 [US4] Create `TLio.Functions.Tests/FunctionsTests/MathTests/MathEdgeCaseTests.cs` with: Sum empty array, Avg empty array, Min empty array, Max empty array, Count empty array → 0, Sum single-element returns that element, Min/Max with single element, Sum/Avg with all-negative values, Sum/Avg with zero values in array, Min/Max with identical values

**Checkpoint**: `MathEdgeCaseTests.cs` created with ≥ 10 passing tests.

---

## Phase 7: User Story 5 - Built-in Function Error Paths (Priority: P4)

**Goal**: Fetch, Indirect, Partial, Datetime functions all have ≥ 1 error-path test asserting graceful failure.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~FetchTests|FullyQualifiedName~IndirectTests|FullyQualifiedName~PartialTests|FullyQualifiedName~DatetimeFunctionTests"` — all pass.

- [X] T034 [P] [US5] Read `TLio.Functions.Tests/FunctionsTests/IndirectTests.cs` and `TLio.Functions.Tests/FunctionsTests/PartialTests.cs` to see existing error-path coverage
- [X] T035 [P] [US5] Read `TLio.Functions.Tests/FunctionsTests/DatetimeFunctionTests.cs` to see existing test coverage
- [X] T036 [P] [US5] Expand `TLio.Functions.Tests/FunctionsTests/IndirectTests.cs` — add test: indirect with a key that doesn't resolve to a valid path → returns failure and logs warning/error
- [X] T037 [P] [US5] Expand `TLio.Functions.Tests/FunctionsTests/PartialTests.cs` — add test: partial with empty arguments → returns failure and logs warning/error
- [X] T038 [P] [US5] Expand `TLio.Functions.Tests/FunctionsTests/DatetimeFunctionTests.cs` — add tests: invalid date string returns null/failure, empty string argument returns null/failure, logs appropriate warning/error

**Checkpoint**: All four built-in functions have ≥ 1 error-path test. All tests pass.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T039 Run `dotnet test` from repo root and confirm zero failures, capture total test count (should be > 1114)
- [X] T040 [P] Verify no production code was modified: `git diff --name-only` should show only test files

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup; must read existing code before writing
- **US1 (Phase 3)**: Can start after Phase 2 — no dependencies on other stories
- **US2 (Phase 4)**: Can largely run in parallel with US1 (adds logging to same files); best after US1 tasks complete
- **US3 (Phase 5)**: Independent after Phase 2
- **US4 (Phase 6)**: Independent after Phase 2
- **US5 (Phase 7)**: Independent after Phase 2
- **Polish (Phase 8)**: After all desired user stories complete

### User Story Dependencies

- **US1 (P1)**: No dependencies on other stories
- **US2 (P2)**: Adds logging assertions to files created in US1; best after US1 completes
- **US3 (P3)**: Fully independent
- **US4 (P3)**: Fully independent
- **US5 (P4)**: Fully independent

### Parallel Opportunities

- T004–T017 (US1 text function tasks): ALL can run in parallel (different files)
- T018–T027 (US2 logging tasks): All can run in parallel (different files or appending to US1 files)
- T036–T038 (US5): All can run in parallel

---

## Parallel Example: User Story 1 Text Functions

```text
All can run simultaneously (each touches a different file):
- Task T004: Create ToStringTests.cs
- Task T005: Expand ConcatTests.cs
- Task T006: Expand SubstringTests.cs
- Task T007: Expand CaseTests.cs
- Task T008: Expand TrimTests.cs
- Task T009: Expand PredicateTests.cs
- Task T010: Expand ReplaceTests.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (T001) + Phase 2 (T002–T003)
2. Complete Phase 3: US1 — text function coverage (T004–T017)
3. **VALIDATE**: `dotnet test --filter "FullyQualifiedName~TextTests"` all pass

### Incremental Delivery

1. Setup + Foundational → baseline confirmed
2. US1 → 22 text functions covered (highest value)
3. US2 → logging assertion coverage added
4. US3 → ToCsv edge cases covered
5. US4 → math edge cases covered
6. US5 → built-in error paths covered
7. Polish → full test suite confirmed

---

## Notes

- All tasks write test code only — zero production code changes
- `context.GetLogEntries()` requires `using Microsoft.Extensions.Logging;` in test files
- `result.Data.First!.Value<bool>()` for bool results; `result.Data[0].ToObject<string>()` for strings
- See `quickstart.md` for code patterns
- Constitution Article VI: inline `[TestCase]` is acceptable for unit-level edge cases (null args, path-not-found) — no fixture triplets required for these
- Constitution Article XI: no new commands or functions added — no ai-ref.md required
