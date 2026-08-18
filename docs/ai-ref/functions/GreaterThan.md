# greaterThan

> Returns true when the first value sorts after the second — numerically for numbers, ordinally for text.

## Syntax

```
=greaterThan(<left>, <right>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number, string or path | yes | Left value. |
| 2 | number, string or path | yes | Right value. |

## Returns

A boolean node. Returns `false` (with a warning) when either side is missing, null, an object or an array — those cannot be ordered.

## Example

```json
{ "command": "ifElse",
  "condition": "=greaterThan($.age, 17)",
  "ifScript":   [{ "command": "add", "path": "$.adult", "value": true }],
  "elseScript": [{ "command": "add", "path": "$.adult", "value": false }] }
```

## When to use

- Numeric thresholds: age limits, amounts, counts.
- Text ordering when you need a strict ordinal comparison (`"b" > "a"`).
- Composed with `=and(...)` for ranges: `=and(greaterOrEqual($.n, 10), lessOrEqual($.n, 20))`.

## When NOT to use

- Comparing dates as text where formats differ — use `dateCompare` / `isDateBetween` from the TimeDate pack.
- Checking equality — use `equals`.

## Common mistakes

- **Numeric text**: `"10"` and `10` both compare numerically, so `=greaterThan('9', 10)` is false — as expected numerically, but surprising if you expected text ordering.
- **Missing operands answer false**: `=greaterThan($.missing, 5)` is false, and so is `=lessOrEqual($.missing, 5)`. Guard with `exists` when the distinction matters.
- **Mixed text and number**: when one side is non-numeric text, both are compared as text.
