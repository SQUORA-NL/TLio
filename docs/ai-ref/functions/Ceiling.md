# ceiling

> Returns the smallest integer greater than or equal to the given value.

## Syntax

```
=ceiling(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to ceil. |

## Returns

A long node equal to `Math.Ceiling(value)`.

## Example

```json
{ "command": "set", "path": "$.c", "value": "=ceiling($.v)" }
```

Input: `{ "v": 7.1, "c": 0 }`
Output: `{ "v": 7.1, "c": 8 }`
