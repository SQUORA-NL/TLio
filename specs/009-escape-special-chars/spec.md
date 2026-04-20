# Feature Specification: Special Character Escaping in Value and Path Parsing

**Feature Branch**: `009-escape-special-chars`
**Created**: 2026-04-19
**Status**: Draft
**Input**: User description: "special character escaping in path and value parsing — @ sign issues in FunctionConverter, = sign issues, and escaping for all path implementations so users can control literal vs. meaningful interpretation"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Escape trigger characters in value expressions (Priority: P1)

A script author wants to set a field to the literal string `@adminRole` but TLio interprets any value starting with `@` as a path expression, returning the wrong data instead of the literal text.

**Why this priority**: The `@` and `$` triggers are hit on the very first character — no workaround exists today without wrapping in quotes, and even then the `@@` escape in the WIP code only applies inside quoted strings, leaving unquoted literals and function arguments broken. This blocks correct scripts from being written.

**Independent Test**: Write a TLio `set` command whose `value` field is the literal string `@admin`. Without escaping, the engine navigates to the `admin` node; with the escape, the engine stores the string `@admin`. Fully testable with a unit test against `FunctionConverter.ParseValue`.

**Acceptance Scenarios**:

1. **Given** a raw value `@@admin`, **When** `ParseValue` is called, **Then** it returns a `FixedValue` containing the string `@admin` (not a `PathValue`).
2. **Given** a raw value `'@@admin'` (quoted), **When** `ParseValue` is called, **Then** it returns a `FixedValue` containing `@admin`.
3. **Given** a raw value `$$total` (double `$`), **When** `ParseValue` is called, **Then** it returns a `FixedValue` containing `$total` (not a `PathValue`).
4. **Given** a raw value `==formula`, **When** `ParseValue` is called, **Then** it returns a `FixedValue` containing `=formula` (not a function call).
5. **Given** a raw value `@address.city` (single `@`), **When** `ParseValue` is called, **Then** it still returns a `PathValue` (existing behaviour preserved).
6. **Given** the duplicate quoted-string block in `FunctionConverter.cs`, **When** the code is compiled, **Then** the dead/duplicate block is removed and only the escape-aware block exists.

---

### User Story 2 - Escape special characters inside function arguments (Priority: P2)

