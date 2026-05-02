# startsWith

> Returns true if a string starts with the given prefix.

## Syntax

```
=startsWith(<source>, <prefix>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to check. |
| 2 | string or path | yes | The prefix to test. |

## Returns

A boolean node.

## Example

```json
{ "command": "set", "path": "$.ok", "value": "=startsWith($.url,'https')" }
```

Input: `{ "url": "https://api.example.com", "ok": null }`
Output: `{ ..., "ok": true }`

## When to use

- You need to verify a URL scheme, a path prefix, a code prefix, or any string that must begin with a known value.
- You want to write a boolean flag that signals a well-formed prefix for later conditional processing.
- Prefix check is semantically clearer here than `contains` even though both could detect the same prefix.

## When NOT to use

- The substring can appear anywhere — use `contains` instead.
- The string must end with a known suffix — use `endsWith` instead.
- You need the position of the match — use `indexOf` instead.
- The input may be null — `startsWith` will fail on a null source; guard with `isEmpty` first.
- You need case-insensitive matching — normalise with `toLower` in a prior step first.

## Comparison

| Function | Returns | Use when |
|----------|---------|----------|
| `startsWith` | boolean | String must begin with a specific prefix |
| `endsWith` | boolean | String must end with a specific suffix |
| `contains` | boolean | Substring can appear anywhere |
| `indexOf` | integer | Position of the substring is needed |

## Common mistakes

- **Case sensitivity**: `startsWith($.url,'HTTP')` will NOT match `"https://..."`. Normalise first.
- **Null input**: null source argument fails. Use `isEmpty` as a guard before calling `startsWith` on optional fields.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths inside functions**: `$.items[*].url` passed as argument grabs all values as a flat list; `startsWith` will not iterate per-element. Use indexed paths.
- **Checking suffix by mistake**: to check `".json"` at the end, use `endsWith`, not `startsWith`.
