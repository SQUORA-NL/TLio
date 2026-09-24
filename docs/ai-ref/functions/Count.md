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

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=count($.nums)" }
```

Input: `{ "nums": [1, 2, 3, 4], "a": 10, "b": 20 }`
Output: adds `"result": 4`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/count/01-array.json`
(`02-two-scalars.json` verifies `=count($.a, $.b)` counting two scalar arguments as `2`.)

A path that does not resolve to anything returns `0` — `count` does **not** fail, unlike
`sum`/`avg`/`min`/`max`/`median`:

```json
{ "command": "put", "path": "$.result", "value": "=count($.missing)" }
```

Input: `{ "nums": [1, 2, 3, 4] }` → Output: `{ "nums": [1, 2, 3, 4], "result": 0 }`

Verified by: `TLio.Functions.Tests/Fixtures/Math/count/03-path-not-found-returns-zero.json`

A found-but-`null` node counts as **one** item, not zero and not excluded:

```json
{ "command": "put", "path": "$.result", "value": "=count($.nul)" }
```

Input: `{ "nul": null }` → Output: `{ "nul": null, "result": 1 }`

Verified by: `TLio.Functions.Tests/Fixtures/Math/count/04-null-counts-as-one.json`

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

- **Assuming `count` fails like `sum`/`avg`/`min`/`max` on a missing path.** It does not —
  a non-existent path is simply skipped, so `=count($.missing)` succeeds with `0` rather than
  aborting the script. If you need to *detect* the path is missing, use `exists` first.
- **Using `sum` when you want a count** — if each element happens to be 1, `sum` produces the count, but this breaks the moment values differ. Use `count`.
- **Using `count` when you want a total** — `count` ignores the numeric value of elements; it only counts them.
- **Wildcard path** — `=count($.items[*].id)` counts the resolved id nodes, not the items array length. Both give the same number when every item has an id, but differ when ids are missing. Use `$.items` to count the array itself.
- **Wrong path scope** — path arguments resolve against the document root, not the current node.
