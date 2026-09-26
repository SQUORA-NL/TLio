# dayCountFraction

> The fraction of a year between two dates under a named day-count convention — the year-fraction
> that interest-accrual math (bonds, loans, swaps) multiplies a rate by.

## Syntax

```
=dayCountFraction(<from>, <to>, <convention>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The start of the accrual period. |
| 2 | date string or path | yes | The end of the accrual period. |
| 3 | string | yes | `A360`, `A365`, or `30E360`. Case-insensitive. |

## Returns

A `double` — the year fraction, negative when `to` precedes `from`.

- `A360` — actual calendar days elapsed, divided by 360 (the money-market convention).
- `A365` — actual calendar days elapsed, divided by 365.
- `30E360` — 30/360 European: each month treated as exactly 30 days (day-of-month capped at 30
  on both ends), divided by 360. `360*(Y2-Y1) + 30*(M2-M1) + (D2-D1)`.

Only these three are supported. More exist in the wider day-count-convention family (ISDA
actual/actual, plain 30/360, business-day/end-of-month-adjusted variants) and are not — an
unrecognised convention fails the function rather than guessing.

## Verified example

```json
{ "command": "put", "path": "$.accrued",
  "value": "=dayCountFraction($.periodStart,$.periodEnd,'A360')" }
```

Input: `{ "periodStart": "2025-01-01", "periodEnd": "2026-01-01" }`
Output: `{ ..., "accrued": 1.0138888888888888 }` — 365 actual days / 360.

Verified by: `TLio.Functions.Tests/FunctionsTests/TimeDateTests/DayCountFractionTests.cs`

## When to use

- Computing an interest payment for a period: `payoff = principal * rate * dayCountFraction(...)`.
- Any fixed-income or loan schedule where a period's interest depends on its actual length, not
  a flat per-period assumption.

## When NOT to use

- You need a whole-unit count (days, months, years) rather than a year fraction — use `dateDiff`.
- You need a convention not in the supported list — this function fails loudly rather than
  approximating; compute it a different way (or extend this pack) instead of guessing.

## Common mistakes

- **Reversing the arguments.** Like `dateDiff`, it reads "from A to B" — swapping gives a
  negative fraction.
- **Expecting month-count conventions (30/360, 30E/360 ISDA) to match `30E360` exactly.** They
  differ in how they treat month-end dates; `30E360` is specifically the European variant.
- **Passing a non-ISO date string.** Normalise first with `parseDate` if the input isn't already
  `yyyy-MM-dd` or one of the other formats `dateDiff`/`dateAdd` accept.
