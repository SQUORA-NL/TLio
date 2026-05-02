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

## When to use

- You need to compute **base^exponent** — compound interest, exponential scaling, area/volume from a side length.
- You need the **square root** via `=pow($.x, 0.5)` (equivalent to `sqrt`).
- The exponent is fractional or negative — `pow` handles all real exponent values.

## When NOT to use

- You only need a **square root** with a path argument — `sqrt` is more readable.
- You need **modular arithmetic** (remainder after division) — use `modulo`.
- Either argument might not exist — `pow` fails if either path does not resolve.

## Common mistakes

- **Confusing base and exponent order** — argument 1 is the base, argument 2 is the exponent. `=pow($.exp, $.base)` computes the wrong thing.
- **Negative base with fractional exponent** — `pow(-8, 0.333...)` does not produce -2 in floating-point; it produces `NaN`. Only use fractional exponents on non-negative bases.
- **Path not found = failure** — if either path does not resolve, the command fails.
- **Wrong path scope** — both path arguments resolve against the document root.
