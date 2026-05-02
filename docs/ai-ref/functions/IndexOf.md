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
