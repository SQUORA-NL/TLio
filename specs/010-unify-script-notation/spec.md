# Feature Specification: Unified Script Notation

**Feature Branch**: `010-unify-script-notation`  
**Created**: 2026-04-20  
**Status**: Draft  
**Input**: User description: "find and investigate the inconsistencies in the scription notations like have to have '' arround arguments and sometimes not using @propertyname or @.propertyname? anything that is not consistent i want to know and a resultion to make it consistent so we can plan and implement"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Understand the JSON-First Context and Quoting Rules (Priority: P1)

A developer writing TLio scripts needs to internalise one foundational rule above all others: **a TLio script is always a valid JSON document**. Every command property (including `value`) is a JSON string. Inside those JSON strings, special prefixes and delimiters carry meaning:

- A JSON string value starting with `=` is a **function expression** — single quotes around the whole expression are not needed and would be wrong.
- Inside a function expression (which is already delimited by the outer `"..."`), **literal string arguments** must be wrapped in single quotes `'...'` because the outer JSON string already uses double quotes.
- **Path arguments** inside functions (`$.field`, `@.field`) never need quotes.

Today this rule is never stated explicitly, leading to expressions like `"'=concat(...)"` (wrong: outer single-quoting a function) or `"=concat($.a, $.b, world)"` (wrong: `world` must be quoted as `'world'`).

**Why this priority**: This is the single most important rule. All other notation consistency depends on developers first understanding the JSON-string embedding context.

**Independent Test**: A developer who has never used TLio can read the notation reference and write the following correctly without help: (a) a `set` command with a plain string value, (b) a `set` command with a function expression that concatenates two paths with a literal separator.

**Acceptance Scenarios**:

1. **Given** a developer wants to set a field to a literal string, **When** they read the notation reference, **Then** they find: a plain JSON string value like `"Alice"` is valid as-is; no single quotes needed at the value level.
2. **Given** a developer wants to concatenate two fields with a literal hyphen separator, **When** they write `"=concat($.first, '-', $.last)"`, **Then** the script runs correctly, and the notation reference confirms: `=` starts the function, `'-'` is a single-quoted literal argument, `$.first` and `$.last` are unquoted path arguments.
3. **Given** a developer writes `"'=concat($.a, $.b)'"` with outer single quotes around a function, **When** they run the script, **Then** the expression is **evaluated** — a `'...'` string whose content starts with `=` is a nested expression, which is what makes dynamic paths such as `=fetch('=indirect($.pathRef)')` possible. To store the literal text, double the `=`: `"'==concat($.a, $.b)'"`.
   > Corrected 2026-08-18. This scenario originally stated the opposite; the implementation has always evaluated it, and the fixture that "verified" the literal reading was never loaded by the test harness (only `fixture.json` is). Live coverage now exists in `Fixtures/Notation/quoted-expression-evaluates/`.
4. **Given** a developer writes `"=concat($.a, $.b, hello)"` with an unquoted literal `hello`, **When** they run the script, **Then** the fallback plain-string rule applies (treating it as a literal), but the notation reference clarifies that literal string arguments SHOULD be single-quoted to be unambiguous: `'hello'`.

---

### User Story 2 - Understand the `@` Symbol Unambiguously (Priority: P1)

A developer encounters `@` in TLio expressions in three very different roles: as a relative-path prefix (`@.propertyName`), as an escape prefix (`@@` to produce a literal `@`), and as an XML attribute selector in XPath (`item[@id='1']`). No single document explains all three usages and which contexts each applies to.

**Why this priority**: The `@` overloading is the highest-impact ambiguity because misusing it causes silent wrong results rather than errors, and the XML vs JSON distinction makes it cross-adapter.

**Independent Test**: A developer can correctly use a relative path (`@.childProp`), escape a literal `@` character, and (if using XML) reference an XML attribute — all after reading only the notation reference.

**Acceptance Scenarios**:

1. **Given** a developer is writing a JSON transformation and wants to reference a property on the current item, **When** they read the notation reference, **Then** they find a clear rule: use `@.propertyName` (with the dot) for relative paths in JSON/YAML context.
2. **Given** a developer wants to produce a string containing a literal `@` sign, **When** they consult the notation reference, **Then** they find `@@` is the escape sequence and works both inside and outside single-quoted string arguments.
3. **Given** a developer is writing an XML script using XPath, **When** they consult the notation reference, **Then** they find that `@attrName` (without dot, inside a path predicate) is the XPath attribute selector and is explicitly distinguished from the relative-path `@.` notation.
4. **Given** a developer writes `@propertyName` (without the dot) in a JSON script, **When** they consult the notation reference, **Then** it clearly states this form is not valid for relative paths in JSON/YAML — the dot is always required.

