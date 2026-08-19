# TLio Notation Reference

> Authoritative rules for writing TLio script expressions.
> For adapter selection and path dialect details see [overview.md](overview.md).

## 1. JSON-First Rule

A TLio script is always a **valid JSON array** of command objects. Every command property
(`value`, `condition`, `defaultValue`, …) is a JSON value. All expression strings live
inside JSON string delimiters `"..."`.

```json
[
  { "command": "set",    "path": "$.name",  "value": "Alice" },
  { "command": "add",    "path": "$.full",  "value": "=concat($.first, ' ', $.last)" },
  { "command": "remove", "path": "$.temp" }
]
```

## 2. Value Types

| Type | Trigger | Example |
|------|---------|---------|
| **FunctionValue** | Starts with `=` | `"=concat($.a, $.b)"` |
| **PathValue** (absolute) | Starts with `$` | `"$.name"` |
| **PathValue** (relative) | Starts with `@.` | `"@.child"` |
| **FixedValue** (escape) | `@@` / `$$` / `==` prefix | `"@@admin"` → `@admin` |
| **FixedValue** (quoted) | Wrapped in `'...'` | `"'hello'"` → `hello` |
| **FixedValue** (JSON) | JSON boolean / number / null — **unquoted** | `true`, `42`, `null` |
| **FixedValue** (plain) | Anything else | `"Alice"` |

### Strings stay strings

At value level a JSON **string** is never re-interpreted as another JSON type. Write the
JSON literal itself when you want a number, a boolean or null:

```json
{ "value": "007" }   → the string "007"      { "value": 7 }     → the number 7
{ "value": "1e3" }   → the string "1e3"      { "value": 14.5 }  → the number 14.5
{ "value": "true" }  → the string "true"     { "value": true }  → the boolean true
{ "value": "null" }  → the string "null"     { "value": null }  → null
```

Integral literals stay integral: `"value": 152` writes `152`, not `152.0`.

Inside a function's argument list there is no JSON layer to carry the type, so bare
literals *are* typed there: `=substring($.a, 0, 3)` passes the numbers 0 and 3.

### Decimal separator: `.` only

**TLio reads `.` as the decimal separator, and only `.`.** There is no locale setting, and
there is nowhere in a document to declare one.

This is not a TLio choice — it is what the formats mandate:

| Format | Rule |
|---|---|
| JSON | RFC 8259 §6: `decimal-point = %x2E` — the period. JSON has **no locale mechanism** at all. |
| XML | XML Schema `xs:decimal` / `xs:double` use the period likewise. |
| YAML 1.2 | The core schema's number production is JSON-compatible. |

So a *native* number can never carry a comma. `{"n": 3,5}` is not one number written the
European way — it is a syntax error, and the document never parses:

```json
{ "value": 3.5 }    ✅ the number 3.5
{ "value": 3,5 }    ❌ invalid JSON — this is not "3,5", it is two things
```

The question only arises for a number carried **as a string**, which is valid JSON and
therefore does reach TLio:

```json
{ "n": "3.5" }  → 3.5 ✅
{ "n": "3,5" }  → 35  ⚠️  the comma is read as a THOUSANDS separator, not a decimal point
{ "n": "1,234" } → 1234
```

