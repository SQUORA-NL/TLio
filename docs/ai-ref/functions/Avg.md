# avg

> Computes the arithmetic mean of all numeric values at the given path.

## Syntax

```
=avg(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | numeric array or path | yes | Path to an array of numbers. |

## Returns

A double node representing the average.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=avg($.nums)" }
```

Input: `{ "nums": [2, 4, 6], "a": 10, "b": 20 }`
Output: adds `"result": 4` (`(2 + 4 + 6) / 3`).

Verified by: `TLio.Functions.Tests/Fixtures/Math/avg/01-array.json`
(`02-two-values.json` in the same directory verifies multiple path arguments —
`=avg($.a, $.b)` → `15` — are averaged together, and `03-fractional-result.json`
verifies a non-integer mean, `[1, 2]` → `1.5`.)

## Notes

- Path not found (the argument does not resolve to any node at all) → command **fails**.
- An argument that resolves but is an **empty array** is different: it contributes nothing
  and, if every argument is empty, `avg` returns `0` rather than failing — see `Avg.cs`.
- Found-but-`null` values are treated as `0`, and they count toward the denominator too
  (a 3-element array with one `null` averages over 3, not 2).

## When to use

- You need the **arithmetic mean** of a numeric array — average score, average price, average duration.
- The data is roughly symmetric and outliers are expected to contribute proportionally.
- A wildcard path like `$.items[*].score` is valid — all resolved values are averaged as a flat list.

## When NOT to use

- Your data has significant outliers that would distort the mean — use `median` instead.
- You need the **total**, not the mean — use `sum`.
- You need the **count** of elements — use `count`.
- Null elements are present and you want them excluded from the denominator — `avg` treats nulls as 0, which lowers the result.

## Comparison

| Function | Question answered | Input requirement |
|----------|-------------------|-------------------|
| `avg` | What is the mean? | Numeric array; fails only if the path is missing, not if it is empty (returns `0`) |
| `sum` | What is the total? | Numeric array; fails only if the path is missing, not if it is empty (returns `0`) |
| `median` | What is the middle value? | Numeric array; robust to outliers; same missing-vs-empty rule as `avg` |
| `min` / `max` | Smallest / largest value | Numeric array; fails on missing path **and** on an empty array — there is no value to return |
| `count` | How many items? | Any array |

## Common mistakes

- **Confusing avg with median** — `avg` is pulled by outliers; a single very large value skews it. Use `median` for skewed distributions.
- **Null elements lowering the average** — nulls count as 0 in both the numerator and denominator, dragging the mean down. Filter nulls before averaging if this matters.
- **Path not found = failure, not zero** — if `$.scores` does not exist at all, the command fails. An *existing* empty array is different and yields `0` — see Notes above. Ensure the path always resolves if you need the failure signal.
- **Wrong path scope** — all path arguments resolve against the document root, not the current node.