A script author passes a literal string starting with `@` or `=` as an argument to a function like `concat('@@prefix', @$.name)`. Currently the arg-splitting loop treats `$`/`@` args as path strings passed through and `=`…` as nested function calls — there is no way to express "I literally mean the string `@prefix`" inside a function argument.

**Why this priority**: Function arguments share the same parsing surface as top-level values; any escape scheme must be consistent across both contexts so authors don't need to learn two rule sets.

**Independent Test**: Call `concat('@@prefix', @$.name)` via `FunctionConverter`. The first arg should resolve to the fixed string `@prefix`; the second should remain a path. Testable via `FunctionConverter` unit tests without a full engine run.

**Acceptance Scenarios**:

1. **Given** a function arg `'@@token'` (quoted with double-`@` inside), **When** parsed, **Then** the argument value is the string `@token`.
2. **Given** a function arg `==raw`, **When** parsed, **Then** the argument value is the string `=raw`.
3. **Given** a function arg `$$ref`, **When** parsed, **Then** the argument value is the string `$ref`.
4. **Given** a function arg `@$.name` (single `@`), **When** parsed, **Then** it still passes through as a path string for the function to evaluate (existing behaviour preserved).

---

### User Story 3 - Escape special characters in path property names (Priority: P3)

A script author works with JSON data where a property key contains a dot, e.g., `{"version.major": 2}`. Writing `$.version.major` is ambiguous — the dot is also the path separator. The author needs a way to express "the property whose name is `version.major`" in each path format (JSON, XML, YAML).

**Why this priority**: This is a path-layer concern rather than a value-layer concern. Each format already has (or can adopt) a bracket/escape notation, so this is lower priority than fixing the value parser but still required for full round-trip correctness.

**Independent Test**: Using `JsonPathItemsFetcher`, write a path that selects a property named `"a.b"` from `{"a.b": 42}` and verify the correct node is returned. Independently testable per adapter with no other commands involved.

**Acceptance Scenarios**:

1. **Given** JSON data `{"a.b": 1}` and path `$['a.b']` (bracket notation), **When** `SelectNodes` is called, **Then** the node with value `1` is returned.
2. **Given** YAML data where a key contains a dot and bracket notation is used, **When** `SelectNodes` is called, **Then** the correct node is returned.
3. **Given** XML data with a `NativeXPathItemsFetcher` path that uses XPath predicate notation to select an element whose `@attribute` name contains a special character, **When** `SelectNodes` is called, **Then** the correct node is returned.
4. **Given** a slash-path XPath (`SlashPathItemsFetcher`) where a segment contains a `/`, **When** the path is constructed using the bracket/escape convention for that fetcher, **Then** the correct node is returned.

---

### Edge Cases

- What happens when `@@` appears in the middle of an unquoted value (e.g., `hello@@world`)? It must NOT be treated as an escape — the escape sequence only applies at the beginning of an unquoted value or anywhere inside a quoted string.
- What happens when `$$` or `==` appear in the middle of a value? Same rule — the double-prefix escape is only meaningful at position zero for unquoted values.
- What happens when a path property name contains both a dot and a bracket character? The bracket notation itself must be properly escaped or the behaviour documented.
- What happens when the same script is run against XML vs. JSON — do escape sequences remain consistent? Value-level escapes (`@@`, `$$`, `==`) must behave identically regardless of the adapter in use.
- What happens if a user writes `@@@foo` (triple `@`)? Should resolve to `PathValue(@foo)` — the first `@@` is the escape sequence, yielding `@`, then `@foo` is treated as a path. Document this behaviour explicitly.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The value parser MUST recognise `@@` at the start of an unquoted value as an escape sequence that produces a `FixedValue` containing the string that follows the first `@` with a leading `@` (i.e., `@@foo` → string `@foo`).
- **FR-002**: The value parser MUST recognise `$$` at the start of an unquoted value as an escape sequence that produces a `FixedValue` containing the string `$` + the remainder (i.e., `$$foo` → string `$foo`).
- **FR-003**: The value parser MUST recognise `==` at the start of an unquoted value as an escape sequence that produces a `FixedValue` containing `=` + the remainder (i.e., `==foo` → string `=foo`).
- **FR-004**: Inside quoted strings (`'…'` or `"…"`), `@@` MUST be decoded to a literal `@`; `$$` MUST be decoded to a literal `$`; `==` MUST be decoded to a literal `=`.
- **FR-005**: The same escape rules from FR-001–FR-004 MUST apply consistently inside function argument lists, not just at the top-level `ParseValue` call.
- **FR-006**: The duplicate/dead quoted-string block in `FunctionConverter.cs` MUST be removed; only one escape-aware block must remain.
- **FR-007**: Existing behaviour for single `@`, `$`, and `=` triggers MUST be fully preserved (no regression).
- **FR-008**: Each `IItemsFetcher` implementation MUST document which characters in property names require special handling and provide the bracket/escape notation or equivalent that allows those characters to be used literally.
- **FR-009**: `JsonPathItemsFetcher` MUST support bracket-notation paths (`$['prop.name']`) for property names that contain the path delimiter `.` or other JSONPath special characters.
- **FR-010**: `YamlPathItemsFetcher` MUST support an equivalent bracket or quoted-segment notation for property names containing `.`.
- **FR-011**: `NativeXPathItemsFetcher` MUST document how attributes (`@`) and predicates (`=`, `[`, `]`) are expressed when the element or attribute name itself contains those characters.
- **FR-012**: `SlashPathItemsFetcher` MUST document how a path segment containing `/` is expressed (e.g., URL-encoding or bracket notation).
- **FR-013**: Escape-sequence rules MUST be captured in `ai-ref.md` so the LLM assistant and human authors have a single authoritative reference.

### Key Entities

- **RawValue**: The string token read from a TLio script JSON property before type-resolution. Can be a path expression, a function call, a quoted literal, or an unquoted literal.
- **Escape sequence**: A two-character prefix (`@@`, `$$`, `==`) that signals "the following text is a literal string, not a trigger character".
- **PathExpression**: A `RawValue` that starts with `$` (root) or `@` (current node) and is passed to the active `IItemsFetcher`.
- **FunctionExpression**: A `RawValue` that starts with `=` and is parsed as a function call.
- **FixedValue**: A `RawValue` resolved to a constant node (string, number, boolean, null).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing tests in `TLio.Client` and `TLio.Functions.Tests` continue to pass after the change (zero regressions).
- **SC-002**: A script author can write a literal string value starting with `@`, `$`, or `=` without any workaround other than the defined escape sequence — verifiable by dedicated unit tests covering all three triggers in both top-level and function-argument positions.
- **SC-003**: The duplicate code block in `FunctionConverter.cs` is eliminated — verifiable by code review: exactly one branch handles quoted strings.
- **SC-004**: Each of the four `IItemsFetcher` implementations (JSON, JSON SystemText, XML Slash, XML XPath, YAML) has at least one passing test demonstrating a property name that contains that fetcher's delimiter character being addressed correctly.
- **SC-005**: The `ai-ref.md` escape-sequence section is added or updated and covers all three value-level escapes and links to per-fetcher path escape documentation.

## Assumptions

- The `@@` escape convention mirrors JLio's existing behaviour; `$$` and `==` are TLio extensions using the same double-prefix pattern for consistency.
- Escape sequences are only meaningful at parse time (in `FunctionConverter` and in path strings handed to fetchers) — the runtime data values themselves are never modified.
- Bracket notation for JSONPath (`$['key']`) is already supported by the underlying `jsonpath-plus` / `JsonPath.Net` library; the work here is validation and documentation, not a new library implementation.
- Mobile/web UI support for displaying escape sequences is out of scope for this feature.
- The `TlioLlmClient.cs` (in-progress LLM client) is not in scope for this feature.