`"3,5"` becoming `35` is a tenfold error with no warning, and `=calculate('2,5+3,7')` rejects
the same notation outright — the two disagree. See
[behaviour-decisions.md](../behaviour-decisions.md#a1-35-becomes-35-in-every-math-function).

**Practical rule:** if numbers reach you as strings from a comma-decimal source, normalise them
before the arithmetic (`=replace($.n, ',', '.')`) rather than relying on the coercion.

## 3. Function Expressions

Function expressions start with `=`. The `=` prefix is the only marker required —
**do not wrap the expression in outer single quotes** unless you want the inner string to be
evaluated as a nested function expression (see Section 4a).

```json
{ "value": "=fetch($.source)" }          ✅ correct — = marks the function
{ "value": "=fetch('$.source')" }        ✅ also correct — quoted path, same result
{ "value": "'=fetch($.source)'" }        ⚠️ still evaluated — quotes do NOT make it literal
{ "value": "'==fetch($.source)'" }       ✅ literal text "=fetch($.source)"
```

> Quoting protects an argument (`'fetch($.a)'` is text), but a quoted string whose content
> begins with `=` is an expression everywhere — that is the rule that makes dynamic paths
> work (§4a). Double the `=` when you want the text.

Syntax: `=functionName(<arg1>, <arg2>, ...)`

Arguments are comma-separated; each argument is a path reference, a single-quoted literal,
or a nested function call.

### 3a. Inner (nested) calls — the `=` is optional

Only the **outermost** expression must start with `=`. Inside an argument list, a call may be
written with or without the prefix — both forms parse to the same thing:

```json
{ "value": "=concat(fetch($.first), ' ', fetch($.last))" }    ✅ inner = omitted
{ "value": "=concat(=fetch($.first), ' ', =fetch($.last))" }  ✅ inner = present — identical
{ "value": "=concat(toUpper(fetch($.first)), '!')" }          ✅ nests to any depth
```

An unquoted argument is read as a nested call when it is a single balanced
`identifier(...)` expression **and** the identifier is a registered function. Everything
else keeps its literal meaning:

| Argument | Read as |
|----------|---------|
| `fetch($.a)` | Call to `fetch` (assuming it is registered) |
| `'fetch($.a)'` | Literal text `fetch($.a)` — quoting always wins |
| `notRegistered($.a)` | Literal text — plus a parser warning about the unknown name |
| `a(1) b(2)` | Literal text — not a single balanced call |
| `newGuid` | Literal text — a bare name without `()` is never a call inside an argument |

> Because `fetch(...)` inside a function resolves the path itself, most nesting is
> unnecessary: `=concat($.first, ' ', $.last)` is equivalent to
> `=concat(fetch($.first), ' ', fetch($.last))` and is the preferred form.

## 4. Quoting Rules

The outer JSON string uses `"..."`. Inside a function expression, **literal string arguments
use single quotes `'...'`** because the outer delimiter is already `"`. Path arguments
(`$.f`, `@.f`) never need quotes.

```json
{ "value": "=concat($.first, ' ', $.last)" }
```

`$.first`, `$.last` → path arguments — **no quotes**.  
`' '` → literal space — **single quotes required**.

| Value kind | Form | Quotes? |
|-----------|------|---------|
| Function expression | `"=name(...)"` | None (outer) |
| Literal string in function arg | `'text'` | Single quotes |
| Path in function arg | `$.f` or `@.f` | None |
| Plain string at value level | `"Alice"` | None (JSON string) |
| Number / boolean / null | `42`, `true`, `null` | None (JSON literal) |

## 4a. Quoted-string function expressions

A quoted string whose inner content begins with `=` (but not `==`) is evaluated as a
nested function expression. Its return value is used directly (or as a path if the result
starts with `$` or `@`).

```json
{ "value": "=fetch('$.source')" }                  ✅ quoted path — same as =fetch($.source)
{ "value": "=fetch('=indirect($.pathRef)')" }       ✅ dynamic path via nested function
{ "value": "=fetch('=concat($.a, $.b)')" }          ✅ path built from field values
```

To produce a literal `=something` string inside quotes, double the `=`:

```json
{ "command": "put", "path": "$.label", "value": "'==concat($.a, $.b)'" }
```

→ stores the literal text `=concat($.a, $.b)` (not evaluated).

### Doubled-quote escape (`''`)

To embed a literal single-quote character inside a `'...'` argument, double it:

```
=fetch('=concat(''$.'', $.fieldname, ''.price'')')
```

After `''` → `'` unescape, the inner expression is `concat('$.', $.fieldname, '.price')`,
which builds a path string at runtime. This also prevents commas inside the doubled-quoted
content from being interpreted as argument separators.

## 4b. Function Names

Function names are matched case-insensitively: `=toUpper(...)`, `=toupper(...)` and
`=TOUPPER(...)` all resolve to the same function. Packs therefore register each name once —
there are no separate camelCase aliases.

## 5. Path References

| Form | Meaning | Valid in |
|------|---------|---------|
| `$.field` | Absolute path from root | All adapters |
| `@.field` | Child of current node | JSON, YAML |
| `@.<--` | Parent of current node | JSON, YAML |
| `@.<--.sibling` | Sibling via parent | JSON, YAML |

> **Rule**: Relative paths in JSON/YAML always use `@.` (with the dot).
> `@field` (no dot) is invalid in JSON/YAML context and triggers a parser warning —
> at value level and inside function arguments alike.

A path that matches nothing produces no value and logs a warning naming the path, so a
typo shows up in the execution log instead of silently writing nothing.

```json
{ "command": "set", "path": "$.items[*].full", "value": "=concat(@.first, ' ', @.last)" }
```

## 6. Escape Sequences

Double the trigger character to produce a literal string instead of triggering path or
function parsing. Works **identically** at the value level and inside `'...'` quoted arguments.

| Escape | Context | Result | Example |
|--------|---------|--------|---------|
| `@@` | Value level | Literal `@` | `"@@admin"` → `@admin` |
| `@@` | Inside `'...'` | Literal `@` | `'user@@example.com'` → `user@example.com` |
| `$$` | Value level | Literal `$` | `"$$ref"` → `$ref` |
| `$$` | Inside `'...'` | Literal `$` | `'$$var'` → `$var` |
| `==` | Value level | Literal `=` | `"==expr"` → `=expr` |
| `==` | Inside `'...'` | Literal `=` | `'==x+1'` → `=x+1` |
| `''` | Inside `'...'` | Literal `'` | `'it''s'` → `it's` |
| `""` | Inside `"..."` args | Literal `"` | inner `""` → one `"` |

The `''` / `""` doubled-quote escape is especially useful inside function expressions
nested within a quoted argument — it prevents the inner quote from ending the outer
quoted string prematurely.

## 7. The Three Roles of `@`

| Role | Symbol | Context |
|------|--------|---------|
| Relative path (child) | `@.fieldName` | JSON/YAML — value or path |
| Escape sequence | `@@` | All adapters — produces literal `@` |
| XML attribute selector | `@attrName` (no dot) | XML XPath — inside `[...]` predicates only |

## 8. Bracket Notation

Use bracket-quoted notation when a property name contains the path delimiter (`.`) or other
special characters.

| Adapter | Normal path | Bracket-notation path |
|---------|------------|----------------------|
| JSON (any) | `$.version` | `$['version.major']` |
| YAML | `$.server` | `$['server.host']` |
| XML XPath | n/a | Use XPath predicates: `item[@id='1']` |

Reading bracket-notation paths is fully supported. Writing via `set`/`add` is tracked on
branch `fix/bracket-write`.

## 9. XPath Special Cases (XML only)

In XML scripts using the native XPath adapter, `@attrName` (no dot) selects an XML
attribute **inside a predicate bracket**:

```json
{ "command": "set", "path": "item[@id='1']/name", "value": "Alice" }
```

`@id` here is XPath attribute syntax — it is **not** a relative-path reference and is only
valid inside `[...]`. See [adapters/xml-xpath.md](adapters/xml-xpath.md).
