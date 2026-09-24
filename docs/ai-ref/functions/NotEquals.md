# notEquals

> Returns true when two values are not equal — the exact negation of `equals`.

## Syntax

```
=notEquals(<left>, <right>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | Left value. |
| 2 | any or path | yes | Right value. |

## Returns

A boolean node.

## Verified example

Input (subset of the shared test document):

```json
{ "name": "Sanne" }
```

Script:

```json
[{ "command": "add", "path": "$.out", "value": "=notEquals($.name, 'Other')" }]
```

Result: `out` is `true`.

Verified by: `PredicateFunctionTests.Equality("=notEquals($.name, 'Other')", true)` —
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:66`. The same test
class also asserts `=notEquals($.age, 37)` → `false` (line 67, since `$.age` is `37`), and
cross-format use appears in `TLio.Parity.Tests/Sweep/sweep.json:355`.

## When to use

- Guarding a branch that should run for everything except one value.
- Reads better than `=not(equals(...))`, which does the same thing.

## When NOT to use

- Excluding several values — use `=not(in($.field, 'a', 'b'))`.
- Checking presence rather than value — use `exists` or `isNull`.

## Common mistakes

- **Missing values**: a missing path counts as null, so `=notEquals($.missing, null)` is false, not true.
- **Type bridging still applies**: `=notEquals(37, '37')` is false — the values are equal numerically. See [equals](Equals.md).