---

### User Story 3 - Understand Token Handling Inside Quoted String Arguments (Priority: P2)

A developer writing function arguments that contain special characters (`@`, `$`, `=`) needs to know how to escape them inside single-quoted string arguments. Currently the escape sequences (`@@`, `$$`, `==`) are documented in the overview but not clearly tied to the specific context of being inside a `'...'` argument within a `"..."` JSON string value.

**Why this priority**: Without understanding this, developers produce incorrect scripts when dealing with email addresses, variable references, or expression-looking strings in arguments.

**Independent Test**: A developer can write a `concat` expression that produces the output string `user@example.com` without any search or guessing, after reading only the notation reference.

**Acceptance Scenarios**:

1. **Given** a developer wants to pass a literal `@` inside a function argument, **When** they consult the notation reference, **Then** they find: inside `'...'` use `@@` to produce a literal `@` — e.g., `'user@@example.com'` produces `user@example.com`.
2. **Given** a developer wants to pass a literal `$` inside a function argument, **When** they consult the notation reference, **Then** they find: inside `'...'` use `$$` to produce a literal `$`.
3. **Given** a developer wants to pass a string starting with `=` as a literal, **When** they consult the notation reference, **Then** they find: inside `'...'` use `==` to produce a leading `=` — e.g., `'==formula'` produces `=formula`.
4. **Given** a developer uses `@@`, `$$`, or `==` outside of any quoted string (at the value level), **When** the script runs, **Then** the escape is applied the same way, and the notation reference documents this consistency explicitly.

---

### User Story 4 - Consistent Notation Across All Documentation (Priority: P2)

A developer reading any TLio documentation — whether a function page, command reference, or overview — sees the same notation conventions applied consistently. Currently, abstract syntax signatures and concrete examples are mixed without explanation, and spacing around commas is inconsistent.

**Why this priority**: Inconsistent documentation erodes trust and makes it harder to generalise from one example to another.

**Independent Test**: Pick any 5 function pages and any 3 command pages; all use the same notation convention for abstract syntax signatures, and all examples apply quoting and path notation consistently.

**Acceptance Scenarios**:

1. **Given** a developer reads any function reference page, **When** they see the abstract syntax line, **Then** it uses a consistent placeholder convention distinguishing abstract placeholders from concrete paths or literals.
2. **Given** a developer reads any two function reference pages, **When** they compare the syntax notation style, **Then** spacing, quote usage, and placeholder naming follow the same pattern.
3. **Given** a developer reads the escape-sequence section, **When** they compare it with function-level examples, **Then** escaping is shown identically in both places.

---

### User Story 5 - Authoritative Notation Reference Document (Priority: P2)

A single authoritative "Notation Reference" section (or document) exists that defines all TLio expression syntax rules in one place. Today a developer must read multiple files and reconcile contradictions themselves.

**Why this priority**: All other consistency work depends on having one source of truth that other documents can point to.

**Independent Test**: A developer with a syntax question can open one document and find a definitive answer without needing to cross-reference multiple files.

**Acceptance Scenarios**:

1. **Given** the notation reference exists, **When** a developer looks up "when to use quotes," **Then** they find the JSON-context rule explained (outer `"..."` = JSON string, inner `'...'` = literal string argument) in one section.
2. **Given** the notation reference exists, **When** a developer looks up "relative paths," **Then** they find a clear definition of `@.`, `@.<--`, and how these compose.
3. **Given** the notation reference exists, **When** a developer looks up "escape sequences," **Then** they find a complete table with all supported escape forms and whether each works inside or outside `'...'` quoted arguments.

---

### Edge Cases

