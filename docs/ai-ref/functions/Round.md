# round

> Rounds a numeric value to the nearest integer (or specified decimal places).

## Syntax

```
=round(<value>)
=round(<value>, <decimals>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to round. |
| 2 | integer | no | Number of decimal places (default 0). |

## Returns

A numeric node rounded using midpoint-away-from-zero rounding.

## Example

```json
{ "command": "set", "path": "$.r", "value": "=round($.v)" }
```

Input: `{ "v": 7.6, "r": 0 }`
Output: `{ "v": 7.6, "r": 8 }`

## When to use

- You want the **nearest** integer or decimal — displaying a price to 2 decimal places, rounding a calculated score.
- Midpoint rounding (0.5) should go **away from zero**: 2.5 → 3, -2.5 → -3.
- You need a controlled number of decimal places: `=round($.price, 2)`.

## When NOT to use

- You always need to round **up** regardless of the fractional part — use `ceiling`.
- You always need to round **down** regardless of the fractional part — use `floor`.
- The value you are rounding might not exist — `round` fails if the path does not resolve.

## Comparison

| Function | Direction | 7.1 → | 7.5 → | 7.9 → | -7.5 → |
|----------|-----------|--------|--------|--------|---------|
| `round` | Nearest (away-from-zero at midpoint) | 7 | 8 | 8 | -8 |
| `ceiling` | Always up (toward +∞) | 8 | 8 | 8 | -7 |
| `floor` | Always down (toward -∞) | 7 | 7 | 7 | -8 |

## Common mistakes

- **Using `round` when you need deterministic direction** — if the value is exactly 7.5 and you need 8 every time, `round` does give 8, but if you need 7 every time, use `floor`. Do not assume `round` always goes in the direction you expect.
- **Forgetting the decimals argument** — `=round($.price)` rounds to the nearest integer. To keep 2 decimal places use `=round($.price, 2)`.
- **Path not found = failure** — if the path does not resolve, the command fails. Ensure the value always exists.
- **Wrong path scope** — the value argument, if a path, resolves against the document root.
