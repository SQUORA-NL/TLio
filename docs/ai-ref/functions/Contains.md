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
