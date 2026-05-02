# sqrt

> Returns the square root of a numeric value.

## Syntax

```
=sqrt(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | Non-negative number to take the root of. |

## Returns

A double node equal to `Math.Sqrt(value)`.

## Example

```json
{ "command": "set", "path": "$.sq", "value": "=sqrt($.n)" }
```

Input: `{ "n": 16, "sq": 0 }`
Output: `{ "n": 16, "sq": 4 }`
