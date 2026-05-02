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

## When to use

- You need the **remainder** after integer division — `10 mod 3 = 1`.
- **Even/odd detection** — `=modulo($.n, 2)` returns 0 for even, 1 for odd.
- **Cyclic indexing** — wrapping an index around a fixed period (e.g., day of week, slot in a ring buffer).
- **Grouping into buckets** — assign item index mod N to a bucket.

## When NOT to use

- You need the **quotient** (integer division result), not the remainder — compute `floor(dividend / divisor)` instead.
- The divisor might be **zero** — `modulo` with a zero divisor causes failure. Always guard against zero divisors.
- You need **percentage** — use division and multiplication, not modulo.

## Common mistakes

- **Zero divisor causes failure** — `=modulo($.n, 0)` fails. Validate that the divisor is non-zero before calling.
- **Sign of the result** — the sign of the remainder follows the dividend in most implementations: `-10 mod 3 = -1`. If you need a positive remainder, wrap with `abs`.
- **Confusing remainder with quotient** — `modulo(10, 3)` is `1` (the leftover), not `3` (how many times 3 fits). Use floor division for the quotient.
- **Path not found = failure** — if either path does not resolve, the command fails.
- **Wrong path scope** — both path arguments resolve against the document root.
