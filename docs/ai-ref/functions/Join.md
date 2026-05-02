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

A string node.

## Example

```json
{ "command": "set", "path": "$.date", "value": "=join($.parts,'-')" }
```

Input: `{ "parts": ["2024", "01", "15"], "date": "" }`
Output: `{ ..., "date": "2024-01-15" }`

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
- **Using join for fixed fields**: `=join($.parts,'-')` requires `$.parts` to be an array. If you only have `$.a` and `$.b`, use `=concat($.a,'-',$.b)`.
- **Single-element arrays**: `join` on a one-element array returns that element with no separator — this is correct, but verify upstream logic actually populates the array.
- **Null array**: if the path resolves to null or a non-array, `join` fails. Guard with an `isEmpty` check or ensure the array is always initialised.
