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

## Verified example

Input (subset of the shared test document):

```json
{ "age": 37 }
```

Script:

```json
[{ "command": "add", "path": "$.out", "value": "=greaterOrEqual($.age, 37)" }]
```

Result: `out` is `true` — the boundary value itself passes.

Verified by: `PredicateFunctionTests.Ordering("=greaterOrEqual($.age, 37)", true)` —
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:75`. A full `ifElse`
built on this same family of predicates is asserted end-to-end in
`PredicateFunctionTests.Predicate_DrivesIfElseBranch` (condition
`=and(greaterOrEqual($.age, 18), in($.status, $.allowed))`, lines 151-165).

## When to use

- Inclusive thresholds — "18 and over" is `greaterOrEqual($.age, 18)`, not `greaterThan($.age, 18)`.
- Lower bound of a range, paired with `lessOrEqual`.

## When NOT to use

- Exclusive thresholds — use `greaterThan`.
- Set membership — use `in`.

## Common mistakes

- **Off-by-one on boundaries**: the boundary value itself passes. Check whether your rule is "over 18" or "18 and over".
- **Missing operands answer false** — see [greaterThan](GreaterThan.md).
