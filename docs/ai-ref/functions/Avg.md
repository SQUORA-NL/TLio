# avg

> Computes the arithmetic mean of all numeric values at the given path.

## Syntax

```
=avg(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | numeric array or path | yes | Path to an array of numbers. |

## Returns

A double node representing the average.

## Example

```json
{ "command": "set", "path": "$.average", "value": "=avg($.scores)" }
```

Input: `{ "scores": [80, 90, 100], "average": 0 }`
Output: `{ "scores": [...], "average": 90.0 }`

## Notes

- Path not found → command **fails**
- Null values in the array are treated as 0
