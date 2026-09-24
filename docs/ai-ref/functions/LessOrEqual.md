# lessOrEqual

> Returns true when the first value is less than or equal to the second.

## Syntax

```
=lessOrEqual(<left>, <right>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number, string or path | yes | Left value. |
| 2 | number, string or path | yes | Right value. |

## Returns

A boolean node. `false` (with a warning) when either side is missing, null, an object or an array.

## Verified example

```json
{ "command": "add", "path": "$.out", "value": "=lessOrEqual($.premium, 14.5)" }
```

Input: `{ "premium": 14.5 }` → Output: `{ ..., "out": true }`

Verified by `PredicateFunctionTests.Ordering("=lessOrEqual($.premium, 14.5)", true)` in
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:78` — run end to end
through the real engine.

As an `ifElse` condition, paired with `greaterOrEqual` for a range:

```json
{ "command": "ifElse",
  "condition": "=and(greaterOrEqual($.score, 50), lessOrEqual($.score, 100))",
  "ifScript": [{ "command": "add", "path": "$.grade", "value": "pass" }] }
```

## When to use

- Inclusive upper bounds, especially as the second half of a range check.

## When NOT to use

- Exclusive upper bounds — use `lessThan`.

## Common mistakes

- **Forgetting both bounds**: a range needs two calls joined by `and`; one comparison only bounds one end.
- **Missing operands answer false** — see [greaterThan](GreaterThan.md).
