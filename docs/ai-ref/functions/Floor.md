# floor

> Returns the largest integer less than or equal to the given value.

## Syntax

```
=floor(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to floor. |

## Returns

A long node equal to `Math.Floor(value)`.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=floor($.down)" }
```

Input: `{ "whole": 4, "down": 4.9, "neg": -4.1 }`
Output: adds `"result": 4`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/floor/02-fractional.json`
(`01-whole-number.json` verifies a whole input passes through unchanged — `floor($.whole)`
→ `4` — and `03-negative.json` verifies the "toward -∞" direction on a negative input —
`floor($.neg)` → `-5`, not `-4`.)

## When to use

- You need to round **down** without exception — quotas, safe capacities, integer division results, resource allocations where over-counting is not acceptable.
- Any fractional part must be discarded: 7.99 → 7.
- You are computing a maximum safe count where rounding up would exceed a limit.

## When NOT to use

- You want the **nearest** integer — use `round`.
- You want to always round **up** — use `ceiling`.
- The value might not exist — `floor` fails if the path does not resolve.

## Comparison

| Function | Direction | 7.1 → | 7.5 → | 7.9 → | -7.5 → |
|----------|-----------|--------|--------|--------|---------|
| `floor` | Always down (toward -∞) | 7 | 7 | 7 | -8 |
| `ceiling` | Always up (toward +∞) | 8 | 8 | 8 | -7 |
| `round` | Nearest (away-from-zero at midpoint) | 7 | 8 | 8 | -8 |

## Common mistakes

- **Using `floor` on a negative number expecting "down" to mean smaller magnitude** — `floor(-7.5)` is `-8` (away from zero), not `-7`. "Down" means toward negative infinity.
- **Using `floor` when you want nearest** — if 7.9 should round to 8, use `round`, not `floor`.
- **Path not found = failure** — if the path does not resolve, the command fails.
- **Wrong path scope** — path arguments resolve against the document root.
