# maxifs

> The largest value in `max_range` among rows where **every** criteria pair matches.

## Syntax

```
=maxifs(<max_range>, <criteria_range1>, <criteria1> [, <criteria_range2>, <criteria2>, ...])
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the numeric array to take the maximum of. |
| 2, 4, 6… | array path | yes | A criteria range, parallel to `max_range`. |
| 3, 5, 7… | string literal | yes | The criteria for its preceding range (single-quoted). |

Requires an odd number of arguments, at least 3.

## Returns

A numeric node — the largest matching value.

**Fails when nothing matches — it does not return `0` or `null`.** This is the opposite
convention from `sumifs`/`averageifs`. See [MinIfs.md](MinIfs.md), its mirror image.

## Example

```json
{
  "command": "set", "path": "$.max_paid_amount",
  "value": "=maxifs($.amounts,$.status,'paid')"
}
```

Input: `{ "status": ["paid","pending","paid"], "amounts": [100, 200, 150] }`
Output: `"max_paid_amount": 150`

## Criteria syntax

Same operator-prefix and wildcard rules as `sumifs` — see
[SumIfs.md — Criteria syntax](SumIfs.md#criteria-syntax).

## When to use

- The **largest value among filtered rows** — highest matching claim, latest matching
  numeric period, top score in a segment.
- You want a **hard failure** (not a silent `0`) when the filter matches nothing.

## When NOT to use

- You need the **smallest** value — use `minifs`.
- You need a **total or mean** — use `sumifs`/`averageifs`.
- You need the **unconditional** maximum — use `max`.
- A no-match should be a **`0`**, not a script failure — guard with `countifs` first, or
  branch with `ifElse` before calling `maxifs`.

## Comparison

See [MinIfs.md — Comparison](MinIfs.md#comparison) for how all six `*if`/`*ifs` functions
relate.

## Common mistakes

- **No-match is a failure, not a `0`** — the same trap as `minifs`. Check with `countifs`
  first if a match is not guaranteed.
- **Odd argument count only** — `max_range` plus complete pairs.
- **All criteria must match (AND), never OR.**
- **Wrong path scope** — every range path resolves against the document root, not the current
  node.
