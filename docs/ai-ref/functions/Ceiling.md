# ceiling

> Returns the smallest integer greater than or equal to the given value.

## Syntax

```
=ceiling(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to ceil. |

## Returns

A long node equal to `Math.Ceiling(value)`.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=ceiling($.up)" }
```

Input: `{ "whole": 4, "up": 4.1, "neg": -4.1 }`
Output: adds `"result": 5`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/ceiling/02-fractional.json`
(`01-whole-number.json` verifies a whole input passes through unchanged — `ceiling($.whole)`
→ `4` — and `03-negative.json` verifies the "toward +∞" direction on a negative input —
`ceiling($.neg)` → `-4`, not `-5`.)

## When to use

- You need to round **up** without exception — page counts (`ceiling(items / pageSize)`), batch counts, required capacity.
- Any fractional part must push the result to the next whole integer, even 0.0001 → 1.
- You are computing a minimum safe allocation where under-counting is not acceptable.

## When NOT to use

- You want the **nearest** integer — use `round`.
- You want to always round **down** — use `floor`.
- The value might not exist — `ceiling` fails if the path does not resolve.

## Comparison

| Function | Direction | 7.1 → | 7.5 → | 7.9 → | -7.5 → |
|----------|-----------|--------|--------|--------|---------|
| `ceiling` | Always up (toward +∞) | 8 | 8 | 8 | -7 |
| `floor` | Always down (toward -∞) | 7 | 7 | 7 | -8 |
| `round` | Nearest (away-from-zero at midpoint) | 7 | 8 | 8 | -8 |

## Common mistakes

- **Using `ceiling` on a negative number expecting "up" to mean larger magnitude** — `ceiling(-7.5)` is `-7` (toward zero), not `-8`. "Up" means toward positive infinity.
- **Using `ceiling` when you want nearest** — if 7.1 should stay 7, use `round`, not `ceiling`.
- **Path not found = failure** — if the path does not resolve, the command fails.
- **Wrong path scope** — path arguments resolve against the document root.
