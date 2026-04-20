# Research: Special Character Escaping in Value and Path Parsing

**Feature**: 009-escape-special-chars | **Date**: 2026-04-19

---

## Decision 1: Escape convention for value trigger characters (`@`, `$`, `=`)

**Decision**: Double-prefix convention — `@@` → literal `@`, `$$` → literal `$`, `==` → literal `=`.

**Rationale**: JLio already uses `@@` inside quoted strings to represent a literal `@`. Using the same double-prefix pattern for all three triggers (`$$`, `==`) is consistent and easy to document: "double the trigger character to escape it". The convention works for both unquoted and quoted values using a single rule.

**Alternatives considered**:
- Backslash escaping (`\@`, `\$`, `\=`): Would conflict with JSON string escaping (`\\` required in JSON source); the double-prefix is simpler in JSON script context.
- Wrapper function (`=literal('@foo')`): Adds a new function name just for escaping; heavier syntax.
- Quote-only escaping (force users to always quote): Already works for `$` and `=` (e.g., `'=foo'` works today), but `@` inside an unquoted context has no workaround. A general escape is cleaner.

---

## Decision 2: Where to apply escape decoding — unquoted prefix vs. inside quotes

**Decision**: Escape sequences apply at position 0 for **unquoted** values (`@@foo` → `@foo`, `$$x` → `$x`, `==y` → `=y`) AND anywhere inside **quoted** strings (`'hello @@world'` → `hello @world`).

**Rationale**: The unquoted rule is needed because `@`, `$`, and `=` are trigger characters checked before the quoted-string branch. The quoted-string rule handles inline occurrences of trigger characters within a literal value. Both rules use the same `@@`/`$$`/`==` sequences, so users only need to remember one convention.

**Alternatives considered**:
- Apply only inside quotes: Users would still have no way to write an unquoted `@@admin` value. Rejected.
- Apply only at position 0: Users couldn't include a literal `@` anywhere in a quoted string. Rejected.

---

## Decision 3: Behaviour of `@@` inside function argument paths

**Decision**: Inside `ParseFunctionExpression`, path-style args (`$...` and `@...`) are passed as string literals for the function to evaluate. The same escape rules apply: `@@foo` as a function arg becomes the string `@foo` (a `FixedValue`), not a path. `@foo` (single `@`) remains a path string passed through.

**Rationale**: Consistency — the same mental model applies at all levels of the expression parser.

---

## Decision 4: Bracket notation for path property names with special characters

**Decision**: Document and test, not implement — the underlying libraries already support it.

- **JSONPath (Newtonsoft)**: `$['a.b']` bracket notation is natively supported by `JToken.SelectTokens`. No code change needed.
- **JSONPath (SystemText/RFC 9535)**: `$['a.b']` is part of RFC 9535 and supported by `JsonPath.Net`. No code change needed.
- **XPath (`NativeXPathItemsFetcher`)**: Standard XPath 1.0 already handles attribute `@` and `=` in predicates. Property names with `/` are not valid XML element names; slash-path segments with special chars use URL-encoding convention already in JLio. Document existing behaviour.
- **YAML dot-notation (`YamlPathItemsFetcher`)**: The fetcher splits on `.`. A property name containing `.` cannot be expressed with current dot-notation. **Action required**: add bracket-notation segment parsing (`$['a.b']` style) to `YamlPathItemsFetcher`.

**Rationale**: Re-implementing JSONPath bracket notation would duplicate what the underlying libraries already do. The YAML fetcher is the only place where a code change is needed.

---

## Decision 5: Triple-prefix semantics (`@@@foo`)

**Decision**: `@@@foo` → `FixedValue("@@foo")` — the first `@@` is consumed as the escape sequence, the remaining `@foo` is the literal content. This naturally falls out of "consume first two characters as escape, take remainder as content".

**Rationale**: Unambiguous mechanical rule; no special casing needed.

---

## Decision 6: Middle-of-value `@@` in unquoted strings

**Decision**: `@@` is only significant at position 0 of an unquoted value. `hello@@world` is not an escape — it is treated as a plain string literal `hello@@world`. Inside a quoted string, `@@` anywhere is replaced with `@`.

**Rationale**: Mirrors the existing WIP logic (`.Replace("@@", "@")` inside quotes); keeping unquoted escaping to position 0 avoids breaking bare strings that happen to contain `@@`.
