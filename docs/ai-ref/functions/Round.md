# round

> Rounds a numeric value to the nearest integer (or specified decimal places).

## Syntax

```
=round(<value>)
=round(<value>, <decimals>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to round. |
| 2 | integer | no | Number of decimal places (default 0). |

## Returns

A numeric node rounded using midpoint-away-from-zero rounding.

## Example

```json
{ "command": "set", "path": "$.r", "value": "=round($.v)" }
```

Input: `{ "v": 7.6, "r": 0 }`
Output: `{ "v": 7.6, "r": 8 }`
