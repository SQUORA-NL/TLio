# min

> Returns the minimum numeric value from an array at the given path.

## Syntax

```
=min(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | numeric array or path | yes | Path to a collection of numbers. |

## Returns

A numeric node equal to the smallest value in the array.

## Example

```json
{ "command": "set", "path": "$.lowest", "value": "=min($.temps)" }
```

Input: `{ "temps": [15, 22, 8, 31], "lowest": 0 }`
Output: `{ "temps": [...], "lowest": 8 }`

## When to use

- You need the **smallest** value in a numeric array — lowest price, minimum score, earliest timestamp as a number.
- The array is guaranteed to have at least one element.
- A wildcard path like `$.products[*].price` works — all resolved values are compared as a flat list.

## When NOT to use

- The array might be empty or the path might not exist — `min` returns failure in that case, not a default.
- You need the **largest** value — use `max`.
- You need the **middle** value — use `median`.
- You want to find the minimum under a condition — there is no `minif`; pre-filter the data with conditional commands before calling `min`.

## Comparison

| Function | Returns | Fails on empty/missing? |
|----------|---------|------------------------|
| `min` | Smallest value | Yes |
| `max` | Largest value | Yes |
| `median` | Middle value | No explicit note — treat as yes |
| `avg` | Arithmetic mean | Yes |

## Common mistakes

- **Empty array causes failure** — `min` on an empty array fails, not returns 0 or null. Guard against empty arrays.
- **Path not found = failure** — if the path does not resolve to any node, the command fails. Verify the path is always populated.
- **Wrong path scope** — path arguments resolve against the document root. `@.price` inside a function refers to the root, not the current array element.
- **Expecting min to handle strings** — `min` is numeric. String comparison is not supported.
