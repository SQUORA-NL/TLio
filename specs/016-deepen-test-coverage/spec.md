# Feature Specification: Deepen Test Coverage

**Feature Branch**: `016-deepen-test-coverage`  
**Created**: 2026-04-23  
**Status**: Draft  

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Text Function Coverage (Priority: P1)

A developer using the TLio text extension pack needs confidence that all 22 text functions behave correctly across normal usage, boundary values, and null/empty inputs. Currently `ToStringFunction` has zero tests and several other functions have only 1–2 tests, leaving large gaps in correctness assurance.

**Why this priority**: Text functions are the most-used extension pack. Gaps here directly risk silent regressions in production pipelines. `ToStringFunction` has zero test coverage — any defect would be invisible.

**Independent Test**: Can be fully tested by running `TLio.Functions.Tests` and verifying all 22 text functions have ≥ 3 passing tests each.

**Acceptance Scenarios**:

1. **Given** `ToStringFunction` exists with zero tests, **When** tests are added for integer, float, bool, null, and array inputs, **Then** all tests pass and coverage reaches 100% of documented behaviors.
2. **Given** each of the 22 text functions has fewer than 3 tests, **When** additional edge-case tests are added (empty string, null, unicode, whitespace), **Then** each function has at minimum 3 passing tests.
3. **Given** `SubstringFunction` receives out-of-range indices, **When** the function is invoked, **Then** it returns an empty string or null without throwing an unhandled exception.
4. **Given** `ReplaceFunction` receives an empty pattern, **When** the function is invoked, **Then** the result is predictable and documented.

---

### User Story 2 - Logging Assertion Tests (Priority: P2)

A developer maintaining TLio functions needs assurance that warning and error messages are emitted when functions are given bad inputs, so that pipelines operating in production can be monitored via logs. Currently 60 function test files contain zero log assertions.

**Why this priority**: Logging is the primary observability mechanism for TLio pipelines. Without log assertions, warning/error paths can silently break. This directly impacts diagnosability of real-world issues.

**Independent Test**: Can be fully tested by running all function test files and verifying at least the most critical warning/error log paths have explicit assertions.

**Acceptance Scenarios**:

1. **Given** a math function receives a null or invalid input, **When** the function runs, **Then** a warning-level log entry is captured and asserted in the test.
2. **Given** a text function receives a null input where it produces a fallback, **When** the function runs, **Then** the appropriate log message is captured.
3. **Given** a built-in function (e.g., `FetchFunction`, `IndirectFunction`) encounters a missing path or invalid reference, **When** the function runs, **Then** an error or warning log entry is asserted.

---

### User Story 3 - ToCsv Command Tests (Priority: P3)

A developer using the `ToCsv` command needs a dedicated test suite that validates CSV generation across all supported configurations: single objects, arrays, custom delimiters, headers, and null handling.

**Why this priority**: ToCsv has zero dedicated tests. Any CSV generation regression would go undetected.

**Independent Test**: Can be fully tested by running `TLio.UnitTests` and verifying a new `ToCsvCommandTests.cs` file exists with passing tests for all major scenarios.

**Acceptance Scenarios**:

1. **Given** a JSON array of objects, **When** the `ToCsv` command is applied, **Then** the output is a well-formed CSV string with headers matching object keys.
2. **Given** a single JSON object, **When** the `ToCsv` command is applied, **Then** the output is a one-row CSV with a header row.
3. **Given** a custom delimiter is specified, **When** the `ToCsv` command runs, **Then** the delimiter character appears between values.
4. **Given** a field contains a null value, **When** the `ToCsv` command runs, **Then** the null cell renders as an empty string without errors.
5. **Given** an empty array is passed, **When** the `ToCsv` command runs, **Then** only a header row (or empty output) is produced without exceptions.

---

### User Story 4 - Math Function Edge Cases (Priority: P3)

A developer using TLio math functions needs assurance that edge cases — empty collections, zero divisors, single-element arrays, and negative values — are handled predictably and do not cause unhandled exceptions.

**Why this priority**: Numeric edge cases (divide by zero, empty aggregates) are common in real data pipelines and can cause silent failures or crashes if untested.

**Independent Test**: Can be fully tested by running `TLio.Functions.Tests` and verifying edge-case tests pass for all math functions.

**Acceptance Scenarios**:

1. **Given** `SumFunction` receives an empty array, **When** it executes, **Then** it returns 0 or null without throwing.
2. **Given** `AverageFunction` receives an empty array, **When** it executes, **Then** it returns null or 0 without throwing.
3. **Given** `DivideFunction` receives a denominator of zero, **When** it executes, **Then** it returns null or a documented sentinel value and emits a warning log.
4. **Given** `MinFunction` and `MaxFunction` receive a single-element array, **When** they execute, **Then** they return that element.
5. **Given** math functions receive negative numeric values, **When** they execute, **Then** results are mathematically correct.

