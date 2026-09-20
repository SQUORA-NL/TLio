# sumif

> Sums values in sum_range where the corresponding criteria_range value matches criteria.

## Syntax

```
=sumif(<criteria_range>, <criteria>, <sum_range>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the criteria array (parallel to sum_range). |
| 2 | string literal | yes | The value to match (single-quoted: `'paid'`). |
| 3 | array path | yes | Path to the numeric array to sum when criteria matches. |

## Returns

A numeric node with the conditional sum.

## Example

```json
{ "command": "set", "path": "$.paid_total", "value": "=sumif($.status,'paid',$.amounts)" }
```

Input: `{ "status": ["paid","pending","paid"], "amounts": [100, 200, 150], "paid_total": 0 }`
Output: `{ ..., "paid_total": 250 }`

## Criteria syntax

The criteria string is not limited to exact equality. An optional operator prefix changes
the comparison, and `*` / `?` work as equality wildcards:

| Criteria | Meaning |
|----------|---------|
| `'paid'` | equals `"paid"` (default, no prefix) |
| `'>=100'` | numeric, greater than or equal to 100 |
| `'<>0'` or `'!=0'` | not equal to 0 |
| `'jo*'` | starts with `jo` (case-insensitive) |
| `'~*text'` | literal leading `*`, escaped with `~` |

Numeric comparison is tried first; if either side does not parse as a number the comparison
falls back to a case-insensitive string comparison. For two or more conditions at once, see
[SumIfs.md](SumIfs.md).

## Notes

- The criteria and sum arrays must be parallel (same length, same index alignment).
- Criteria string literals must be wrapped in single quotes inside the function expression.

## When to use

- You need a **filtered total** — sum only the amounts where a corresponding status, category, or flag matches a value.
- The data is structured as **parallel arrays** — one array for the condition, one for the values (same length, aligned by index).
- You want to avoid pre-filtering the data with separate commands before summing.

## When NOT to use

- You need a **filtered count** (how many match), not a total — use `countif`.
- You need an **unconditional total** — use `sum`.
- The two arrays are **not parallel** (different lengths or not index-aligned) — results will be wrong. Ensure the arrays come from the same source and have the same structure.
- You need **two or more conditions at once** (AND) — use `sumifs`.
- The criteria is a **numeric literal** — wrap it in single quotes: `'42'`, not `42`.

## Comparison

| Function | Returns | Conditions |
|----------|---------|-----------|
| `sumif` | Filtered total | exactly 1 |
| `sumifs` | Filtered total | 1 or more (AND) |
| `countif` | Filtered count | exactly 1 |
| `sum` | Unconditional total | None |
| `count` | Unconditional count | None |

## Common mistakes

- **Arrays not parallel** — `criteria_range` and `sum_range` must have the same length and be aligned by index. If they differ, `sumif` silently processes only the overlapping indices or produces incorrect results.
- **Using double quotes for criteria** — criteria must be single-quoted inside the expression: `'paid'` not `"paid"`. Double quotes break parsing.
- **Confusing sumif with countif** — `sumif` sums the numeric values in `sum_range`; `countif` counts matching elements in a single range.
- **Wrong path scope** — both array path arguments resolve against the document root, not the current node.
