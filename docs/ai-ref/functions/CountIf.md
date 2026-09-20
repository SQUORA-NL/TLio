# countif

> Counts elements in a range that match a given criteria.

## Syntax

```
=countif(<range>, <criteria>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the array to evaluate. |
| 2 | string literal | yes | The value to match (single-quoted: `'active'`). |

## Returns

A long node with the count of matching elements.

## Example

```json
{ "command": "set", "path": "$.active_count", "value": "=countif($.status,'active')" }
```

Input: `{ "status": ["active","inactive","active"], "active_count": 0 }`
Output: `{ ..., "active_count": 2 }`

## Criteria syntax

The criteria string is not limited to exact equality. An optional operator prefix changes
the comparison, and `*` / `?` work as equality wildcards — `'>80'`, `'<>0'`, `'jo*'` are all
valid. See [SumIf.md — Criteria syntax](SumIf.md#criteria-syntax) for the full table; both
functions share the same evaluator.

## When to use

- You need the **count** of elements in an array that match a specific value or comparison —
  how many orders are "paid", how many scores are `>=80`.
- You want a filtered count **without** a separate parallel array — `countif` takes a single
  range and a criteria value.

## When NOT to use

- You need the **filtered total** of a numeric field — use `sumif` (with a parallel criteria array and a sum array).
- You need an **unconditional count** — use `count`.
- You need **two or more conditions at once** (AND) — use `countifs`.
- The criteria is a **numeric literal** — wrap it in single quotes: `'42'`, not `42`.

## Comparison

| Function | Returns | Conditions | Takes parallel array? |
|----------|---------|-----------|----------------------|
| `countif` | Filtered count | exactly 1 | No — single range |
| `countifs` | Filtered count | 1 or more (AND) | No — but multiple ranges |
| `sumif` | Filtered total | exactly 1 | Yes — criteria_range + sum_range |
| `count` | Unconditional count | None | No |
| `sum` | Unconditional total | None | No |

## Common mistakes

- **Using double quotes for criteria** — criteria must be single-quoted inside the expression: `'active'` not `"active"`. Double quotes break parsing.
- **Confusing countif with sumif** — `countif` counts how many match; it has no sum_range. If you need a conditional total of numeric values, use `sumif`.
- **Expecting countif to use a separate criteria range** — unlike `sumif`, `countif` takes only one array (the range being tested). The criteria is matched directly against elements in that range.
- **Wrong path scope** — the range path resolves against the document root, not the current node.
