# distinct

> Returns a new array holding each distinct element once, in the order they first appear.

## Syntax

```
=distinct(<array>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array or path | yes | The collection to de-duplicate. `$.items` and `$.items[*]` both mean "the members of items". |

## Returns

A **new array**. Elements are deep clones, so the source array is never modified.

- Order-preserving: the **first** occurrence of a value keeps its position, later duplicates are dropped.
- Equality is structural (`DeepEquals`), so two identical objects collapse into one — not reference equality.
- A single non-array node comes back as a one-element array, matching how the Math pack flattens a scalar into a one-item list
  (`TLio.Functions.Tests/Fixtures/Collections/distinct/04-scalar-becomes-one-element-array.json`).
- An array that exists but is empty comes back as an empty array. That is an answer, not a failure
  (`TLio.Functions.Tests/Fixtures/Collections/distinct/03-empty-array-stays-empty.json`).

## Verified example

```json
{ "command": "put", "path": "$.unique", "value": "=distinct($.codes)" }
```

Input:

```json
{ "codes": ["wa", "casco", "wa", "rechtsbijstand", "casco"] }
```

Output:

```json
{
  "codes": ["wa", "casco", "wa", "rechtsbijstand", "casco"],
  "unique": ["wa", "casco", "rechtsbijstand"]
}
```

Verified by: `TLio.Functions.Tests/Fixtures/Collections/distinct/01-strings-first-occurrence-wins.json`.

Objects de-duplicate structurally:

Input: `{ "coverages": [{ "code": "wa", "premium": 120 }, { "code": "casco", "premium": 240 }, { "code": "wa", "premium": 120 }] }`
Output adds: `"unique": [{ "code": "wa", "premium": 120 }, { "code": "casco", "premium": 240 }]`

Verified by: `TLio.Functions.Tests/Fixtures/Collections/distinct/02-objects-compare-structurally.json`.

## When to use

- Collapsing a repeated list into a set of values: coverage codes, product groups, postcodes, error codes gathered from several places.
- Before counting distinct values: `=count(=distinct($.codes))`.
- **Note the shape of the result.** `distinct` returns a *collection*, so the containing command is normally `put` writing to an array path (`$.unique`), not `set` on a scalar field.

## When NOT to use

- You need the *number* of distinct values rather than the values — wrap it: `=count(=distinct($.codes))`. `count` alone counts every element, duplicates included.
- You need de-duplication *while merging two documents* — that is the `merge` command with `arrayHandling: "uniqueItemsWithoutKeys"`, which compares the incoming array against an existing one.
- You need de-duplication by a *key* rather than by whole-value equality (all coverages with distinct `code`, keeping the first). `distinct` compares whole elements; two coverages differing only in `premium` are both kept.

## Comparison

| Approach | Needs a second document | Compares | Result |
|---|---|---|---|
| `=distinct($.codes)` | no | whole elements, structurally | new array, first occurrence wins |
| `merge` with `uniqueItemsWithoutKeys` | **yes** — it merges a source into a target | whole elements | the merged array on the target |
| `=count($.codes)` | no | nothing | how many elements there are, duplicates included |
| `=count(=distinct($.codes))` | no | whole elements | how many *distinct* elements there are |

Before this function existed, de-duplicating a single array was unreachable: `merge` was the only
route and it needs something to merge *against*.

## Common mistakes

- **Expecting a scalar back.** `distinct` always returns an array — even for one element, even for none. `"value": "=distinct($.codes)"` on a `set` command writes an array into that field.
- **`$.codes[*]` on an empty array fails.** `$.codes[*]` matches *nothing* when `codes` is `[]`, and a path that matches nothing is an error that aborts the script. Write `=distinct($.codes)` — naming the array matches one node (the array itself) and yields an empty array. This is the trap: the two forms are interchangeable everywhere except on an empty collection.
- **Assuming reference equality.** Two separately-written but identical objects *do* collapse. If you want them both, `distinct` is the wrong tool.
- **Assuming type-insensitive matching.** In JSON, `1` and `"1"` are different nodes and both survive. In XML and YAML, where everything is text, they are the same.
- **Path not found is a failure.** `=distinct($.missing)` logs an error and aborts the script; it does not return an empty array. Only an *existing but empty* array yields `[]`.
