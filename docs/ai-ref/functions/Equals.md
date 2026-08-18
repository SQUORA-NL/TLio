# equals

> Returns true when two values are equal, bridging types so the number 1 equals the text "1".

## Syntax

```
=equals(<left>, <right>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | Left value. |
| 2 | any or path | yes | Right value. |

## Returns

A boolean node.

## Comparison rules

| Case | Result |
|------|--------|
| Both numeric (`37` vs `"37"`) | Compared as numbers → equal |
| Both boolean (`true` vs `"true"`) | Compared as booleans → equal |
| Objects or arrays | Compared structurally (deep equality) |
| One side missing / null | Equal only to another missing or null value |
| Anything else | Ordinal (case-sensitive) text comparison |

## Example

```json
{ "command": "ifElse",
  "condition": "=equals($.status, 'gold')",
  "ifScript":   [{ "command": "add", "path": "$.discount", "value": 0.2 }],
  "elseScript": [{ "command": "add", "path": "$.discount", "value": 0 }] }
```

## When to use

- As an `ifElse` condition, or nested inside `=and(...)` / `=or(...)` / `=not(...)`.
- Comparing a field against a constant, or two fields against each other.
- Checking for null: `=equals($.field, null)` — though `=isNull($.field)` reads better.

## When NOT to use

- You need ordering — use `greaterThan`, `greaterOrEqual`, `lessThan`, `lessOrEqual`.
- You are testing membership in a set — use `in`.
- You want case-insensitive text equality — normalise both sides first: `=equals(toLower($.a), toLower($.b))`.

## Common mistakes

- **Expecting case-insensitive text**: `=equals($.name, 'sanne')` is false for `"Sanne"`.
- **Confusing missing with empty**: a missing path equals null, not `""`. Use `isEmpty` for blank strings.
- **Quoting the literal**: text arguments need single quotes — `'gold'`, not `gold` (which happens to work via the plain-string fallback but is ambiguous).
