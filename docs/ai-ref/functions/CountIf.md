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
| 2 | string literal or path | yes | The criteria to match — either a single-quoted literal (`'active'`) or a path to a string node holding the criteria string. |

## Returns

A long node with the count of matching elements. `0` when nothing matches (not a failure).

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=countif($.nums, $.crit_gt3)" }
```

Input: `{ "nums": [1, 2, 3, 4, 5], "cat": ["A", "B", "A", "C", "A"], "crit_gt3": ">3", "crit_A": "A", "crit_Z": "Z" }`
Output: `..., "result": 2` — `4` and `5` are the only elements `>3`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/countif/01-numeric-criteria.json`. The sibling
`02-string-criteria.json` runs `=countif($.cat, $.crit_A)` over the same input → `3` (string
equality against `"A"`), and `03-no-match.json` confirms a criteria that matches nothing
(`$.crit_Z` = `"Z"`) returns `0`, not a failure.

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

## Performance

`countif` walks the resolved range once, calling `ConditionEvaluator.EvaluateCondition` per
element — cost is O(n) in the range's length. The criteria string itself is parsed fresh on
every call (no compiled/cached predicate), so re-running the same `countif` many times (e.g.
once per row from an outer loop) re-parses the same criteria each time; that cost is small
per call but adds up linearly with how often the function runs, separately from the O(n) scan
inside each call.

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
