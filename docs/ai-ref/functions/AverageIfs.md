# averageifs

> Averages `average_range` where **every** criteria pair matches — the multi-condition sibling of `averageif`.

## Syntax

```
=averageifs(<average_range>, <criteria_range1>, <criteria1> [, <criteria_range2>, <criteria2>, ...])
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the numeric array to average. |
| 2, 4, 6… | array path | yes | A criteria range, parallel to `average_range`. |
| 3, 5, 7… | string literal or path | yes | The criteria for its preceding range — single-quoted literal or a path to a string node. |

Requires an odd number of arguments, at least 3.

## Returns

A numeric node — the mean of the matching elements. `0` — not a failure — when nothing
matches.

## Verified example

Single criteria pair:

```json
{ "command": "put", "path": "$.result", "value": "=averageifs($.nums, $.nums, $.crit_gt3)" }
```

Input: `{ "nums": [1, 2, 3, 4, 5], "crit_gt3": ">3", "crit_lte4": "<=4" }`
Output: `..., "result": 4.5` — mean of `4` and `5` (`average_range` and the sole criteria range
are the same array here).

Verified by: `TLio.Functions.Tests/Fixtures/Math/averageifs/01-single-criteria.json`.

Two criteria pairs, AND-combined:

```json
{ "command": "put", "path": "$.result", "value": "=averageifs($.nums, $.nums, $.crit_gt3, $.nums, $.crit_lte4)" }
```

Same input → `..., "result": 4` — only `4` is `>3` **and** `<=4`, so the mean of the matching
subset is `4` itself.

Verified by: `TLio.Functions.Tests/Fixtures/Math/averageifs/02-two-criteria.json`.

## Criteria syntax

Same operator-prefix and wildcard rules as `sumifs` — see
[SumIfs.md — Criteria syntax](SumIfs.md#criteria-syntax).

## When to use

- A conditional mean that depends on **two or more conditions at once**.

## When NOT to use

- **Only one condition** — `averageif` is simpler.
- You need a **total** — use `sumifs`.
- You need a **count** — use `countifs`.

## Performance

`averageifs` resolves and flattens every range once, then walks `average_range`'s length,
testing all `k` criteria per index (short-circuiting on the first failed pair) — roughly
O(n × k). As with the rest of the family, criteria strings are parsed per element, not
compiled once, so cost scales with both the array length and the number of criteria pairs.

## Comparison

See [SumIfs.md — Comparison](SumIfs.md#comparison) for how all six `*if`/`*ifs` functions
relate, including which ones fail versus return `0` on no match.

## Common mistakes

- **Odd argument count only** — `average_range` plus complete pairs.
- **All criteria must match (AND), never OR.**
- **`0` on no match looks like a real zero average** — the same ambiguity as `averageif`.
- **Ranges of different lengths** — the loop walks `average_range`'s length; a shorter
  criteria range fails its condition past its own end rather than raising an error.
- **Wrong path scope** — every range path resolves against the document root, not the current
  node.
