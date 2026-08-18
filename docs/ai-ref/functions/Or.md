# or

> Returns true when any argument is true. Stops at the first true argument.

## Syntax

```
=or(<condition1>, <condition2>, ...)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1..n | boolean, "true"/"false" text, or path | yes (at least one) | Conditions to combine. |

## Returns

A boolean node.

## Example

```json
{ "command": "ifElse",
  "condition": "=or(equals($.status, 'gold'), greaterThan($.spend, 1000))",
  "ifScript": [{ "command": "add", "path": "$.priority", "value": true }] }
```

## When to use

- Any-of conditions where the alternatives are different kinds of check.
- Fallback logic: "either the flag is set or the amount is high enough".

## When NOT to use

- Comparing one field against several constants — `=in($.status, 'gold', 'silver')` is shorter and clearer.
- All-of conditions — use `and`.

## Common mistakes

- **Truthiness rules apply**: see [and](And.md) — numbers and arbitrary strings are not true.
- **Mixing up with `in`**: `=or($.a, $.b)` tests two booleans; it does not test whether one value is among several.
