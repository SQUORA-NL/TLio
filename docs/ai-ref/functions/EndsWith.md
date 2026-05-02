# endsWith

> Returns true if a string ends with the given suffix.

## Syntax

```
=endsWith(<source>, <suffix>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to check. |
| 2 | string or path | yes | The suffix to test. |

## Returns

A boolean node.

## Example

```json
{ "command": "set", "path": "$.ok", "value": "=endsWith($.file,'.json')" }
```

Input: `{ "file": "data.json", "ok": null }`
Output: `{ ..., "ok": true }`

## When to use

- You need to validate a file extension, a domain suffix, or any string that must end with a known value.
- You want to store a boolean flag indicating a well-formed suffix for later use.
- Suffix check is semantically clearer here than `contains` even though both could detect the same suffix.

## When NOT to use

- The substring can appear anywhere — use `contains` instead.
- The string must start with a known prefix — use `startsWith` instead.
- You need the position of the match — use `indexOf` instead.
- The input may be null — `endsWith` will fail on a null source; guard with `isEmpty` first.
- You need case-insensitive matching — normalise with `toLower` in a prior step first.

## Comparison

| Function | Returns | Use when |
|----------|---------|----------|
| `endsWith` | boolean | String must end with a specific suffix |
| `startsWith` | boolean | String must begin with a specific prefix |
| `contains` | boolean | Substring can appear anywhere |
| `indexOf` | integer | Position of the substring is needed |

## Common mistakes

- **Case sensitivity**: `endsWith($.file,'.JSON')` will NOT match `"data.json"`. Normalise first.
- **Null input**: null source argument fails. Use `isEmpty` as a guard before calling `endsWith` on optional fields.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths inside functions**: `$.files[*].name` passed as argument grabs all values as a flat list; `endsWith` will not iterate per-element. Use indexed paths.
- **Checking prefix by mistake**: to check `"https"` at the start, use `startsWith`, not `endsWith`.
