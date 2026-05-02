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
