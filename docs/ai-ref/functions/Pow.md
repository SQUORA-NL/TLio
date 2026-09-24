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

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=pow($.base, $.exp)" }
```

Input: `{ "base": 2, "exp": 3, "half": 0.5, "four": 4 }`
Output: adds `"result": 8`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/pow/01-integer-result.json`

A fractional exponent takes a root — `four ^ 0.5` is the square root of `4`:

```json
{ "command": "put", "path": "$.result", "value": "=pow($.four, $.half)" }
```

Input: `{ "base": 2, "exp": 3, "half": 0.5, "four": 4 }`
Output: adds `"result": 2`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/pow/02-fractional-exponent.json`

## When to use

- You need to compute **base^exponent** — compound interest, exponential scaling, area/volume from a side length.
- You need the **square root** via `=pow($.x, 0.5)` (equivalent to `sqrt`).
- The exponent is fractional or negative — `pow` handles real exponent values, as long as the
  result is a finite number (see Common mistakes).

## When NOT to use

- You only need a **square root** with a path argument — `sqrt` is more readable.
- You need **modular arithmetic** (remainder after division) — use `modulo`.
- Either argument might not exist — `pow` fails if either path does not resolve.

## Common mistakes

- **Confusing base and exponent order** — argument 1 is the base, argument 2 is the exponent. `=pow($.exp, $.base)` computes the wrong thing.
- **A negative base with a fractional exponent fails the function, it does not return `NaN`.**
  `pow(-8, 0.333...)` is `NaN` in floating point, and — exactly like `sqrt` — `Pow<TNode>.Execute`
  checks the result for `NaN`/`Infinity` and turns either into a failed function rather than
  writing `NaN` into the document. Only use fractional exponents on a non-negative base.
- **Path not found = failure** — if either path does not resolve, the command fails.
- **Wrong path scope** — both path arguments resolve against the document root.
