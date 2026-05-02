# sum

> Sums all numeric values from one or more paths (arrays are flattened).

## Syntax

```
=sum(<path1> [, <path2>, ...])
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1+ | numeric array or path | yes | One or more paths to numeric values. Arrays are flattened. |

## Returns

A numeric node (long when whole, double when fractional) equal to the sum of all resolved values.

**Failure**: returns failure (not 0) when the path does not exist. Check that the path selects at least one node.

## Example

```json
{ "command": "set", "path": "$.total", "value": "=sum($.prices)" }
```

Input: `{ "prices": [9.99, 4.99, 14.99], "total": 0 }`
Output: `{ "prices": [...], "total": 29.97 }`

## Notes

- Path not found → command **fails** with an error trace entry
- Null elements in the array are treated as 0

## When to use

- You need the **total** of a numeric array — revenue, quantities, scores, weights.
- The source is a wildcard path like `$.orders[*].amount` — the wildcard resolves all matching values as a flat list, and `sum` aggregates them correctly.
- You need to sum across multiple disjoint paths: `=sum($.a, $.b)`.

## When NOT to use

- You need the **count** of items — use `count` instead. `sum` on a list of ones accidentally produces a count, but that is fragile.
- The path might not exist at runtime — guard with a conditional or ensure the path is always present, because a missing path causes failure, not zero.
- You need a conditional total (only some rows) — use `sumif` instead.

## Comparison

| Function | Question answered | Input requirement |
|----------|-------------------|-------------------|
| `sum` | What is the total? | Numeric array; fails if empty/missing |
| `avg` | What is the mean? | Numeric array; fails if empty/missing |
| `count` | How many items? | Any array (strings, objects, numbers) |
| `sumif` | Total where condition? | Two parallel arrays of equal length |

## Common mistakes

- **Using `sum` to count** — `=sum($.items)` when items are non-numeric silently fails or produces wrong results. Use `=count($.items)`.
- **Empty array** — `sum` of an empty array fails. If the array might be empty, ensure at least one value exists or use a conditional command.
- **Wrong path scope** — inside a function, paths resolve against the document ROOT (`$`), not the current node. `@.field` inside a function argument refers to the root, not an array element.
- **Forgetting wildcard flattening** — `$.items[*].price` passes a flat list of prices to `sum`; this is correct and intended behavior.
