# Quickstart: Unified Script Notation

**Feature**: `010-unify-script-notation`

## TLio Notation in 60 Seconds

A TLio script is always a **valid JSON array**:

```json
[
  { "command": "set", "path": "$.name", "value": "Alice" },
  { "command": "add", "path": "$.full", "value": "=concat($.first, ' ', $.last)" }
]
```

### The Three Value Kinds

| Kind | Form | Example |
|------|------|---------|
| Function expression | Starts with `=` | `"=concat($.a, '-', $.b)"` |
| Path reference | Starts with `$` or `@.` | `"$.name"`, `"@.child"` |
| Literal | Anything else | `"Alice"`, `42`, `true`, `null` |

### Quoting Rules (the key insight)

The outer JSON string uses `"..."`. Inside a function expression, literal arguments use `'...'`:

```json
{ "value": "=concat($.first, ' ', $.last)" }
```

- `$.first` and `$.last` — path arguments → **no quotes**
- `' '` — a literal space → **single quotes required**
- The function expression itself (`=concat(...)`) → **no outer single quotes**

**Wrong** — outer-quoting a function call:
```json
{ "value": "'=concat($.a, $.b)'" }   ← treated as a literal string, NOT a function
```

### Relative Paths (`@.`)

To reference a property on the current node, use `@.propertyName` (dot always required):

```json
{ "command": "set", "path": "$.items[*].full", "value": "=concat(@.first, ' ', @.last)" }
```

`@propertyName` (without the dot) is not valid — the parser will emit a warning.

### Escaping Special Characters

Inside `'...'` literal arguments (or at the value level), double the trigger character:

| Want | Write | Result |
|------|-------|--------|
| `@` | `@@` | `@admin` → `'@@admin'` or `"@@admin"` |
| `$` | `$$` | `$ref` → `'$$ref'` or `"$$ref"` |
| `=` | `==` | `=x+1` → `'==x+1'` or `"==x+1"` |

Example — literal email in a concat:
```json
{ "value": "=concat('user@@example.com', '@', $.domain)" }
```

### The Three Roles of `@`

| Symbol | Role | Context |
|--------|------|---------|
| `@.field` | Relative path to child | JSON/YAML scripts |
| `@@` | Escape for literal `@` | All contexts (value level or inside `'...'`) |
| `@attr` | XPath attribute selector | XML scripts with XPath — inside `[...]` predicates only |

### See Also

- `docs/ai-ref/notation-reference.md` — full notation rules
- `docs/ai-ref/overview.md` — adapter selection, escape sequences, path escaping
