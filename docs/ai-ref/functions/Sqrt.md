# sqrt

> Returns the square root of a numeric value.

## Syntax

```
=sqrt(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | Non-negative number to take the root of. |

## Returns

A double node equal to `Math.Sqrt(value)`.

## Example

```json
{ "command": "set", "path": "$.sq", "value": "=sqrt($.n)" }
```

Input: `{ "n": 16, "sq": 0 }`
Output: `{ "n": 16, "sq": 4 }`

## When to use

- You need the **square root** of a known non-negative value — distance calculations, standard deviation steps, normalization.
- The input is guaranteed to be zero or positive.

## When NOT to use

- The input might be **negative** — `sqrt` of a negative number produces `NaN`, not a failure. You must guard against negative inputs explicitly.
- You need a general **Nth root** — use `pow($.x, 0.5)` for square root via pow if you need consistency, or implement via `pow` for other roots.
- You need to raise a value to a power — use `pow`.

## Common mistakes

- **Negative input produces NaN, not failure** — `sqrt(-4)` returns `NaN` silently. If the input might be negative, validate it before calling `sqrt`.
- **Path not found = failure** — if the path does not resolve, the command fails (distinct from NaN).
- **Expecting an integer result** — `sqrt(2)` is `1.414...`, a double. If you need an integer, wrap with `round`/`floor`/`ceiling`.
- **Wrong path scope** — path arguments resolve against the document root.
