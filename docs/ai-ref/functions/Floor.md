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

## Example

```json
{ "command": "set", "path": "$.f", "value": "=floor($.v)" }
```

Input: `{ "v": 7.9, "f": 0 }`
Output: `{ "v": 7.9, "f": 7 }`

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
