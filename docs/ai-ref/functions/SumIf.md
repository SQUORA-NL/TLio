# sumif

> Sums values in sum_range where the corresponding criteria_range value matches criteria.

## Syntax

```
=sumif(<criteria_range>, <criteria>, <sum_range>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the criteria array (parallel to sum_range). |
| 2 | string literal | yes | The value to match (single-quoted: `'paid'`). |
| 3 | array path | yes | Path to the numeric array to sum when criteria matches. |

## Returns

A numeric node with the conditional sum.

## Example

```json
{ "command": "set", "path": "$.paid_total", "value": "=sumif($.status,'paid',$.amounts)" }
```

Input: `{ "status": ["paid","pending","paid"], "amounts": [100, 200, 150], "paid_total": 0 }`
Output: `{ ..., "paid_total": 250 }`

## Notes

- The criteria and sum arrays must be parallel (same length, same index alignment).
- Criteria string literals must be wrapped in single quotes inside the function expression.
