# Data Model: Special Character Escaping

**Feature**: 009-escape-special-chars | **Date**: 2026-04-19

---

## Entities

### RawValue (parse-time concept)

A `RawValue` is the string token read from a TLio script JSON property before type resolution. It flows through `FunctionConverter<TNode>.ParseValue`.

**Parsing precedence** (evaluated top to bottom, first match wins):

| Priority | Pattern | Resolved as | Escape override |
|----------|---------|-------------|-----------------|
| 1 | Empty string | `FixedValue("")` | — |
| 2 | Starts with `@@` | `FixedValue(@<remainder>)` | Overrides rule 5 |
| 3 | Starts with `$$` | `FixedValue($<remainder>)` | Overrides rule 6 |
| 4 | Starts with `==` | `FixedValue(=<remainder>)` | Overrides rule 7 |
| 5 | Starts with `=` | `FunctionExpression` | — |
| 6 | Starts with `$` or `@` | `PathValue` | — |
| 7 | Quoted (`'…'` or `"…"`) | `FixedValue(unquoted, @@ → @, $$ → $, == → =)` | — |
| 8 | `true` / `false` | `FixedValue(bool)` | — |
| 9 | Numeric literal | `FixedValue(number)` | — |
| 10 | Anything else | `FixedValue(string as-is)` | — |

---

### EscapeSequence

A two-character token recognized at specific positions during value parsing.

| Sequence | Context | Decodes to |
|----------|---------|-----------|
| `@@` | Position 0 of unquoted value | `@` (rest of string is the literal content) |
| `$$` | Position 0 of unquoted value | `$` |
| `==` | Position 0 of unquoted value | `=` |
| `@@` | Anywhere inside quoted string | `@` |
| `$$` | Anywhere inside quoted string | `$` |
| `==` | Anywhere inside quoted string | `=` |

---

### PathSegment (path-layer concept)

A single navigation step in a path expression. Each `IItemsFetcher` defines how segments are encoded.

| Fetcher | Normal segment | Segment with special chars |
|---------|---------------|--------------------------|
| `JsonPathItemsFetcher` (Newtonsoft) | `$.city` | `$['city.name']` (bracket notation) |
| `SystemTextJsonPathItemsFetcher` | `$.city` | `$['city.name']` (RFC 9535 bracket) |
| `YamlPathItemsFetcher` | `$.city` | `$['city.name']` (new bracket support) |
| `NativeXPathItemsFetcher` | `address/city` | Use XPath predicate or attribute syntax |
| `SlashPathItemsFetcher` | `/address/city` | URL-encode `/` in segment name |

---

## State Transitions

No runtime state is affected — escape decoding is a pure parse-time transformation. The resolved `IFunctionSupportedValue<TNode>` carries the decoded literal; the original script JSON is never rewritten.
