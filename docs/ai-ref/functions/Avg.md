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

## Example

```json
{ "command": "set", "path": "$.average", "value": "=avg($.scores)" }
```

Input: `{ "scores": [80, 90, 100], "average": 0 }`
Output: `{ "scores": [...], "average": 90.0 }`

## Notes

- Path not found → command **fails**
- Null values in the array are treated as 0

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
| `avg` | What is the mean? | Numeric array; fails if empty/missing |
| `sum` | What is the total? | Numeric array; fails if empty/missing |
| `median` | What is the middle value? | Numeric array; robust to outliers |
| `count` | How many items? | Any array |

## Common mistakes

- **Confusing avg with median** — `avg` is pulled by outliers; a single very large value skews it. Use `median` for skewed distributions.
- **Null elements lowering the average** — nulls count as 0 in both the numerator and denominator, dragging the mean down. Filter nulls before averaging if this matters.
- **Path not found = failure, not zero** — if `$.scores` does not exist, the command fails. Ensure the path always resolves.
- **Wrong path scope** — all path arguments resolve against the document root, not the current node.
