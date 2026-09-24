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

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=sqrt($.perfect)" }
```

Input: `{ "perfect": 9, "two": 2 }`
Output: adds `"result": 3`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/sqrt/01-perfect-square.json`

## When to use

- You need the **square root** of a known non-negative value — distance calculations, standard deviation steps, normalization.
- The input is guaranteed to be zero or positive.

## When NOT to use

- The input might be **negative** — `sqrt` of a negative number is a script failure (see Common
  mistakes), not a silent `NaN`. Guard against negative inputs explicitly if they are possible.
- You need a general **Nth root** — use `pow($.x, 0.5)` for square root via pow if you need consistency, or implement via `pow` for other roots.
- You need to raise a value to a power — use `pow`.

## Common mistakes

- **Negative input fails the function, it does not return `NaN`.** `Sqrt<TNode>.Execute`
  checks the result of `Math.Sqrt` for `NaN`/`Infinity` and turns either into a failed
  function with a logged error — `=sqrt(-4)` aborts the script rather than writing `NaN`
  into the document. Verified by `SqrtTests.Sqrt_Negative_ReturnsFailed`
  (`TLio.Functions.Tests/FunctionsTests/MathTests/SqrtTests.cs`).
- **Path not found = failure** — if the path does not resolve, the command fails, for the
  same reason (no numeric value to take the root of).
- **Expecting an integer result** — `sqrt(2)` is `1.414...`, a double. If you need an integer, wrap with `round`/`floor`/`ceiling`.
- **Wrong path scope** — path arguments resolve against the document root.