- What happens when a developer uses `@propertyName` (no dot) in a JSON script? The system must produce a descriptive error directing them to use `@.propertyName`.
- What happens when a developer wraps a function expression in outer single quotes (`"'=funcName(...)'"`)? The system must treat the whole value as a literal string starting with `=funcName...`, not a function call. The notation reference must warn about this.
- In XML mode, `@attr` inside a predicate is valid XPath; outside a predicate it is ambiguous. The notation reference must clarify the context boundary.
- Escape sequences `@@`, `$$`, `==` must behave identically inside and outside `'...'` quoted arguments; any difference must be documented and tested.
- A plain unquoted word as a function argument (e.g., `=concat($.a, hello)`) relies on the fallback plain-string rule. The notation reference must clarify whether this is intentional or discouraged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A formal notation rule MUST state that a TLio script is always a valid JSON document (a JSON array of command objects), and that all value expressions are JSON strings.
- **FR-002**: A formal notation rule MUST define the quoting context: a JSON string value starting with `=` is a function expression; literal string arguments inside that expression must be wrapped in single quotes `'...'` because the outer JSON string already uses double quotes; path arguments (`$.field`, `@.field`) must not be quoted.
- **FR-003**: A formal notation rule MUST explicitly state that function expressions (`=funcName(...)`) must NOT be wrapped in outer single quotes at the JSON string value level; doing so makes the value a literal string, not a function call.
- **FR-004**: A formal notation rule MUST define that relative paths in JSON/YAML scripts always use the `@.propertyName` form (with a dot), and that `@propertyName` (without a dot) is invalid in this context and produces a descriptive parse error.
- **FR-005**: A formal notation rule MUST define the three distinct roles of `@` with clear context rules: (a) `@.` prefix for relative paths in JSON/YAML, (b) `@@` escape sequence for a literal `@` character, (c) `@attrName` inside XPath predicates for XML attribute selection.
- **FR-006**: A formal notation rule MUST define escape sequences (`@@`, `$$`, `==`) and confirm they work identically inside and outside `'...'` single-quoted arguments, covering all three cases with examples.
- **FR-007**: All existing function reference pages and command reference pages MUST be updated to use the unified notation conventions, eliminating discrepancies between abstract signatures and concrete examples.
- **FR-008**: An authoritative "Notation Reference" document MUST be produced that covers: the JSON-validity rule, value types (literal vs. path vs. function expression), quoting context and rules, `@` roles, escape sequences, bracket-notation paths, and parent-navigation syntax (`@.<--`).
- **FR-009**: Existing parser behaviour MUST be verified (and corrected where needed) to match the formalised rules, and unit tests MUST cover each rule boundary.

### Key Entities

- **Notation Rule**: A named, versioned rule defining a specific syntax constraint (e.g. "JSON-First", "LiteralArgumentQuoting", "RelativePathDotRequired").
- **Notation Reference Document**: The single authoritative document listing all notation rules with examples.
- **Notation Inconsistency**: A documented discrepancy between rules, documentation, or parser behaviour that is resolved by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero notation inconsistencies remain between the notation reference document and all function/command reference pages — verifiable by a checklist review of all documentation pages.
- **SC-002**: Every notation rule has at least one passing automated test covering the rule boundary (both the valid and invalid form).
- **SC-003**: A developer unfamiliar with TLio can correctly write a non-trivial script (at least one function expression, one relative path, one literal argument) on the first attempt after reading only the notation reference — verifiable through reviewer walkthrough.
- **SC-004**: Every `@` usage in documentation is unambiguously labelled by its role (relative path, escape, or XPath attribute), with no unlabelled occurrences remaining.

## Assumptions

- The core parser behaviour for quoting and escape sequences is already correct; the primary gaps are: (a) documentation, (b) the missing `@propertyName` (no-dot) validation error, and (c) the absence of a warning when a function expression is incorrectly outer-quoted.
- XML XPath attribute syntax (`@attr` inside predicates) is intentionally different from JSON relative-path syntax and is not a bug — the notation reference makes this distinction explicit.
- The bracket-notation write limitation (tracked on `fix/bracket-write`) is out of scope; the notation reference documents the current read-supported / write-in-progress status without resolving it.
- The parent-navigation syntax (`@.<--`) is an existing intentional feature; this feature formalises its documentation, not its implementation.
- Notation rules apply equally to JSON, YAML, and XML adapters, with explicit callouts where adapter-specific behaviour differs.
- An unquoted plain word used as a function argument (relying on the fallback plain-string rule) is treated as a valid literal; the notation reference will document this behaviour and recommend single-quoting for clarity.
