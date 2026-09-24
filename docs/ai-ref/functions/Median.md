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

A numeric node equal to the median value. The array is sorted internally before picking
the middle value(s) — the input order does not matter.

## Notes

- Path not found (the argument does not resolve at all) → command **fails**.
- A present-but-**empty** array is different: `median` returns `0` rather than failing
  (same rule as `avg`, unlike `min`/`max`, which fail on empty too).
- Found-but-`null` values are treated as `0` and take part in the sort like any other value.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=median($.odd)" }
```

Input: `{ "odd": [1, 3, 5], "even": [1, 2, 3, 4], "a": 2, "b": 6 }`
Output: adds `"result": 3` — the middle of the three sorted values.

Verified by: `TLio.Functions.Tests/Fixtures/Math/median/01-odd-count.json`

An even-length array averages its two middle values:

```json
{ "command": "put", "path": "$.result", "value": "=median($.even)" }
```

Same input → adds `"result": 2.5` (`(2 + 3) / 2`, the two central values of `[1, 2, 3, 4]`).

Verified by: `TLio.Functions.Tests/Fixtures/Math/median/02-even-count.json`
(`03-two-scalars.json` verifies `=median($.a, $.b)` — with only two values, the median is
their mean, `4`.)

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
