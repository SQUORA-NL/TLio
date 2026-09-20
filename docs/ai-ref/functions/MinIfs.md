# minifs

> The smallest value in `min_range` among rows where **every** criteria pair matches.

## Syntax

```
=minifs(<min_range>, <criteria_range1>, <criteria1> [, <criteria_range2>, <criteria2>, ...])
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the numeric array to take the minimum of. |
| 2, 4, 6… | array path | yes | A criteria range, parallel to `min_range`. |
| 3, 5, 7… | string literal | yes | The criteria for its preceding range (single-quoted). |

Requires an odd number of arguments, at least 3.

## Returns

A numeric node — the smallest matching value.

**Fails when nothing matches — it does not return `0` or `null`.** This is the opposite
convention from `sumifs`/`averageifs`.

## Example

```json
{
  "command": "set", "path": "$.min_paid_amount",
  "value": "=minifs($.amounts,$.status,'paid')"
}
```

Input: `{ "status": ["paid","pending","paid"], "amounts": [100, 200, 150] }`
Output: `"min_paid_amount": 100`

## Criteria syntax

Same operator-prefix and wildcard rules as `sumifs` — see
[SumIfs.md — Criteria syntax](SumIfs.md#criteria-syntax).

## When to use

- The **smallest value among filtered rows** — cheapest matching quote, earliest matching
  numeric period, lowest score in a segment.
- You want a **hard failure** (not a silent `0`) when the filter matches nothing — `minifs`
  gives you that for free, unlike `sumifs`/`averageifs`.

## When NOT to use

- You need the **largest** value — use `maxifs`.
- You need a **total or mean** — use `sumifs`/`averageifs`, which return `0` on no match
  instead of failing.
- You need the **unconditional** minimum — use `min`.
- A no-match should be a **`0`**, not a script failure — guard with `countifs` first, or use
  `ifElse` to branch before calling `minifs`.

## Comparison

| Function | Aggregate | Nothing matches |
|----------|-----------|------------------|
| `minifs` | minimum | **fails** |
| `maxifs` | maximum | **fails** |
| `sumifs` | total | `0` |
| `averageifs` | mean | `0` |
| `min` | minimum (unconditional) | fails on empty input |

## Common mistakes

- **No-match is a failure, not a `0`.** Code written against `sumifs`/`averageifs` habits
  will be surprised: a `minifs`/`maxifs` call with a criteria set that matches nothing aborts
  the script rather than writing a zero. Check with `countifs` first if a match is not
  guaranteed.
- **Odd argument count only** — `min_range` plus complete pairs.
- **All criteria must match (AND), never OR.**
- **Non-numeric matched values are skipped**, not counted as a failure by themselves — but if
  every matched value turns out non-numeric, the result is the same as "nothing matched":
  a failure.
- **Wrong path scope** — every range path resolves against the document root, not the current
  node.
