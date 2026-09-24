# join

> Joins array elements into a single string with a separator.

## Syntax

```
=join(<array>, <separator>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array or path | yes | Path to an array of strings/values. |
| 2 | string or path | yes | The separator to insert between elements. |

## Returns

A string node. An array argument is flattened one level and each element rendered as text (a
`null` element becomes `""`); a scalar argument is treated as a one-element list, so
`=join($.a,'-')` on a non-array `$.a` returns that value's text with no separator applied — it
does not require an array. **Only a path matching nothing fails** the script; a *found* `null`
resolves to a single `""` element, not a failure.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=join($.arr, $.sep)" }
```

Input: `{ "arr": ["x", "y", "z"], "sep": "-" }`
Output: `{ "arr": ["x", "y", "z"], "sep": "-", "result": "x-y-z" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/join/01-with-separator.json`. The same directory
covers an empty separator (`02-empty-separator.json`: `["a","b","c"]` with `sep: ""` →
`"abc"`).

## When to use

- You have an array of values at a document path and need to produce a single delimited string.
- The number of elements is dynamic or unknown at script-write time.
- You have previously used `split` to decompose a string and want to re-join after transformation.
- The separator is the same between every pair of elements.

## When NOT to use

- You are joining 2–3 fixed, named fields with different separators between them — use `concat` instead (more readable for fixed fields).
- The values to join are not in an array — use `concat` and reference each field individually.
- You need per-element transformation before joining — transform each element with individual steps first, then `join`.

## Comparison

| Function | Input style | Separator | Use when |
|----------|------------|-----------|----------|
| `join` | Array path + single separator | Single separator for all gaps | Dynamic or array-sourced list of values |
| `concat` | Fixed individual args | Literal arg between values | 2–4 known fields, arbitrary separators |
| `format` | Template string + positional args | Part of template literal | Sentence-style templates with `{0}` placeholders |

## Common mistakes

- **Path resolution**: the array argument resolves against the document root (dataContext). `@.items` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths as first argument**: `$.items[*].name` grabs all matching values as a flat list and passes them to `join` — this may work for simple cases but behaves unexpectedly if items are nested objects. Prefer a pre-built string array at a known path.
- **Using join for fixed, separately-named fields**: `join` takes one array *path*, not a list of
  arguments — `=join($.a,'-',$.b,'-')` is not valid syntax for "a-b". If you only have `$.a` and
  `$.b`, use `=concat($.a,'-',$.b)`.
- **Single-element arrays**: `join` on a one-element array returns that element with no separator — this is correct, but verify upstream logic actually populates the array.
- **Assuming a `null` array fails the same way a missing path does.** It does not: a *found*
  `null` resolves to a single empty-string element (`""`), so `=join($.nul,'-')` returns `""`,
  not a script failure. Only a path that matches **nothing at all** fails — guard that case with
  `exists` if the field might be entirely absent.
