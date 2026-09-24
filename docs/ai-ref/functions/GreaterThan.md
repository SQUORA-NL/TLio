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

## Verified example

Input (subset of the shared test document):

```json
{ "age": 37 }
```

Script:

```json
[{ "command": "add", "path": "$.out", "value": "=greaterThan($.age, 30)" }]
```

Result: `out` is `true`.

Verified by: `PredicateFunctionTests.Ordering("=greaterThan($.age, 30)", true)` —
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:73`. The same test
class also asserts `=greaterThan($.missing, 1)` → `false` and `=greaterThan($.address, 1)` →
`false` (lines 81-82, an object is not orderable), and `=greaterThan($.name, 'A')` → `true`
(ordinal text ordering, line 79).

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
