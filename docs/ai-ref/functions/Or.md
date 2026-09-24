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

## Verified example

Input (subset of the shared test document):

```json
{ "name": "Sanne", "age": 37 }
```

Script:

```json
[{ "command": "add", "path": "$.out",
   "value": "=or(equals($.name, 'Other'), greaterThan($.age, 30))" }]
```

Result: `out` is `true` — the first argument is false but the second is true, and `or` needs only
one.

Verified by: `PredicateFunctionTests.BooleanLogic("=or(equals($.name, 'Other'), greaterThan($.age, 30))", true)`
— `TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:91`. The same test
class asserts a three-argument form true only via the third condition —
`=or(equals($.a, 1), equals($.b, 2), equals($.age, 37))` → `true` (line 97, the first two
arguments reference missing paths) — and cross-format use appears in
`TLio.Parity.Tests/Sweep/sweep.json:385`.

## When to use

- Any-of conditions where the alternatives are different kinds of check.
- Fallback logic: "either the flag is set or the amount is high enough".

## When NOT to use

- Comparing one field against several constants — `=in($.status, 'gold', 'silver')` is shorter and clearer.
- All-of conditions — use `and`.

## Common mistakes

- **Truthiness rules apply**: see [and](And.md) — numbers and arbitrary strings are not true.
- **Mixing up with `in`**: `=or($.a, $.b)` tests two booleans; it does not test whether one value is among several.
