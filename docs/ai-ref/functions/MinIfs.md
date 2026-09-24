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
| 3, 5, 7… | string literal or path | yes | The criteria for its preceding range — single-quoted literal or a path to a string node. |

Requires an odd number of arguments, at least 3.

## Returns

A numeric node — the smallest matching value.

**Fails when nothing matches — it does not return `0` or `null`.** This is the opposite
convention from `sumifs`/`averageifs`.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=minifs($.nums, $.cat, $.crit_A)" }
```

Input: `{ "nums": [1, 2, 3, 4, 5], "cat": ["A", "B", "A", "C", "A"], "crit_A": "A" }`
Output: `..., "result": 1` — the smallest of the three `nums` positions where `cat` is `"A"`
(indices 0, 2, 4 → values `1`, `3`, `5`).

Verified by: `TLio.Functions.Tests/Fixtures/Math/minifs/01-basic.json`.

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

## Performance

`minifs` resolves and flattens every range once, then walks `min_range`'s length testing all
`k` criteria per index — roughly O(n × k) for `n` rows and `k` criteria pairs, the same shape
as `sumifs`/`averageifs`/`maxifs`. Criteria strings are parsed per element rather than compiled
once per call.

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
