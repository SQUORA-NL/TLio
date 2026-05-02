# count

> Counts the number of elements in an array or matching nodes at a path.

## Syntax

```
=count(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array or path | yes | Path to the array or collection to count. |

## Returns

A long (integer) node with the element count.

## Example

```json
{ "command": "set", "path": "$.n", "value": "=count($.tags)" }
```

Input: `{ "tags": ["a", "b", "c"], "n": 0 }`
Output: `{ "tags": [...], "n": 3 }`

## When to use

- You need the **quantity** of items in an array — order lines, tags, users, results.
- The array elements are non-numeric (strings, objects, booleans) — `count` works on any element type.
- You want to count all matched nodes from a wildcard: `=count($.orders[*])`.
- You need to know if an array is non-empty before processing.

## When NOT to use

- You need the **total** of numeric values — use `sum`.
- You need a **conditional count** (only elements that match a value) — use `countif`.
- You need the **mean** — use `avg`.

## Comparison

| Function | Question answered | Works on non-numeric? |
|----------|-------------------|----------------------|
| `count` | How many items? | Yes — any element type |
| `sum` | What is the total? | No — numeric only |
| `countif` | How many match a value? | Yes — string match |

## Common mistakes

- **Using `sum` when you want a count** — if each element happens to be 1, `sum` produces the count, but this breaks the moment values differ. Use `count`.
- **Using `count` when you want a total** — `count` ignores the numeric value of elements; it only counts them.
- **Wildcard path** — `=count($.items[*].id)` counts the resolved id nodes, not the items array length. Both give the same number when every item has an id, but differ when ids are missing. Use `$.items` to count the array itself.
- **Wrong path scope** — path arguments resolve against the document root, not the current node.
