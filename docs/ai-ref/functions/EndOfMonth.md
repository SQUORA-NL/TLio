# endOfMonth

> The last day of the month a date falls in, as a date-only ISO string — leap-year correct.

## Syntax

```
=endOfMonth(<date>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | Any date in the month you want the end of. |

## Returns

A date-only string, `yyyy-MM-dd`. Any time component is dropped — the answer is a day, not an instant.

| Expression | Result |
|---|---|
| `=endOfMonth('2024-02-17')` | `2024-02-29` |
| `=endOfMonth('2023-02-17')` | `2023-02-28` |
| `=endOfMonth('1900-02-10')` | `1900-02-28` |
| `=endOfMonth('2000-02-10')` | `2000-02-29` |
| `=endOfMonth('2024-04-10T22:15:00Z')` | `2024-04-30` |
| `=endOfMonth('2024-12-05')` | `2024-12-31` |

## Verified example

```json
{ "command": "put", "path": "$.policy.termEnd",
  "value": "=endofmonth($.request.requestedStartDate)" }
```

Input: `{ "request": { "requestedStartDate": "2026-09-15" } }`
Output: `{ ..., "policy": { "termEnd": "2026-09-30" } }`

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/endofmonth/05-sample-term-end.json`
(leap February, common February, thirty-day month, and December verified by
`.../01-leap-february.json` through `.../04-december-does-not-roll-over.json` in the same
directory)

## When to use

- Term, billing and reporting period ends.
- The high bound of a month window, paired with `startOfMonth` and `isDateBetween`.
- Anywhere a rule says "the last day of the month" — there is no way to express it by composing the other functions, because month lengths vary and February varies by year.
- Remaining-days arithmetic: `=dateDiff($.d,=endOfMonth($.d),'days')`.

## When NOT to use

- You need the first day — use `startOfMonth`.
- You need *how many* days the month has rather than which date ends it — use `datePart(d,'daysInMonth')`.
- You need the first of the *next* month — that is `=dateAdd(=startOfMonth($.d),1,'months')`, not this plus a day.

## Comparison

| Function | Input | Returns | Use when |
|----------|-------|---------|----------|
| `endOfMonth` | one date | date string | Last day of that month |
| `startOfMonth` | one date | date string | First day of that month |
| `datePart` | date + `daysInMonth` | long | The count, 28–31 |
| `dateAdd` | date + amount + unit | date string | Any other shift |

## Common mistakes

- **Assuming February is 28 days.** It is 29 in a leap year, and the Gregorian rule is not "divisible by 4": 1900 is a common year, 2000 is a leap year. `endOfMonth` gets both right; a hard-coded `'-02-28'` does not.
- **Building it with `dateAdd`.** `=dateAdd($.d,1,'months')` then stepping back a day does *not* give the month end in general — from 2024-02-17 it gives 2024-03-16. The round trip only works if you start from `startOfMonth`.
- **Expecting a timestamp back.** `=endOfMonth('2024-04-10T22:15:00Z')` is `2024-04-30`, not the last instant of the month. For an exclusive upper bound on timestamps, use the first of the next month instead.
- **Expecting local-month semantics.** The month comes from the UTC value, so `2024-03-01T02:00:00+05:00` — 29 February UTC — gives `2024-02-29`.
- **Using it to test "is this the last day?"** Compare instead: `=equals($.d,=endOfMonth($.d))` — but note that only matches when `$.d` is date-only, since the result never carries a time.
