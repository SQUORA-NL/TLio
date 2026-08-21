# sort

> Returns a new array with the elements ordered ascending or descending.

## Syntax

```
=sort(<array>)
=sort(<array>, 'asc'|'desc')
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array or path | yes | The collection to order. `$.items` and `$.items[*]` both mean "the members of items". |
| 2 | `'asc'` or `'desc'` | no | Direction, case-insensitive. Default `'asc'`. Any other word is an error, not a silent ascending sort. |

## Returns

A **new array**. Elements are deep clones, so the source array is never modified.

The comparison rule is worth reading before assuming:

- When **every** element is numeric, elements are compared **numerically**.
- Otherwise every element is compared by its **string form** using `StringComparer.Ordinal`.

One non-numeric element therefore moves the whole array to text ordering, where `10` sorts before
`9`. That is deliberate: a mixed array must not produce a numeric-looking order that holds for
only part of it.

The sort is **stable** — elements that compare equal keep their document order.

## Example

```json
{ "command": "put", "path": "$.ordered", "value": "=sort($.scores, 'desc')" }
```

Input:

```json
{ "scores": [10, 2, 33, 4] }
```

Output:

```json
{ "scores": [10, 2, 33, 4], "ordered": [33, 10, 4, 2] }
```

Ordinal string ordering (uppercase before lowercase):

Input: `{ "codes": ["pear", "Apple", "banana"] }`
Output adds: `"ordered": ["Apple", "banana", "pear"]`

## When to use

- Putting a list of scalars into a presentable order for output: premiums, scores, codes, ISO date strings.
- Producing a deterministic array so two runs of the same script diff cleanly.
- Reaching the extreme *and its neighbours* — `min`/`max` give you one value; a sorted array gives you the top three.
- **Note the shape of the result.** `sort` returns a *collection*, so the containing command is normally `put` writing to an array path (`$.ordered`), not `set` on a scalar field.

## When NOT to use

- The elements are **objects** — sorting objects by their serialised text is meaningless. Use `sortBy` with a key path.
- You only need the single largest or smallest value — use `max` / `min` (numbers) or `maxDate` / `minDate` (dates). They return the value, not a collection.
- You need to reorder the array **in place** in the document rather than write a new one — `put` the result back over the original path, or use `resolve` if you are reshaping rather than ordering.

## Comparison

| Function | Input | Key | Returns |
|---|---|---|---|
| `sort` | array of scalars | the element itself | new array, ordered |
| `sortBy` | array of objects | a property read from each element | new array, ordered |
| `min` / `max` | numbers | numeric | one number |
| `minDate` / `maxDate` | date strings | chronological | one date string |

`min`/`max`/`minDate`/`maxDate` answer the one-value question and stop there. Ordering a whole
collection had no expression in the language before `sort`.

## Common mistakes

- **Assuming numeric order for a mixed array.** `[10, "two", 9]` sorts to `[10, 9, "two"]`, because one non-numeric element switches the whole array to ordinal text comparison, and `"10"` precedes `"9"`. If you want numbers, make sure every element is a number.
- **Assuming culture-aware or case-insensitive text order.** Comparison is `Ordinal`: `"Apple"` sorts before `"banana"` because `A` (65) precedes `b` (98). `"apple"` would sort *after* `"banana"`.
- **Misspelling the direction.** `'ascending'`, `'ASCENDING'`, `'up'`, `'descending'` are all rejected with an error that aborts the script. Only `asc` and `desc` are accepted (case-insensitively).
- **`$.scores[*]` on an empty array fails.** `[*]` matches nothing when the array is empty, and a path matching nothing aborts the script. Use `=sort($.scores)` — naming the array yields an empty array.
- **Sorting date strings.** Ordinal ordering is correct for ISO-8601 (`2024-06-01`) and wrong for anything else (`01-06-2024`, `June 1 2024`). Normalise the format first.
- **Expecting the document to change.** `sort` returns a value; the *command* decides where it lands. Nothing is reordered until you write the result somewhere.
