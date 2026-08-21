# sign

> Returns -1, 0 or 1 according to whether a numeric value is negative, zero or positive.

## Syntax

```
=sign(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value whose sign is wanted. |

## Returns

A whole-number node: `-1`, `0` or `1`. Always an integer, never `-1.0`.

## Example

```json
{ "command": "put", "path": "$.direction",
  "value": "=sign(=subtract($.renewal,$.quoted))" }
```

Input: `{ "quoted": 412.5, "renewal": 398.2 }`
Output: `{ "quoted": 412.5, "renewal": 398.2, "direction": -1 }`

## When to use

- **Recording the direction of a change** — did the renewal premium go up, down, or stay flat — as one field instead of a branch per outcome.
- **Reapplying a sign after taking a magnitude** — `=multiply(=abs($.v),$.storedSign)`.
- **Collapsing a comparison to a sortable, storable number** where a boolean cannot express the three-way answer.
- **Feeding a `decisionTable`** keyed on `-1` / `0` / `1`.

## When NOT to use

- You want the **magnitude** without the sign — use `abs`.
- You want a **boolean** "is this negative" — use `=lessThan($.v,0)`; a predicate composes with `and` / `or` / `not`, a `-1` does not.
- You want **different values than -1/0/1** — `sign` has no mapping argument. Pair it with `decisionTable`, or branch with `ifElse`.
- The value is a **date or a string** — `sign` is numeric; a non-numeric string fails the function.

## Comparison

| Function | Returns | `-7` → | `0` → | `7` → |
|----------|---------|--------|-------|-------|
| `sign` | -1, 0 or 1 (integer) | `-1` | `0` | `1` |
| `abs` | magnitude, sign discarded | `7` | `0` | `7` |
| `lessThan(v, 0)` | boolean | `true` | `false` | `false` |
| `=multiply(=abs(v),=sign(v))` | the original value | `-7` | `0` | `7` |

## Common mistakes

- **Zero is `0`, not `1` and not a failure.** `sign` is three-valued. Code that assumes only `-1` or `1` — "positive or negative" — will mis-handle an exact zero, which is exactly the case that turns up when two amounts are equal.
- **`null` gives `0`, not a failure.** Found-but-null is 0 throughout the Math pack, so `=sign($.nullField)` is `0` and indistinguishable from a real zero. A path that resolves to nothing fails instead — the two nulls are different states.
- **A tiny negative is still `-1`.** `sign(-0.001)` is `-1`. If you meant "negative beyond a tolerance", compare against that tolerance instead.
- **The result is an integer, not a boolean.** `=not(=sign($.v))` does not do anything sensible; feed a predicate to `not`.
- **Wrong path scope** — the path argument resolves against the document root, not the current node.
