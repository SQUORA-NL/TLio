# and

> Returns true when every argument is true. Stops at the first false argument.

## Syntax

```
=and(<condition1>, <condition2>, ...)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1..n | boolean, "true"/"false" text, or path | yes (at least one) | Conditions to combine. |

## Returns

A boolean node.

## Truthiness

Only a boolean value, or the text `"true"` / `"false"` (case-insensitive), counts. Numbers,
non-empty strings and objects are **not** truthy — `=and($.count)` is false even when count is 5.
Use an explicit predicate: `=and(greaterThan($.count, 0))`.

## Verified example

Input (subset of the shared test document):

```json
{ "age": 37, "status": "gold", "allowed": ["gold", "silver"] }
```

Script:

```json
[{ "command": "ifElse",
   "condition": "=and(greaterOrEqual($.age, 18), in($.status, $.allowed))",
   "ifScript":   [{ "command": "add", "path": "$.tier", "value": "eligible" }],
   "elseScript": [{ "command": "add", "path": "$.tier", "value": "rejected" }] }]
```

Result: `tier` is `"eligible"` — both arguments are true, `$.age` is 37 (>= 18) and `$.status`
(`"gold"`) is a member of `$.allowed`.

Nested calls do not need their own `=`: `=and(equals($.a, 1), exists($.b))` is the same as
`=and(=equals($.a, 1), =exists($.b))` — confirmed by
`PredicateFunctionTests.BooleanLogic("=and(equals($.name, 'Sanne'), greaterThan($.age, 30))", true)`
alongside the explicit-`=` form on the preceding line (both true).

Verified by: `PredicateFunctionTests.Predicate_DrivesIfElseBranch` —
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:151-165` (the same
condition also drives the `elseScript` branch to `"rejected"` in
`Predicate_DrivesIfElseElseBranch`, lines 167-181). Truthiness cases (`$.active` boolean `true`,
`$.activeText` text `"true"`, `$.age` a number that is *not* truthy) are asserted at lines 95-96,
and cross-format use appears in `TLio.Parity.Tests/Sweep/sweep.json:380`.

## When to use

- Combining several conditions for one `ifElse` branch.
- Range checks: `=and(greaterOrEqual($.n, 10), lessOrEqual($.n, 20))`.

## When NOT to use

- Any-of logic — use `or`.
- Set membership — `=in($.status, 'a', 'b')` is clearer than a chain of `or(equals(...))`.

## Common mistakes

- **Passing a non-boolean**: a value like `5` or `"yes"` is not truthy. Wrap it in a predicate.
- **Missing paths are false**, so `=and($.flagThatDoesNotExist)` is false rather than an error.
- **Expecting short-circuit side effects**: evaluation stops early, but functions have no side effects, so this only affects speed.
