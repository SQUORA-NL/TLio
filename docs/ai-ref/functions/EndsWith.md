# endsWith

> Returns true if a string ends with the given suffix. **Case-insensitive.**

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

A boolean node. The comparison is `StringComparison.OrdinalIgnoreCase`, so `endsWith($.file,'.JSON')`
DOES match `"data.json"`.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=endswith($.str, $.suffix)" }
```

Input: `{ "str": "Hello World", "suffix": "World", "no": "XYZ" }`
Output: `{ "str": "Hello World", "suffix": "World", "no": "XYZ", "result": true }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/endswith/01-match.json` (and
`02-no-match.json`, `=endswith($.str, $.no)` → `false`).

## When to use

- You need to validate a file extension, a domain suffix, or any string that must end with a known value, regardless of case.
- You want to store a boolean flag indicating a well-formed suffix for later use.
- Suffix check is semantically clearer here than `contains` even though both could detect the same suffix.

## When NOT to use

- The substring can appear anywhere — use `contains` instead.
- The string must start with a known prefix — use `startsWith` instead.
- You need the position of the match — use `indexOf` instead.
- The input may be null — `endsWith` will fail on a null source; guard with `isEmpty` first.
- You need a **case-sensitive** suffix check — `endsWith` cannot do this; there is no built-in flag.

## Comparison

| Function | Returns | Case | Use when |
|----------|---------|------|----------|
| `endsWith` | boolean | insensitive | String must end with a specific suffix |
| `startsWith` | boolean | insensitive | String must begin with a specific prefix |
| `contains` | boolean | insensitive | Substring can appear anywhere |
| `indexOf` | integer | insensitive | Position of the substring is needed |

## Common mistakes

- **Assuming it's case-sensitive**: `endsWith($.file,'.JSON')` DOES match `"data.json"` — the
  comparison is `OrdinalIgnoreCase`. Don't add a redundant `toLower` step expecting otherwise.
- **Null input**: null source argument fails. Use `isEmpty` as a guard before calling `endsWith` on optional fields.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths inside functions**: `$.files[*].name` passed as argument grabs all values as a flat list; `endsWith` will not iterate per-element. Use indexed paths.
- **Checking prefix by mistake**: to check `"https"` at the start, use `startsWith`, not `endsWith`.
