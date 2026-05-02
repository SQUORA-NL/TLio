# median

> Returns the median (middle value) of a numeric array.

## Syntax

```
=median(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | numeric array or path | yes | Path to the array of numbers. |

## Returns

A numeric node equal to the median value.

## Example

```json
{ "command": "set", "path": "$.mid", "value": "=median($.values)" }
```

Input: `{ "values": [3, 1, 4, 1, 5], "mid": 0 }`
Output: `{ ..., "mid": 3 }`
