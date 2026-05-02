# modulo

> Returns the remainder of dividing dividend by divisor.

## Syntax

```
=modulo(<dividend>, <divisor>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The dividend. |
| 2 | number or path | yes | The divisor (must not be zero). |

## Returns

A numeric node equal to `dividend % divisor`.

## Example

```json
{ "command": "set", "path": "$.rem", "value": "=modulo($.n,$.d)" }
```

Input: `{ "n": 10, "d": 3, "rem": 0 }`
Output: `{ ..., "rem": 1 }`
