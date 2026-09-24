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
| 2 | integer | no | Number of decimal places (default 0). Clamped to `[0, 15]` — a negative value is treated as `0`, and anything above `15` is treated as `15`; it is never rejected. |

## Returns

A numeric node rounded using midpoint-away-from-zero rounding (`MidpointRounding.AwayFromZero`).

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=round($.half)" }
```

Input: `{ "half": 4.5, "frac": 4.567, "decimals": 2 }`
Output: adds `"result": 5` (the midpoint rounds away from zero, to `5`, not `4`).

Verified by: `TLio.Functions.Tests/Fixtures/Math/round/01-to-integer.json`

The two-argument form controls decimal places — here read from a path rather than written
as a literal:

```json
{ "command": "put", "path": "$.result", "value": "=round($.frac, $.decimals)" }
```

Input: `{ "half": 4.5, "frac": 4.567, "decimals": 2 }`
Output: adds `"result": 4.57`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/round/02-with-decimals.json`

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
