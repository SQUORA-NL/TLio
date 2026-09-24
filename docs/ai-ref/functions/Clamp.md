# clamp

> Bounds a numeric value to an inclusive range: below the low bound it returns the low bound, above the high bound it returns the high bound.

## Syntax

```
=clamp(<value>, <low>, <high>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to bound. |
| 2 | number or path | yes | The inclusive lower bound. |
| 3 | number or path | yes | The inclusive upper bound. Must be greater than or equal to `low`. |

## Returns

A numeric node: `value` when it lies inside `[low, high]`, otherwise the bound it crossed. Whole results come out as an integer.

## Verified example

```json
{ "command": "put", "path": "$.cappedFactor",
  "value": "=clamp($.bonusMalusFactor, $.low, $.high)" }
```

Input: `{ "bonusMalusFactor": 1.9, "low": 0.5, "high": 1.5 }`
Output: `{ "bonusMalusFactor": 1.9, "low": 0.5, "high": 1.5, "cappedFactor": 1.5 }`

Verified by: `TLio.Functions.Tests/Fixtures/Math/clamp/03-above-high.json`

The bounds are inclusive — a value equal to `high` passes through unchanged, it is not treated
as "above":

```json
{ "command": "put", "path": "$.result", "value": "=clamp($.v, $.low, $.high)" }
```

Input: `{ "v": 5, "low": 1, "high": 5 }` → Output: `{ ..., "result": 5 }`

Verified by: `TLio.Functions.Tests/Fixtures/Math/clamp/04-bound-is-inclusive.json`

## When to use

- **Capping a rating factor** to the band its rate table allows — the case this function exists for.
- **Bounding a score or percentage** into `0..100` after a calculation that can overshoot.
- **Keeping a computed index inside an array's range**.
- Anywhere you would otherwise write `=min(=max(v,lo),hi)` — `clamp` is that expression, one level instead of two, and it checks the bounds make sense.

## When NOT to use

- You want to **know whether** the value was inside the range rather than bound it — that is a predicate. Use `between`: `=between($.v,$.low,$.high)`.
- You want a **failure** when the value is out of range — `clamp` silently substitutes the bound. Validate first if an out-of-range value is an error rather than something to cap.
- You are bounding **dates** — `clamp` is numeric. Use `minDate` / `maxDate` and `isDateBetween`.
- Only **one** side needs bounding — `=min($.v,$.cap)` or `=max($.v,$.floor)` says so more plainly than a clamp with an invented infinite bound.

## Comparison

| Function | Arity | Bounds | `clamp(9, 1, 5)` equivalent |
|----------|-------|--------|------------------------------|
| `clamp` | exactly 3 | both, inclusive | `5` |
| `min` | variadic | upper only (smallest of the arguments) | `=min(9,5)` → `5` |
| `max` | variadic | lower only (largest of the arguments) | `=max(9,1)` → `9` |
| `min` + `max` nested | 2 calls | both | `=min(=max(9,1),5)` → `5` |
| `between` | exactly 3 | both, inclusive — tests, does not change | `false` |

## Common mistakes

- **`low` above `high` fails the script.** `=clamp($.v,$.high,$.low)` — arguments in the wrong order — logs an error and fails rather than swapping the bounds. An inverted band is a broken rate table, and quietly picking one bound would hide it. The order is always `value, low, high`.
- **Both bounds are inclusive.** `clamp(5, 1, 5)` is `5`. There is no exclusive variant.
- **Out of range is not an error.** A value far outside the band produces the bound with no warning in the result — only the log records that anything happened. If you need to know it was capped, compare before and after.
- **A `null` value clamps as 0.** Found-but-null is 0 in the Math pack, so `=clamp($.nullField,1,5)` returns `1`, not `null`. A path that does not resolve at all fails instead.
- **`clamp` takes one value, not an array.** Each of the three arguments must resolve to a single scalar; it does not bound a collection element-wise.
- **Wrong path scope** — path arguments resolve against the document root, not the current node.
