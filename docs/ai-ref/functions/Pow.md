# pow

> Raises a base to an exponent power.

## Syntax

```
=pow(<base>, <exponent>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The base value. |
| 2 | number or path | yes | The exponent. |

## Returns

A numeric node equal to `Math.Pow(base, exponent)`.

## Example

```json
{ "command": "set", "path": "$.pw", "value": "=pow($.base, $.exp)" }
```

Input: `{ "base": 2, "exp": 8, "pw": 0 }`
Output: `{ "base": 2, "exp": 8, "pw": 256 }`
