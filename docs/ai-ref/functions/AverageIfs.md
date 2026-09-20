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
| 3, 5, 7… | string literal | yes | The criteria for its preceding range (single-quoted). |

Requires an odd number of arguments, at least 3.

## Returns

A numeric node — the mean of the matching elements. `0` — not a failure — when nothing
matches.

## Example

```json
{
  "command": "set", "path": "$.avg_paid_nl",
  "value": "=averageifs($.amounts,$.status,'paid',$.country,'NL')"
}
```

Input:
```json
{
  "status":  ["paid", "paid", "paid"],
  "country": ["NL", "BE", "NL"],
  "amounts": [100, 200, 150]
}
```
Output: `"avg_paid_nl": 125` — mean of the two `NL` rows, `100` and `150`.

## Criteria syntax

Same operator-prefix and wildcard rules as `sumifs` — see
[SumIfs.md — Criteria syntax](SumIfs.md#criteria-syntax).

## When to use

- A conditional mean that depends on **two or more conditions at once**.

## When NOT to use

- **Only one condition** — `averageif` is simpler.
- You need a **total** — use `sumifs`.
- You need a **count** — use `countifs`.

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
