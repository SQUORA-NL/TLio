# countifs

> Counts rows where **every** criteria pair matches — the multi-condition sibling of `countif`.

## Syntax

```
=countifs(<criteria_range1>, <criteria1> [, <criteria_range2>, <criteria2>, ...])
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1, 3, 5… | array path | yes | A criteria range. All ranges are treated as parallel. |
| 2, 4, 6… | string literal or path | yes | The criteria that its preceding range must satisfy — single-quoted literal or a path to a string node. |

Requires an even number of arguments, at least 2 — one or more
`(criteria_range, criteria)` pairs. Unlike `sumifs`/`averageifs`, there is no separate value
range: `countifs` counts, it does not total anything.

## Returns

A long node with the count of rows where all pairs matched. `0` — not a failure — when
nothing matches.

## Verified example

Single criteria pair (behaves like `countif`):

```json
{ "command": "put", "path": "$.result", "value": "=countifs($.nums, $.crit_gt3)" }
```

Input: `{ "nums": [1, 2, 3, 4, 5], "crit_gt3": ">3", "crit_lte4": "<=4" }`
Output: `..., "result": 2`

Verified by: `TLio.Functions.Tests/Fixtures/Math/countifs/01-single-criteria.json`.

Two criteria pairs over the **same** range, AND-combined:

```json
{ "command": "put", "path": "$.result", "value": "=countifs($.nums, $.crit_gt3, $.nums, $.crit_lte4)" }
```

Same input → `..., "result": 1` — only `4` is both `>3` and `<=4`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/countifs/02-two-criteria.json`.

## Criteria syntax

Same operator-prefix and wildcard rules as `sumifs` — see
[SumIfs.md — Criteria syntax](SumIfs.md#criteria-syntax).

## When to use

- A filtered count that depends on **two or more conditions at once**.
- The ranges involved are **parallel arrays**.

## When NOT to use

- **Only one condition** — `countif` is simpler.
- You need a **total**, not a count — use `sumifs`.
- You need an **unconditional count** — use `count`.
- The ranges are **not parallel** — misaligned arrays compare the wrong pairs.

## Performance

Each criteria/range pair is resolved and flattened once up front, then `countifs` walks the
longest range's length, evaluating all `k` criteria for every index via `List<T>.All` (short-
circuiting on the first non-match) — roughly O(n × k) in the worst case for `n` rows and `k`
criteria pairs. Every criteria string is re-parsed by `ConditionEvaluator` on each element it
is tested against, since there is no per-call caching of the parsed condition. For large arrays
or many criteria pairs evaluated repeatedly, pre-filtering the data before the script runs is
cheaper than re-scanning with `countifs` on every invocation.

## Comparison

| Function | Conditions | Iterates to | Nothing matches |
|----------|-----------|-------------|------------------|
| `countif` | exactly 1 | the single range's length | `0` |
| `countifs` | 1 or more (AND) | the **longest** criteria range's length | `0` |
| `sumifs` | 1 or more (AND) | `sum_range`'s length | `0` |

`countifs` iterating to the longest range (rather than the shortest, or a designated value
range) matters when criteria ranges are different lengths: an index past a shorter range
simply fails that pair's condition rather than truncating the whole comparison.

## Common mistakes

- **Even argument count only.** Fewer than 2 arguments, or an odd total, fails immediately
  with a warning.
- **All criteria must match (AND), never OR.** Run separate calls and combine results for OR
  logic.
- **No value/sum range.** `countifs` never reads a "range to total" — every argument pair is
  a condition. Reach for `sumifs`/`averageifs` when a value needs aggregating.
- **Using double quotes for criteria** — must be single-quoted.
- **Wrong path scope** — every range path resolves against the document root, not the current
  node.
