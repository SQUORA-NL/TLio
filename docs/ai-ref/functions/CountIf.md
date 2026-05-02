# countif

> Counts elements in a range that match a given criteria.

## Syntax

```
=countif(<range>, <criteria>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the array to evaluate. |
| 2 | string literal | yes | The value to match (single-quoted: `'active'`). |

## Returns

A long node with the count of matching elements.

## Example

```json
{ "command": "set", "path": "$.active_count", "value": "=countif($.status,'active')" }
```

Input: `{ "status": ["active","inactive","active"], "active_count": 0 }`
Output: `{ ..., "active_count": 2 }`
