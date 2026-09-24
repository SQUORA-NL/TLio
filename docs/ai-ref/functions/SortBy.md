# sortby

> Returns a new array of objects ordered by a key read from each element.

## Syntax

```
=sortby(<array>, '<keyPath>')
=sortby(<array>, '<keyPath>', 'asc'|'desc')
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array or path | yes | The collection to order. `$.items` and `$.items[*]` both mean "the members of items". |
| 2 | key path | yes | A plain chain of property names, resolved **against each element**: `'$.premium'`, `'premium'`, `'$.rating.factor'`, and in XML `'/premium'`. |
| 3 | `'asc'` or `'desc'` | no | Direction, case-insensitive. Default `'asc'`. Any other word is an error. |

## Returns

A **new array**. Elements are deep clones, so the source array is never modified.

Ordering follows the same rule as `sort`, applied to the **extracted keys**: numeric when every
key is numeric, `StringComparer.Ordinal` on the string form otherwise. The sort is **stable**, so
elements with equal keys keep their document order.

**An element whose key path matches nothing sorts last — in `asc` and in `desc` alike.** That is a
decision, not a fallout of the comparison: a record missing the very key it is being ordered by
has no place in the order, so it stays out of the way at the end rather than jumping to the front
when the direction flips.

## Verified example

```json
{ "command": "put", "path": "$.ranked", "value": "=sortby($.coverages, '$.premium', 'desc')" }
```

Input:

```json
{
  "coverages": [
    { "code": "wa", "premium": 120 },
    { "code": "rechtsbijstand", "premium": 60 },
    { "code": "casco", "premium": 240 }
  ]
}
```

Output adds:

```json
{
  "ranked": [
    { "code": "casco", "premium": 240 },
    { "code": "wa", "premium": 120 },
    { "code": "rechtsbijstand", "premium": 60 }
  ]
}
```

Verified by: `TLio.Functions.Tests/Fixtures/Collections/sortby/02-coverages-by-premium-descending.json`
(the ascending default, `'$.premium'` with no third argument, is
`01-coverages-by-premium.json`, and a nested key path `'$.rating.factor'` is
`04-nested-key-path.json`).

Missing keys go to the end regardless of direction:

Input: `{ "claims": [{ "id": "b", "amount": 200 }, { "id": "x" }, { "id": "a", "amount": 100 }] }`,
script `=sortby($.claims, '$.amount', 'desc')` → `ranked: [b(200), a(100), x]`.

Verified by: `TLio.Functions.Tests/Fixtures/Collections/sortby/03-missing-key-sorts-last.json`
(descending case). The ascending case — same input, `'asc'` gives `[a(100), b(200), x]` — is
covered by the inline test `SortByTests.SortBy_MissingKeySortsLastInBothDirections`
(`TLio.Functions.Tests/FunctionsTests/CollectionTests/SortByTests.cs:117`), which asserts both
directions on one input.

## When to use

- The reporting case this exists for: coverages by premium, claims by date, drivers by age, line items by amount.
- Producing a stable presentation order before serialising to CSV or XML.
- Ranking: sort descending and read the head of the list with `partial`, or the tail with `last`.
- **Note the shape of the result.** `sortBy` returns a *collection*, so the containing command is normally `put` writing to an array path (`$.ranked`), not `set` on a scalar field.

## When NOT to use

- The elements are scalars, not objects — there is no key to read. Use `sort`.
- You need to *enrich* each element from another collection rather than reorder them — that is the `resolve` command (ETL pack), which matches elements against a reference collection and writes derived values into them. `resolve` changes contents and leaves order alone; `sortBy` changes order and leaves contents alone.
- You need to sort by something computed rather than stored (`premium * factor`). Compute it into a property first — with a `put` over `$.items[*]` — then sort by that property.

## Comparison

| Tool | Kind | Orders by | Touches |
|---|---|---|---|
| `sortBy` | function | a property of each element | order only; returns a new array |
| `sort` | function | the element itself | order only; returns a new array |
| `resolve` | command (ETL) | nothing — it is a lookup, not an ordering | element *contents*, in place |

`min`, `max`, `minDate` and `maxDate` answer the one-value question. Ordering a collection by one
of its fields had no expression in the language before `sortBy`.

## Common mistakes

- **Expecting a missing key to flip with the direction.** It does not. Elements without the key are appended at the end in both `'asc'` and `'desc'`, in their original document order. If you need them first, give them a default value before sorting.
- **Writing a key path with a subscript, wildcard, predicate or recursive descent.** `'$.premium[0]'`, `'$..premium'`, `'$.items[*].premium'` are all rejected with an error. The key path is resolved against a single element, and the format fetchers disagree about what those constructs mean there — so only a plain property chain is accepted.
- **Using the document's path language wrongly.** The key path speaks the *document's* path language: `'$.premium'` for JSON and YAML, `'/premium'` or `'premium'` for XML. A bare `'premium'` works in every format.
- **Assuming numeric order for mixed keys.** One non-numeric key switches the whole comparison to ordinal text, exactly as in `sort`: `"10"` then sorts before `"9"`.
- **`$.coverages[*]` on an empty array fails.** `[*]` matches nothing when the array is empty and a path matching nothing aborts the script. Use `=sortby($.coverages, '$.premium')`.
- **Forgetting the quotes on the key path.** `=sortby($.coverages, $.premium)` happens to work, because a `$…` argument is handed to the function as text either way — but quoting it (`'$.premium'`) says plainly that it is a path *for the element*, not a value read from the document root.
