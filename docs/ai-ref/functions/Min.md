# min

> Returns the minimum numeric value from an array at the given path.

## Syntax

```
=min(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | numeric array or path | yes | Path to a collection of numbers. |

## Returns

A numeric node equal to the smallest value in the array.

## Example

```json
{ "command": "set", "path": "$.lowest", "value": "=min($.temps)" }
```

Input: `{ "temps": [15, 22, 8, 31], "lowest": 0 }`
Output: `{ "temps": [...], "lowest": 8 }`
