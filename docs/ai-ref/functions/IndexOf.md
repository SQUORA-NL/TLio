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

A long node with the index, or -1 if not found.

## Example

```json
{ "command": "set", "path": "$.pos", "value": "=indexOf($.str,$.sub)" }
```

Input: `{ "str": "Hello World", "sub": "World", "pos": 0 }`
Output: `{ ..., "pos": 6 }`

## When to use

- You need the numeric position of a substring, not just whether it exists.
- You want to use the position as input to a subsequent `substring` call (e.g., split on first occurrence of a delimiter).
- You need to compute lengths or offsets relative to a found character.

## When NOT to use

- You only need a boolean answer — use `contains` instead. Using `indexOf` and comparing to -1 is noisier and requires an extra step.
- You only need to check the prefix — use `startsWith` instead.
- You only need to check the suffix — use `endsWith` instead.
- You need case-insensitive search — normalise with `toLower` in a prior step.
- The input may be null — `indexOf` will fail on a null source; guard with `isEmpty` first.

## Comparison

| Function | Returns | Use when |
|----------|---------|----------|
| `indexOf` | integer (position or -1) | Exact position of substring is needed |
| `contains` | boolean | Only need to know if substring exists |
| `startsWith` | boolean | Only need to check the beginning |
| `endsWith` | boolean | Only need to check the end |

## Common mistakes

- **Case sensitivity**: `indexOf` is case-sensitive. `indexOf($.str,'world')` returns -1 for `"Hello World"`. Normalise first.
- **Treating result as boolean**: a return value of `0` means the substring was found at position 0 (the start) — it does NOT mean "not found". Only `-1` means not found.
- **Null input**: null source argument fails. Guard with `isEmpty` first.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].text` as argument grabs all values as a flat list. Use indexed paths for per-element operations.
- **Only first occurrence**: `indexOf` returns the index of the FIRST match only. There is no `lastIndexOf` variant; if you need the last occurrence you must combine `indexOf`, `length`, and `substring`.
