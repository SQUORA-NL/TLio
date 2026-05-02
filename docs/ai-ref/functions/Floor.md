# floor

> Returns the largest integer less than or equal to the given value.

## Syntax

```
=floor(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to floor. |

## Returns

A long node equal to `Math.Floor(value)`.

## Example

```json
{ "command": "set", "path": "$.f", "value": "=floor($.v)" }
```

Input: `{ "v": 7.9, "f": 0 }`
Output: `{ "v": 7.9, "f": 7 }`
