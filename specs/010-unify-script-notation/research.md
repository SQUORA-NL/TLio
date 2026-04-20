# Research: Unified Script Notation

**Branch**: `010-unify-script-notation` | **Date**: 2026-04-20

## 1. Current Parser Behaviour (FunctionConverter.cs)

**Decision**: The parser is largely correct; the primary gaps are documentation and one missing validation.

**Findings from `TLio.Client/FunctionConverter.cs`:**

The `ParseValue` method applies these rules in priority order:
1. If value is `null` JSON node → `NullValue`
2. If value is a non-string JSON node (number, boolean, array, object) → `FixedValue` (raw JSON)
3. If string starts with `=` → function call (parse as `FunctionValue`)
4. If string starts with `$` or `@` → path expression (`PathValue`)
5. If string is single- or double-quoted → strip quotes, apply escape decoding → `FixedValue`
6. If string starts with `@@`, `$$`, `==` → strip first char, produce `FixedValue`
7. Otherwise → plain string literal → `FixedValue`

**The `@` without dot issue**: When a value starts with `@` but is NOT followed by `.` (e.g., `@name`), the parser currently treats it as a `PathValue` and passes `@name` to the path fetcher. The fetcher may silently fail or return unexpected results depending on the path dialect. No validation error is raised.

**Escape sequences inside quoted strings** (`SplitArgs` + `UnescapeValue`): `@@` → `@`, `$$` → `$`, `==` → `=`. This applies identically inside and outside `'...'` delimiters. **Confirmed consistent.**

**Rationale for the approach**: No parser rewrite is needed. The one code change required is: in `ParseValue`, after identifying a `@`-prefixed string as a path, check if the `@` is followed by `.` (or is `@@` escape or `@.` relative path or `@` used inside a JSONPath filter). If `@` is followed by a plain identifier character (not `.`), emit a warning/error through `IExecutionLogger` directing the user to `@.propertyName`.

---

## 2. Notation Inconsistencies Catalogue

All inconsistencies discovered during investigation:

### A. Abstract syntax vs concrete examples (all function ai-ref.md files)

`Concat.md` syntax shows `=concat(a, b)` — abstract, no quotes — but the example shows `=concat($.first,' ',$.last)` with a quoted literal. A developer reading only the syntax line would not know `' '` is required.

**Resolution**: Update abstract syntax placeholders to use a convention that makes the type explicit (e.g., `=concat(<path-or-value>, <path-or-value>)`), and add a note pointing to the notation reference.

### B. `@propertyName` vs `@.propertyName` — no documented rule

No documentation states that `@.propertyName` (with dot) is the required form for relative paths. `@propertyName` (without dot) silently works in some contexts and fails in others.

**Resolution**: FR-004 — add validation; update notation reference with the rule.

### C. Three roles of `@` — undocumented overloading

`@.` (relative path), `@@` (escape), `@attr` (XPath attribute inside predicates) are three unrelated usages of `@`. No document lists them together or distinguishes the contexts.

**Resolution**: FR-005 — notation reference table with context column.

### D. JSON-first context never stated

The fact that TLio scripts must be valid JSON — and that this determines why `'...'` is used for inner string literals (because the outer delimiter is `"`) — is never explained anywhere.

**Resolution**: FR-001 and FR-002 — opening section of notation reference.

### E. `overview.md` bracket-notation note references `010-bracket-write`

The note `"tracked as follow-up issue 010-bracket-write"` at line 122 refers to the current branch by number but won't stay correct. Not blocking; update to refer to the `fix/bracket-write` branch name.

---

## 3. Scope of Documentation Changes

**Files to update:**

| File | Change |
|------|--------|
| `docs/ai-ref/overview.md` | Add notation reference link; fix bracket-write reference |
| `docs/ai-ref/notation-reference.md` | **New file** — authoritative notation rules |
| `docs/ai-ref/functions/Concat.md` | Syntax placeholder convention; note for literal args |
| `docs/ai-ref/functions/Format.md` | Same |
| `docs/ai-ref/functions/Replace.md` | Same |
| `docs/ai-ref/functions/Fetch.md` | Same (default arg literal quoting) |
| `docs/ai-ref/functions/Substring.md` | Same |
| `docs/ai-ref/functions/ScriptPath.md` | Clarify `@.child` notation |
| `docs/ai-ref/commands/Resolve.md` | Clarify `@.productId` relative-path notation |
| All other functions/commands | Verify consistency; update if needed |

**Files to change (code):**

| File | Change |
|------|--------|
| `TLio.Client/FunctionConverter.cs` | Add `@` without dot validation (LogWarning or descriptive error) |

**New test fixtures:**

| Scenario | Project | Path |
|----------|---------|------|
| `@.field` relative path — valid | `TLio.UnitTests` | `Fixtures/Notation/@dotpath-valid/` |
| `@@` escape at value level | `TLio.UnitTests` | `Fixtures/Notation/escape-at-value/` |
| `'...'` literal arg with `@@` inside | `TLio.Functions.Tests` | `Fixtures/Notation/escape-in-quoted-arg/` |
| Outer-quoted function (should be literal) | `TLio.UnitTests` | `Fixtures/Notation/outer-quoted-function/` |

---

## 4. Alternatives Considered

| Option | Rejected Because |
|--------|-----------------|
| Rewrite the parser to require `@.` everywhere | Breaking change for any existing scripts using `@` without dot in filters — not needed, scope is documentation + validation warning |
| Create a formal BNF grammar document | Adds maintenance burden without proportionate user benefit; a readable notation reference with examples is more useful for the target audience |
| Rename `@.` to a different notation | Would break all existing scripts; not warranted |
