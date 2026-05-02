# max

> Returns the maximum numeric value from an array at the given path.

## Syntax

```
=max(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | numeric array or path | yes | Path to a collection of numbers. |

## Returns

A numeric node equal to the largest value in the array.

## Example

```json
{ "command": "set", "path": "$.highest", "value": "=max($.temps)" }
```

Input: `{ "temps": [15, 22, 8, 31], "highest": 0 }`
Output: `{ "temps": [...], "highest": 31 }`
