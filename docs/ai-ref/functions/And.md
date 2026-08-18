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

## Example

```json
{ "command": "ifElse",
  "condition": "=and(greaterOrEqual($.age, 18), equals($.country, 'NL'), not(isNull($.email)))",
  "ifScript": [{ "command": "add", "path": "$.eligible", "value": true }] }
```

Nested calls do not need their own `=`: `=and(equals($.a, 1), exists($.b))` is the same as
`=and(=equals($.a, 1), =exists($.b))`.

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
