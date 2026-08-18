# lessThan

> Returns true when the first value sorts before the second — numerically for numbers, ordinally for text.

## Syntax

```
=lessThan(<left>, <right>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number, string or path | yes | Left value. |
| 2 | number, string or path | yes | Right value. |

## Returns

A boolean node. `false` (with a warning) when either side is missing, null, an object or an array.

## Example

```json
{ "command": "ifElse",
  "condition": "=lessThan($.stock, 5)",
  "ifScript": [{ "command": "add", "path": "$.reorder", "value": true }] }
```

## When to use

- Upper thresholds and low-stock / low-balance style checks.
- Upper bound of a range, paired with `greaterOrEqual`.

## When NOT to use

- Inclusive upper bounds — use `lessOrEqual`.
- Date ordering across formats — use the TimeDate pack.

## Common mistakes

- **Missing operands answer false**, so `=lessThan($.missing, 5)` does not fire. Guard with `exists`.
- **Comparing an array's length**: `=lessThan($.items, 5)` compares the array itself (not orderable). Use `=lessThan(count($.items), 5)` with the Math pack.
