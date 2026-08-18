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

## Example

```json
{ "command": "ifElse",
  "condition": "=notEquals($.country, 'NL')",
  "ifScript": [{ "command": "add", "path": "$.needsVatCheck", "value": true }] }
```

## When to use

- Guarding a branch that should run for everything except one value.
- Reads better than `=not(equals(...))`, which does the same thing.

## When NOT to use

- Excluding several values — use `=not(in($.field, 'a', 'b'))`.
- Checking presence rather than value — use `exists` or `isNull`.

## Common mistakes

- **Missing values**: a missing path counts as null, so `=notEquals($.missing, null)` is false, not true.
- **Type bridging still applies**: `=notEquals(37, '37')` is false — the values are equal numerically. See [equals](Equals.md).
