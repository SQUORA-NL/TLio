# between

> Returns true when a value falls inside a range, inclusive on both bounds.

## Syntax

```
=between(<value>, <low>, <high>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number, text or path | yes | The value to test. |
| 2 | number, text or path | yes | Lower bound — **included** in the range. |
| 3 | number, text or path | yes | Upper bound — **included** in the range. |

Same argument order and the same inclusivity as `isDateBetween`, so the two read alike.

## Returns

A boolean. `true` when `low <= value <= high`.

An argument path that matches nothing, a `null`, an object or an array logs a warning and returns
`false`. It does **not** fail: `between` is a core predicate, and a predicate answers rather than
aborts. This is the one place it diverges from `isDateBetween`, which lives in the TimeDate pack
and fails on an unparseable date. It matches the `greaterOrEqual` / `lessOrEqual` pair that
`between` replaces.

Comparison follows the library-wide rule: if both sides read as numbers they compare numerically
(so the text `"24"` equals `24`); otherwise they compare as ordinal text.

## Example

```json
{ "command": "add", "path": "$.inBand", "value": "=between($.age,$.band.from,$.band.to)" }
```

Input: `{ "age": 30, "band": { "from": 25, "to": 39 } }`
Output: `{ "age": 30, "band": { "from": 25, "to": 39 }, "inBand": true }`

As an `ifElse` condition — the band check every rate table needs:

```json
{ "command": "ifElse",
  "condition": "=between($.mileage,10001,15000)",
  "ifScript":   [{ "command": "put", "path": "$.factor", "value": 1.1 }],
  "elseScript": [{ "command": "put", "path": "$.factor", "value": 1.0 }] }
```

## When to use

- **Band checks in a rate table**: age bands, mileage bands, value bands, no-claim-years bands.
  This is what it was added for — it replaces `=and(=greaterOrEqual(v,lo),=lessOrEqual(v,hi))`,
  which is how every one of them is written today.
- As a condition for `ifElse`, `decisionTable`, or the `if` function.
- Validating that an input sits within an allowed range before using it.
- Range-checking text ordinally (postcode letters, sequence prefixes) — the same ordinal ordering
  `greaterThan` uses.

## When NOT to use

- **The values are dates — use `isDateBetween`.** It parses dates properly. `between` on ISO-8601
  date *strings* happens to work because ISO-8601 sorts ordinally, but any other format, or a
  mixture of formats, or a time zone, will give a wrong answer silently.
- **You need one bound only, or an exclusive bound** — use `greaterOrEqual`, `greaterThan`,
  `lessOrEqual` or `lessThan`. `between` is inclusive on both ends and offers no option to change
  that; an exclusive upper bound is `=and(=greaterOrEqual(v,lo),=lessThan(v,hi))`.
- **The candidates are a fixed set of discrete values, not a range — use `in`.**
  `=in($.status,'gold','silver')`, not a range over text.
- **Many contiguous bands each mapping to a factor** — a `decisionTable` expresses the whole
  table at once and is easier to read than a stack of `between` conditions.

## Comparison

| Function | Bounds | Value kind | Missing / unusable operand |
|----------|--------|-----------|---------------------------|
| `between(v, lo, hi)` | both inclusive | number or text | warning, returns `false` |
| `isDateBetween(d, from, to)` | both inclusive | date (parsed) | fails, aborting the script |
| `greaterOrEqual(a, b)` / `lessOrEqual(a, b)` | one, inclusive | number or text | warning, returns `false` |
| `greaterThan(a, b)` / `lessThan(a, b)` | one, exclusive | number or text | warning, returns `false` |
| `in(v, o1, o2, …)` | n/a — discrete set | any, equality | `false` |

## Common mistakes

- **Writing contiguous bands with overlapping bounds.** Both ends are inclusive, so `0–10000`
  and `10000–15000` both match `10000`. Write the second band as `10001–15000`, exactly as you
  would in the rate table itself.
- **Passing the bounds in the wrong order.** They are not sorted. `=between(21,24,18)` is
  `false`, not `true` — `low` is the second argument and `high` the third.
- **Using it on dates.** `=between($.startDate,'2024-01-01','2024-12-31')` compares text. It is
  right for ISO-8601 and wrong for `31-01-2024`, and nothing warns you. Use `isDateBetween`.
- **Expecting a failure when the value is missing.** Unlike `isDateBetween`, a path matching
  nothing gives `false` with a warning in the log. A misspelled path therefore looks like a value
  outside the band — check the log if a band never matches.
- **Assuming a two-argument form.** `=between($.age,18)` fails with a warning; there is no
  open-ended range. Use `greaterOrEqual` for that.
- **Path args resolve against the document root.** Inside the call, use `$.field`.
