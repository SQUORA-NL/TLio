# median

> Returns the median (middle value) of a numeric array.

## Syntax

```
=median(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | numeric array or path | yes | Path to the array of numbers. |

## Returns

A numeric node equal to the median value.

## Example

```json
{ "command": "set", "path": "$.mid", "value": "=median($.values)" }
```

Input: `{ "values": [3, 1, 4, 1, 5], "mid": 0 }`
Output: `{ ..., "mid": 3 }`

## When to use

- Your data has **outliers** that would distort an arithmetic mean — salaries, housing prices, response times.
- You need the **middle value** of a sorted dataset (50th percentile).
- The array has an odd count: the middle element is returned. Even count: the average of the two middle elements is returned.

## When NOT to use

- Your data is roughly symmetric with no outliers — `avg` is simpler and more widely understood in reporting contexts.
- You need the **total** — use `sum`.
- You need the **count** — use `count`.
- You need the **smallest or largest** — use `min`/`max`.
- `median` is not a standard aggregate available in all pipeline contexts — prefer `avg` for general reporting; reserve `median` for statistical analysis.

## Comparison

| Function | Robust to outliers? | Question answered |
|----------|--------------------|--------------------|
| `median` | Yes | Middle value (50th percentile) |
| `avg` | No | Arithmetic mean |
| `min` | N/A | Smallest value |
| `max` | N/A | Largest value |

## Common mistakes

- **Choosing avg when median is appropriate** — a single extreme value (e.g., one salary of $1 000 000 in a list of $50 000 salaries) inflates the mean significantly. Use `median` when data is skewed.
- **Choosing median for simple reporting** — `median` is a statistical concept. If stakeholders expect a sum or mean, use the right function.
- **Even-length arrays** — for an even number of elements, `median` returns the mean of the two central values, which may be a non-integer even if all inputs are integers.
- **Wrong path scope** — path arguments resolve against the document root, not the current node.
