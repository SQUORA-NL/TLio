# startsWith

> Returns true if a string starts with the given prefix. **Case-insensitive.**

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

A boolean node. The comparison is `StringComparison.OrdinalIgnoreCase`, so `startsWith($.url,'HTTP')`
DOES match `"https://..."`. An empty-string prefix always returns `true` (every string "starts with" `""`).

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=startswith($.str, $.prefix)" }
```

Input: `{ "str": "Hello World", "prefix": "Hello", "no": "XYZ" }`
Output: `{ "str": "Hello World", "prefix": "Hello", "no": "XYZ", "result": true }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/startswith/01-match.json` (and
`02-no-match.json`, `=startswith($.str, $.no)` → `false`).

The empty-prefix edge case is covered by the inline test
`PredicateTests.StartsWith_EmptyNeedle_ReturnsTrue`.

## When to use

- You need to verify a URL scheme, a path prefix, a code prefix, or any string that must begin with a known value, regardless of case.
- You want to write a boolean flag that signals a well-formed prefix for later conditional processing.
- Prefix check is semantically clearer here than `contains` even though both could detect the same prefix.

## When NOT to use

- The substring can appear anywhere — use `contains` instead.
- The string must end with a known suffix — use `endsWith` instead.
- You need the position of the match — use `indexOf` instead.
- The input may be null — `startsWith` will fail on a null source; guard with `isEmpty` first.
- You need a **case-sensitive** prefix check — `startsWith` cannot do this; there is no built-in flag.

## Comparison

| Function | Returns | Case | Use when |
|----------|---------|------|----------|
| `startsWith` | boolean | insensitive | String must begin with a specific prefix |
| `endsWith` | boolean | insensitive | String must end with a specific suffix |
| `contains` | boolean | insensitive | Substring can appear anywhere |
| `indexOf` | integer | insensitive | Position of the substring is needed |

## Common mistakes

- **Assuming it's case-sensitive**: `startsWith($.url,'HTTP')` DOES match `"https://..."` — the
  comparison is `OrdinalIgnoreCase`. Don't add a redundant `toLower` step expecting otherwise.
- **Null input**: null source argument fails. Use `isEmpty` as a guard before calling `startsWith` on optional fields.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths inside functions**: `$.items[*].url` passed as argument grabs all values as a flat list; `startsWith` will not iterate per-element. Use indexed paths.
- **Checking suffix by mistake**: to check `".json"` at the end, use `endsWith`, not `startsWith`.
