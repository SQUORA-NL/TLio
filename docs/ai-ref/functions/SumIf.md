# sumif

> Sums values in sum_range where the corresponding criteria_range value matches criteria.

## Syntax

```
=sumif(<range>, <criteria>)
=sumif(<range>, <criteria>, <sum_range>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the range tested against `criteria`. |
| 2 | string literal or path | yes | The criteria (see below). Single-quoted when a literal: `'paid'`. |
| 3 | array path | no | Path to the array summed for matching indices. **Omit it and `range` itself is both tested and summed** — that is the two-argument form, not a separate default value. |

## Returns

A numeric node with the conditional sum. `0` when nothing matches — not a failure.

## Verified example

Two-argument form — `range` is both the test and the value being summed:

```json
{ "command": "put", "path": "$.result", "value": "=sumif($.nums, $.crit_gt3)" }
```

Input: `{ "nums": [1, 2, 3, 4, 5], "crit_gt3": ">3" }`
Output: `$.result` is `9` (`4 + 5`, the elements of `nums` greater than 3).

Verified by: `TLio.Functions.Tests/Fixtures/Math/sumif/01-numeric-criteria.json`

Three-argument form — `sum_range` is a different array than the one tested:

```json
{ "command": "put", "path": "$.result", "value": "=sumif($.cat, $.crit_A, $.values)" }
```

Input: `{ "cat": ["A","B","A","C","A"], "crit_A": "A", "values": [10, 20, 30, 40, 50] }`
Output: `$.result` is `90` (indices 0, 2, 4 have `cat == "A"`; `10 + 30 + 50`).

Verified by: `TLio.Functions.Tests/Fixtures/Math/sumif/02-with-sum-range.json`

No match returns `0`, not a failure — verified by
`TLio.Functions.Tests/Fixtures/Math/sumif/03-no-match.json`.

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

- The tested range and `sum_range` (when given) must be parallel (same length, same index
  alignment) — `sumif` walks `min(range.Count, sumRange.Count)`, so a length mismatch silently
  truncates rather than erroring.
- Single quotes are only needed for a criteria **literal** written inline in the function call
  (`=sumif($.range,'paid')`) — the parser needs the quotes to tell a string literal from a
  bare path. A criteria argument that is itself a path (`=sumif($.nums,$.crit_gt3)`, as in the
  verified examples above) needs no quoting; the string it resolves to is used as-is.

## Performance

`sumif` resolves `range` and `sum_range` into lists once, then walks them together with a single
`for` loop, evaluating one `ConditionEvaluator.EvaluateCondition` call per element — cost scales
linearly with array length. Only the surrounding quote-stripping (`ExtractCriteria`) happens once,
before the loop; `ConditionEvaluator.EvaluateCondition` itself re-parses the operator prefix
(`>=`, `<>`, …) on every element, and for a wildcard criteria (`*`/`?`) it also rebuilds and runs
a `Regex.IsMatch` per element — there is no compiled/cached pattern reused across elements or
across calls. For a very large array evaluated with the same criteria many times across a
script, pre-filtering upstream (or restructuring so the condition runs once, not once per
`sumif` call) is the only lever.

## When to use

- You need a **filtered total** — sum only the amounts where a corresponding status, category, or flag matches a value.
- The data is structured as **parallel arrays** — one array for the condition, one for the values (same length, aligned by index).
- You want to avoid pre-filtering the data with separate commands before summing.

## When NOT to use

- You need a **filtered count** (how many match), not a total — use `countif`.
- You need an **unconditional total** — use `sum`.
- The range and `sum_range` are **not parallel** (different lengths or not index-aligned) — results will be wrong. Ensure the arrays come from the same source and have the same structure.
- You need **two or more conditions at once** (AND) — use `sumifs`.
- The criteria is a **numeric literal written inline** — wrap it in single quotes: `'42'`, not `42`.

## Comparison

| Function | Returns | Conditions |
|----------|---------|-----------|
| `sumif` | Filtered total | exactly 1 |
| `sumifs` | Filtered total | 1 or more (AND) |
| `countif` | Filtered count | exactly 1 |
| `sum` | Unconditional total | None |
| `count` | Unconditional count | None |

## Common mistakes

- **Arrays not parallel** — `range` and `sum_range` must have the same length and be aligned by index. If they differ, `sumif` silently sums only over the shorter length's indices rather than erroring.
- **Omitting `sum_range` is not the same as passing it explicitly equal to `range`** — leaving it out is what makes `range` do double duty as both the test and the sum; there is no separate "default" argument.
- **Using double quotes for an inline criteria literal** — criteria must be single-quoted inside the expression: `'paid'` not `"paid"`. Double quotes break parsing. (Not applicable when criteria comes from a path.)
- **Confusing sumif with countif** — `sumif` sums the numeric values in `sum_range`; `countif` counts matching elements in a single range.
- **Wrong path scope** — both array path arguments resolve against the document root, not the current node.
