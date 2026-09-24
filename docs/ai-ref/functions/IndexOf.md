# indexOf

> Returns the zero-based index of the first occurrence of a substring.

## Syntax

```
=indexOf(<source>, <substring>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to search in. |
| 2 | string or path | yes | The substring to find. |

## Returns

A long node with the index, or -1 if not found. The match is **case-insensitive**
(`StringComparison.OrdinalIgnoreCase`) — `indexOf('Hello World','world')` is `6`, not `-1`.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=indexof($.str, $.sub)" }
```

Input: `{ "str": "Hello World", "sub": "World", "no": "XYZ" }`
Output: `{ "str": "Hello World", "sub": "World", "no": "XYZ", "result": 6 }`

Verified by `TLio.Functions.Tests/Fixtures/Text/indexof/01-found.json`. The same directory covers
the not-found case (`02-not-found.json`, `result: -1`), and the case-insensitive match is proven
at the unit level by `IndexOfTests.IndexOf_CaseInsensitive_LowercaseMatchesUppercase` in
`TLio.Functions.Tests/FunctionsTests/TextTests/IndexOfTests.cs` — `"world"` still finds
`"Hello World"` at index 6.

## When to use

- You need the numeric position of a substring, not just whether it exists.
- You want to use the position as input to a subsequent `substring` call (e.g., split on first occurrence of a delimiter).
- You need to compute lengths or offsets relative to a found character.

## When NOT to use

- You only need a boolean answer — use `contains` instead. Using `indexOf` and comparing to -1 is noisier and requires an extra step.
- You only need to check the prefix — use `startsWith` instead.
- You only need to check the suffix — use `endsWith` instead.
- You need a case-*sensitive* search — `indexOf` (like `contains`) already ignores case, and
  there is no built-in switch to make it sensitive; compare an extracted `substring` with `equals`
  instead.
- The input may be null — `indexOf` will fail on a null source; guard with `isEmpty` first.

## Comparison

| Function | Returns | Use when |
|----------|---------|----------|
| `indexOf` | integer (position or -1) | Exact position of substring is needed |
| `contains` | boolean | Only need to know if substring exists |
| `startsWith` | boolean | Only need to check the beginning |
| `endsWith` | boolean | Only need to check the end |

## Common mistakes

- **Assuming it is case-sensitive.** It is not: `indexOf` matches ordinally *ignoring case*, so
  `indexOf($.str,'world')` finds `"Hello World"` at position 6, not -1. If a case-sensitive match
  is what you actually need, there is no built-in for it — compare the raw text yourself (e.g.
  `equals(substring(...), ...)`).
- **Treating result as boolean**: a return value of `0` means the substring was found at position 0 (the start) — it does NOT mean "not found". Only `-1` means not found.
- **Null input**: null source argument fails. Guard with `isEmpty` first.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].text` as argument grabs all values as a flat list. Use indexed paths for per-element operations.
- **Only first occurrence**: `indexOf` returns the index of the FIRST match only. There is no `lastIndexOf` variant; if you need the last occurrence you must combine `indexOf`, `length`, and `substring`.
