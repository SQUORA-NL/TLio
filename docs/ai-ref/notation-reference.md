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
| **FixedValue** (JSON) | JSON boolean / number / null | `true`, `42`, `null` |
| **FixedValue** (plain) | Anything else | `"Alice"` |

## 3. Function Expressions

Function expressions start with `=`. The `=` prefix is the only marker required —
**do not wrap the expression in outer single quotes**.

```json
{ "value": "=fetch($.source)" }         ✅ correct — = marks the function
{ "value": "'=fetch($.source)'" }        ❌ wrong  — treated as literal string
```

Syntax: `=functionName(<arg1>, <arg2>, ...)`

Arguments are comma-separated; each argument is a path reference or a single-quoted literal.

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

## 5. Path References

| Form | Meaning | Valid in |
|------|---------|---------|
| `$.field` | Absolute path from root | All adapters |
| `@.field` | Child of current node | JSON, YAML |
| `@.<--` | Parent of current node | JSON, YAML |
| `@.<--.sibling` | Sibling via parent | JSON, YAML |

> **Rule**: Relative paths in JSON/YAML always use `@.` (with the dot).
> `@field` (no dot) is invalid in JSON/YAML context and triggers a parser warning.

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
