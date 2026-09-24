# dateDiff

> Whole units elapsed from one date to another — calendar-aware for years and months, so `years` means birthdays passed.

## Syntax

```
=dateDiff(<from>, <to>)
=dateDiff(<from>, <to>, <unit>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The start of the interval. |
| 2 | date string or path | yes | The end of the interval. |
| 3 | string | no | `year(s)`, `month(s)`, `week(s)`, `day(s)`, `hour(s)`, `minute(s)`, `second(s)`. Case-insensitive, singular or plural. Default `days`. |

## Returns

A `long` — whole units, **truncated toward zero**, negative when `to` precedes `from`.

- `years` and `months` are **calendar-aware**, not `days / 365.25`. 1985-03-12 → 2026-03-11 is `40` years; → 2026-03-12 is `41`. Months work the same way: 491 and 492.
- `weeks` is the elapsed day count divided by 7, truncated.
- `days`, `hours`, `minutes`, `seconds` come from the elapsed interval, truncated.
- A 29 February date has no anniversary in a common year; month-end clamping gives it 28 February, the same day `dateAdd(d, 1, 'years')` produces.

## Verified example

```json
{ "command": "put", "path": "$.new",
  "value": "=datediff($.request.applicant.birthDate,$.request.quotedOn,'years')" }
```

Input: `{ "request": { "quotedOn": "2026-08-21", "applicant": { "birthDate": "1991-11-04" } } }`
Output: `{ ..., "new": 34 }`

The birthday has not come round yet in August, so the answer is 34, not 35. The fixture also
runs the old `floor(calculate(concat(...)))` YYYYMMDD-subtraction idiom side by side (into
`$.old`) and asserts both give `34` — proof that `dateDiff` is a drop-in replacement for it.

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/datediff/08-sample-driver-age-matches-the-old-idiom.json`
(unit variants verified by `.../01-years-day-of-the-anniversary.json` through
`.../07-hours-truncate-toward-zero.json` in the same directory)

## When to use

- Any age: driver age, licence years, vehicle age, days since a claim.
- Band and threshold rules — feed the result straight into `decisionTable`, `between` or `lessThan`.
- Ageing a queue or an outstanding balance in `days`, `hours` or `minutes`.
- Working out how many whole months a policy has run.

## When NOT to use

- You only need to know which of two dates is earlier — use `dateCompare`, which returns -1/0/1.
- You need a date back rather than a count — use `dateAdd`.
- You need one component of a single date (its year, its quarter) — use `datePart`.
- You need the sign discarded — wrap it: `=abs(=dateDiff($.a,$.b,'days'))`.

## Comparison

| Function | Input | Returns | Use when |
|----------|-------|---------|----------|
| `dateDiff` | two dates + unit | long | How far apart are they? |
| `dateCompare` | two dates | long (-1/0/1) | Which one is earlier? |
| `isDateBetween` | date + from + to | boolean | Is it inside a window? |
| `dateAdd` | date + amount + unit | date string | What is the date N units later? |
| `datePart` | one date + part | long | What is this date's year / quarter / week? |

## Common mistakes

- **Reaching for the YYYYMMDD trick.** `=floor(=calculate(=concat('(',=replace($.to,'-',''),'-',=replace($.from,'-',''),')/10000')))` is what this function replaces. It only ever gives the right answer for whole years, and gives nonsense for months — `dateDiff` is both shorter and correct.
- **Reversing the arguments.** It reads "from A to B": the birth date goes first, the reference date second. Swapping them gives a negative age.
- **Expecting rounding.** The result is truncated, never rounded. An interval of 11 months 29 days is `0` years, not `1`.
- **Expecting `days/365.25` for years.** It is not. `dateDiff` counts anniversaries, so the answer changes on a birthday and nowhere else.
- **Assuming .NET's `DayOfWeek`-style off-by-one applies here.** It does not; that trap lives in `datePart`.
- **Passing an unrecognised unit.** `'yrs'`, `'fortnights'` and `'mo'` all fail the function, which aborts the script. The error message lists the accepted spellings.
- **Feeding it a non-ISO string.** A `31-12-2026` input parses, but anything outside the supported format list fails. Normalise first with `parseDate`.
