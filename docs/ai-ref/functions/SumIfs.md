# sumifs

> Sums `sum_range` where **every** criteria pair matches — the multi-condition sibling of `sumif`.

## Syntax

```
=sumifs(<sum_range>, <criteria_range1>, <criteria1> [, <criteria_range2>, <criteria2>, ...])
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the numeric array to sum. |
| 2, 4, 6… | array path | yes | A criteria range, parallel to `sum_range`. |
| 3, 5, 7… | string literal | yes | The criteria that its preceding range must satisfy (single-quoted). |

Requires an odd number of arguments, at least 3 (`sum_range` plus one or more
`(criteria_range, criteria)` pairs).

## Returns

A numeric node with the conditional sum. **`0` — not a failure — when nothing matches.**

## Example

```json
{
  "command": "set", "path": "$.paid_nl_total",
  "value": "=sumifs($.amounts,$.status,'paid',$.country,'NL')"
}
```

Input:
```json
{
  "status":  ["paid", "paid", "pending"],
  "country": ["NL", "BE", "NL"],
  "amounts": [100, 200, 150]
}
```
Output: `"paid_nl_total": 100` — only index 0 has `status="paid"` **and** `country="NL"`.

## Criteria syntax

Each criteria string is not limited to exact equality. An optional operator prefix changes
the comparison, and `*` / `?` work as equality wildcards:

| Criteria | Meaning |
|----------|---------|
| `'paid'` | equals `"paid"` (default, no prefix) |
| `'>=100'` | numeric, greater than or equal to 100 |
| `'<>0'` or `'!=0'` | not equal to 0 |
| `'jo*'` | starts with `jo` (case-insensitive) |
| `'~*text'` | literal leading `*`, escaped with `~` |

Numeric comparison is tried first; if either side does not parse as a number the comparison
falls back to a case-insensitive string comparison. See [SumIf.md](SumIf.md) for the same
rules on the single-condition form.

## When to use

- A filtered total that depends on **two or more conditions at once** — paid amounts for one
  country, claims above a threshold and within a date band, etc.
- All the ranges involved (the range being summed and every criteria range) are **parallel
  arrays** — same length, same index alignment.

## When NOT to use

- **Only one condition** — `sumif` is the same idea with less ceremony.
- The ranges are **not parallel** — misaligned arrays silently compare the wrong pairs.
- You need a **filtered count**, not a total — use `countifs`.
- You need an **unconditional total** — use `sum`.

## Comparison

| Function | Conditions | Value range | Nothing matches |
|----------|-----------|-------------|------------------|
| `sumif` | exactly 1 | required (3rd arg, optional — defaults to `range`) | `0` |
| `sumifs` | 1 or more (AND) | required (1st arg) | `0` |
| `countifs` | 1 or more (AND) | none (counts rows) | `0` |
| `averageifs` | 1 or more (AND) | required (1st arg) | `0` |
| `minifs` / `maxifs` | 1 or more (AND) | required (1st arg) | **fails** |

## Common mistakes

- **Odd argument count only.** `sumifs` requires `sum_range` plus complete
  `(criteria_range, criteria)` pairs — an even total, or fewer than 3 arguments, fails
  immediately with a warning, before any range is even resolved.
- **All criteria must match (AND), never OR.** There is no built-in "any of these conditions"
  form; run separate `sumif`/`sumifs` calls and add the results if OR logic is needed.
- **Ranges of different lengths silently under-match.** The sum walks `sum_range`'s length; a
  shorter criteria range is treated as "no match" for the missing indices rather than an
  error.
- **Using double quotes for criteria** — criteria must be single-quoted inside the expression.
- **Wrong path scope** — every range path resolves against the document root, not the current
  node.
