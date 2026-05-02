# isEmpty

> Returns true if a string is null, empty, or whitespace-only.

## Syntax

```
=isEmpty(<source>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The value to check. |

## Returns

A boolean node: `true` when the string is null/empty/blank, `false` otherwise.

## Example

```json
{ "command": "set", "path": "$.missing", "value": "=isEmpty($.field)" }
```

Input: `{ "field": "", "missing": null }`
Output: `{ ..., "missing": true }`

## When to use

- You need a null-safe blank check — `isEmpty` handles null, empty string `""`, and whitespace-only strings like `"   "` in a single call.
- You want to set a flag that guards later steps from operating on absent or blank values.
- You are validating required fields before applying transformations.
- Replacing a manual multi-condition check: `isEmpty` is simpler and safer than checking null and length separately.

## When NOT to use

- You only need to check a specific prefix or suffix — use `startsWith` / `endsWith` (but guard them with `isEmpty` first if the field may be null).
- You need to check for a specific substring — use `contains` (but guard with `isEmpty` first if needed).
- You need the length of the string — use `length` (returns 0 for null/empty without throwing).

## Comparison

| Function | Null-safe | Whitespace-safe | Returns |
|----------|-----------|-----------------|---------|
| `isEmpty` | yes | yes | boolean |
| `contains` | no — fails on null | no | boolean |
| `startsWith` | no — fails on null | no | boolean |
| `endsWith` | no — fails on null | no | boolean |
| `length` | yes (returns 0) | no | integer |

## Common mistakes

- **Expecting isEmpty to return false for whitespace**: `isEmpty("   ")` returns `true`. If you want `false` for whitespace-only strings (i.e., treat whitespace as content), use `length` after `trim` instead.
- **Confusing isEmpty with a length-zero check**: `isEmpty` also returns `true` for whitespace-only strings — it is NOT equivalent to `length(x) == 0` for strings that contain only spaces.
- **Using isEmpty to check numeric fields**: `isEmpty` operates on the string representation. For numeric zero-checks, compare the value directly.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].name` as argument grabs all values as a flat list. Use indexed paths for per-element operations.
