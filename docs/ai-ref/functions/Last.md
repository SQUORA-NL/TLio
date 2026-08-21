# last

> Returns the last node matched by a path expression.

## Syntax

```
=last(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | path | yes | A path expression. `last` works on the **matched node set**, so use `$.items[*]`, not `$.items`. |

Exactly one argument. Two or more is an error.

## Returns

The last node in the match, as it is. A path that matches nothing **fails** and aborts the
script — the same answer `partial` gives when its index addresses nothing.

## Example

```json
{ "command": "put", "path": "$.latest", "value": "=last($.items[*])" }
```

Input:

```json
{ "items": ["first", "second", "third"] }
```

Output:

```json
{ "items": ["first", "second", "third"], "latest": "third" }
```

Objects come back whole:

Input: `{ "events": [{ "on": "2024-01-01" }, { "on": "2024-06-01" }] }`
`=last($.events[*])` → `{ "on": "2024-06-01" }`

## When to use

- Reading the most recent entry of an append-only list: the latest status, the last event, the final instalment.
- Reaching the end of a collection whose length you do not know — the whole reason this exists.
- Taking the top of a ranking after ordering ascending: `=last(...)` after `sort` writes the array, or simply sort descending and use `partial`.

Unlike `distinct`, `sort` and `sortBy`, `last` returns a **single node**, not a collection — so
`set` on a scalar field is the natural containing command, and the target is only an array path
when the last matched node happens to be an array or object.

## When NOT to use

- You want the *n*th element from the front — use `partial($.items[*], n)`, which is what it is for.
- You want the last element of an array *value* rather than of a node set — see the mistakes below; `=last($.items)` returns the array.
- You want to know *how many* elements there are — use `count`.
- You want a specific element by key or predicate — put the predicate in the path (`$.items[?(@.type=='wa')]` in JSON, `/order/item[@id='1']` in XML) and use `fetch`.

## Comparison

| Function | Counts from | Needs the length | Returns |
|---|---|---|---|
| `last($.items[*])` | the end | no | the final matched node |
| `partial($.items[*], n)` | the front (0-based); `n` omitted means the first | yes, to reach the end | the *n*th matched node |
| `fetch($.items[*])` | — | — | the **first** matched node (or a default, with a second argument) |
| `count($.items[*])` | — | — | how many nodes matched |

`partial` counts from the front only, and the length of a collection is not something a script
can compute into an argument position — which is exactly the gap `last` closes.

## Common mistakes

- **`=last($.items)` returns the array, not its last element.** Like `partial`, `last` operates on the *matched node set*. `$.items` matches one node — the array — so that array is what comes back. Write `$.items[*]`.
- **An empty array fails.** `=last($.empty[*])` matches nothing, which logs an error and aborts the script. Guard with `=exists($.empty[0])` or use `fetch` with a default when an empty collection is a normal case.
- **Confusing "last" with "largest".** `last` is positional: it returns the final element in *document order*, not the maximum. Sort first if you want an extreme, or use `max` / `maxDate`.
- **Assuming array order is meaningful.** In XML an "array" is a run of same-named sibling elements; the last one is the last sibling in the document. In JSON and YAML it is the last element of the sequence. In all three it is document order, never sorted order.
- **Passing two arguments.** `last` takes exactly one. There is no index argument — that is `partial`.
