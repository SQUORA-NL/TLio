# sum

> Sums all numeric values from one or more paths (arrays are flattened).

## Syntax

```
=sum(<path1> [, <path2>, ...])
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1+ | numeric array or path | yes | One or more paths to numeric values. Arrays are flattened. |

## Returns

A numeric node (long when whole, double when fractional) equal to the sum of all resolved values.

**Failure**: returns failure (not 0) when the path does not exist. Check that the path selects at least one node.

## Example

```json
{ "command": "set", "path": "$.total", "value": "=sum($.prices)" }
```

Input: `{ "prices": [9.99, 4.99, 14.99], "total": 0 }`
Output: `{ "prices": [...], "total": 29.97 }`

## Notes

- Path not found → command **fails** with an error trace entry
- Null elements in the array are treated as 0
