# greaterOrEqual

> Returns true when the first value is greater than or equal to the second.

## Syntax

```
=greaterOrEqual(<left>, <right>)
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
  "condition": "=greaterOrEqual($.age, 18)",
  "ifScript": [{ "command": "add", "path": "$.adult", "value": true }] }
```

## When to use

- Inclusive thresholds — "18 and over" is `greaterOrEqual($.age, 18)`, not `greaterThan($.age, 18)`.
- Lower bound of a range, paired with `lessOrEqual`.

## When NOT to use

- Exclusive thresholds — use `greaterThan`.
- Set membership — use `in`.

## Common mistakes

- **Off-by-one on boundaries**: the boundary value itself passes. Check whether your rule is "over 18" or "18 and over".
- **Missing operands answer false** — see [greaterThan](GreaterThan.md).
