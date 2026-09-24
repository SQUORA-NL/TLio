# in

> Returns true when a value equals any of the given options.

## Syntax

```
=in(<value>, <option1>, <option2>, ...)
=in(<value>, <arrayPath>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to look for. |
| 2..n | any, path, or array path | yes (at least one) | Options. An argument that resolves to an array contributes all its elements. |

## Returns

A boolean node. A missing value is `false`.

## Verified example

```json
{ "command": "add", "path": "$.out", "value": "=in($.status, $.allowed)" }
```

Input: `{ "status": "gold", "allowed": ["gold", "silver"] }` → Output: `{ ..., "out": true }`

Verified by `PredicateFunctionTests.MembershipAndPattern("=in($.status, $.allowed)", true)` in
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:139` — run end to end
through the real engine, so the notation is verified along with the logic. The same test method
also covers a literal option list (`=in($.status, 'gold', 'silver')` → `true`,
`=in($.status, 'bronze', 'silver')` → `false`), numeric options
(`=in($.age, 36, 37, 38)` → `true`), and a missing value path
(`=in($.missing, 'gold')` → `false`), all in
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:137-141`.

As an `ifElse` condition — the case it was added for:

```json
{ "command": "ifElse",
  "condition": "=in($.status, 'gold', 'silver')",
  "ifScript": [{ "command": "add", "path": "$.tier", "value": "eligible" }],
  "elseScript": [{ "command": "add", "path": "$.tier", "value": "rejected" }] }
```

Verified by `PredicateFunctionTests.Predicate_DrivesIfElseBranch`, which combines `in` with
`greaterOrEqual` under `and` in one condition
(`=and(greaterOrEqual($.age, 18), in($.status, $.allowed))`).

## When to use

- Testing one field against a set of allowed values — shorter and clearer than chained `or(equals(...))`.
- Validating against a whitelist that lives in the document or in configuration.

## When NOT to use

- Testing whether an array *contains* a value where the array is the subject — `in` takes the value first, options second.
- Substring matching — use `contains` from the Text pack.

## Common mistakes

- **Argument order**: `=in($.status, $.allowed)` — value first, options second. Reversing it silently tests the wrong thing.
- **Equality rules apply**: numbers and numeric text match each other; text matching is case-sensitive. See [equals](Equals.md).
