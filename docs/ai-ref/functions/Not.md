# not

> Negates a condition.

## Syntax

```
=not(<condition>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | boolean, "true"/"false" text, or path | yes | The condition to negate. |

## Returns

A boolean node.

## Example

```json
{ "command": "ifElse",
  "condition": "=not(in($.status, 'cancelled', 'refunded'))",
  "ifScript": [{ "command": "add", "path": "$.billable", "value": true }] }
```

## When to use

- Inverting a predicate that has no negative twin (`in`, `matches`, `exists`, `isArray`, …).
- Reading more naturally than an inverted comparison in a compound condition.

## When NOT to use

- Simple inequality — `notEquals` says it directly.
- Double negatives — `=not(not(x))` is just `x`.

## Common mistakes

- **Negating a missing path**: `=not($.missing)` is true, because a missing value is not true. That may not be the intent — use `=not(exists($.missing))` to talk about presence.
- **Negating a non-boolean**: `=not($.count)` is true for any number, since numbers are never truthy. Wrap in a predicate first.