---

### User Story 5 - Built-in Function Error Paths (Priority: P4)

A developer using TLio built-in functions (`FetchFunction`, `IndirectFunction`, `PartialFunction`, `DatetimeFunction`) needs assurance that invalid or missing inputs are handled gracefully with appropriate return values and log messages rather than unhandled exceptions.

**Why this priority**: Error paths in built-in functions are the last line of defense before pipeline crashes. Coverage here ensures robust failure handling.

**Independent Test**: Can be fully tested by running `TLio.Functions.Tests` and verifying error-path tests exist for each of the four functions.

**Acceptance Scenarios**:

1. **Given** `FetchFunction` is called with a null or non-existent path, **When** it executes, **Then** it returns null or an empty result without throwing.
2. **Given** `IndirectFunction` references a key that does not exist in the document, **When** it executes, **Then** it returns null or empty and logs a warning.
3. **Given** `PartialFunction` receives fewer arguments than required, **When** it executes, **Then** it returns an error result or null without throwing.
4. **Given** `DatetimeFunction` receives an unparseable date string, **When** it executes, **Then** it returns null and logs an appropriate error message.

---

### Edge Cases

- What happens when text functions receive unicode characters (emoji, multi-byte)?
- How do math functions handle `NaN` or `Infinity` floating-point values?
- What happens when `ToCsv` receives nested objects (non-flat structure)?
- How does `IndirectFunction` behave with circular references?
- What happens when `DatetimeFunction` receives an empty string vs null?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All 22 text functions in `TLio.Extensions.Text` MUST have a minimum of 3 test cases each in `TLio.Functions.Tests`.
- **FR-002**: `ToStringFunction` MUST have tests covering integer, float, boolean, null, and array-to-string conversions.
- **FR-003**: Each text function MUST have at least one test for null/empty input behavior.
- **FR-004**: At least the primary warning/error log paths for math, text, and built-in functions MUST have explicit log capture assertions in tests.
- **FR-005**: A dedicated `ToCsvCommandTests.cs` file MUST exist with tests for array input, single-object input, custom delimiter, null values, and empty input.
- **FR-006**: Math function tests MUST cover: empty collection inputs, zero/negative values, and single-element collections for all aggregate functions.
- **FR-007**: `FetchFunction`, `IndirectFunction`, `PartialFunction`, and `DatetimeFunction` MUST each have at least one test for an invalid or missing input that verifies graceful failure.
- **FR-008**: All new tests MUST pass `dotnet test` with zero failures.
- **FR-009**: Tests MUST use the existing `ILogger` capture pattern established in the project (no new test infrastructure required).

### Key Entities

- **Text Functions (22)**: concat, toString, parse, format, length, substring, replace, toLower, toUpper, trim, startsWith, endsWith, contains, indexOf, padLeft, padRight, split, join, repeat, reverse, truncate, base64Encode/Decode (as applicable)
- **Math Functions**: sum, average, min, max, count, divide, multiply, round (aggregate and arithmetic)
- **Built-in Functions**: FetchFunction, IndirectFunction, PartialFunction, DatetimeFunction
- **ToCsv Command**: command accepting a path to a JSON array/object and producing CSV text output

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 22 text functions have ≥ 3 passing tests each (measurable by test count per function class).
- **SC-002**: `ToStringFunction` goes from 0 tests to ≥ 5 tests covering all supported input types.
- **SC-003**: At least 20 log assertion tests are added across the function test files (currently 0).
- **SC-004**: `ToCsv` command has ≥ 5 passing dedicated tests covering all major scenarios.
- **SC-005**: All math functions have edge-case tests for empty, single-element, zero, and negative inputs.
- **SC-006**: Each of the 4 built-in functions has ≥ 1 error-path test verifying graceful failure.
- **SC-007**: `dotnet test` reports 0 failures after all changes.

## Assumptions

- The existing `ILogger` test capture pattern (e.g., capturing `MockLogger` or similar) is already established and will be reused — no new logging infrastructure is needed.
- The 22 text functions are all already implemented; only tests are missing.
- `ToCsv` command is already implemented in `TLio.Commands`; only tests are missing.
- Tests are written using NUnit 4.x with the same conventions used in existing test files.
- Unicode/emoji edge cases are tested as best-effort; full internationalization is out of scope.
- Performance tests for new functions are out of scope for this feature.
