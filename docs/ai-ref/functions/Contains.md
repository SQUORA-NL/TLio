# contains

> Returns true if a string contains the specified substring.

## Syntax

```
=contains(<source>, <substring>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to search in. |
| 2 | string or path | yes | The substring to look for. |

## Returns

A boolean node.

## Example

```json
{ "command": "set", "path": "$.has", "value": "=contains($.url,$.keyword)" }
```

Input: `{ "url": "https://api.example.com", "keyword": "example", "has": null }`
Output: `{ ..., "has": true }`

## When to use

- You need to know whether a substring appears anywhere in a string (not just at the start or end).
- You want to store a boolean flag based on whether a field contains a given token.
- You are checking for presence before performing a conditional transformation downstream.

## When NOT to use

- You only need to check the start of the string — use `startsWith` instead (clearer intent).
- You only need to check the end of the string — use `endsWith` instead.
- You need to know the position of the substring — use `indexOf` instead.
- The input may be null — `contains` will fail on a null source; use `isEmpty` first to guard null values if needed.
- You need a case-insensitive check — there is no built-in flag; pipe through `=toLower($.field)` first: `=contains(=toLower($.field),'keyword')` is not valid inline syntax; instead set a normalised field first, then call `contains` on it.

## Comparison

| Function | Returns | Use when |
|----------|---------|----------|
| `contains` | boolean | Substring appears anywhere |
| `startsWith` | boolean | String begins with prefix |
| `endsWith` | boolean | String ends with suffix |
| `indexOf` | integer (-1 if missing) | Position of substring matters |
| `isEmpty` | boolean | Need null-safe blank check |

## Common mistakes

- **Case sensitivity**: `contains` is case-sensitive. `contains($.email,'Gmail')` will NOT match `"gmail.com"`. Normalise with `toLower` in a prior step.
- **Null input**: passing a null path as the first argument will fail. Guard with `isEmpty` if the field may be absent.
- **Path resolution**: arguments resolve against the document root (dataContext), not the current node. `@.field` inside a function refers to the ROOT, not a parent element.
- **Using as a script-level condition**: `contains` produces a boolean value node. It is set as a VALUE in a `set`/`add` command — it is not a filter or branching condition in the script itself.
- **Wildcard paths inside functions**: `$.items[*].name` passed to `contains` grabs all values as a flat list; the function will not iterate per-element. Use indexed paths like `$.items[0].name` for element-level operations.
