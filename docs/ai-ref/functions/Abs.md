# abs

> Returns the absolute value of a number.

## Syntax

```
=abs(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to take the absolute of. |

## Returns

A numeric node equal to `Math.Abs(value)`.

## Example

```json
{ "command": "set", "path": "$.ab", "value": "=abs($.neg)" }
```

Input: `{ "neg": -5, "ab": 0 }`
Output: `{ "neg": -5, "ab": 5 }`
