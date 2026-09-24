# dateAdd

> Shifts a date by a whole number of units, clamping at month ends the way a calendar does.

## Syntax

```
=dateAdd(<date>, <amount>)
=dateAdd(<date>, <amount>, <unit>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The date to shift. |
| 2 | integer or path | yes | How many units. Negative subtracts. |
| 3 | string | no | `year(s)`, `month(s)`, `week(s)`, `day(s)`, `hour(s)`, `minute(s)`, `second(s)`. Case-insensitive, singular or plural. Default `days`. |

## Returns

A date string in TLio's canonical form: `yyyy-MM-dd` when the result falls on midnight UTC, `yyyy-MM-ddTHH:mm:ssZ` otherwise. A date-only input therefore stays date-only unless the shift introduces a time.

**Month-end clamps**, exactly as .NET's `AddMonths` / `AddYears` do:

| Expression | Result |
|---|---|
| `=dateAdd('2024-01-31',1,'months')` | `2024-02-29` |
| `=dateAdd('2023-01-31',1,'months')` | `2023-02-28` |
| `=dateAdd('2024-02-29',1,'years')` | `2025-02-28` |
| `=dateAdd('2024-02-29',4,'years')` | `2028-02-29` |

## Verified example

```json
{ "command": "put", "path": "$.new",
  "value": "=dateadd($.request.requestedStartDate,1,'years')" }
```

Input: `{ "request": { "requestedStartDate": "2026-09-01" } }`
Output: `{ ..., "new": "2027-09-01" }`

The fixture also runs the old `concat(sum(substring(...,0,4),1),substring(...,4,6))` string-surgery
idiom side by side (into `$.old`) and asserts both give `"2027-09-01"`.

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/dateadd/08-sample-renewal-date-matches-the-old-idiom.json`
(month-end clamping, units, and negative amounts verified by `.../01-years.json` through
`.../07-minutes-keep-the-time.json` in the same directory)

## When to use

- Renewal, expiry and term dates: start plus one year, start plus twelve months.
- Grace periods and deadlines: `=dateAdd($.invoice.date,30,'days')`.
- Backdating: `=dateAdd($.quotedOn,-5,'years')` for a claims look-back window.
- Anywhere the amount itself comes from the document: the second argument may be a path.

## When NOT to use

- You need the distance between two dates, not a new date — use `dateDiff`.
- You need the first or last day of a month — use `startOfMonth` / `endOfMonth`, which do not depend on knowing the month's length.
- You need to render an existing date differently — use `formatDate`.

## Comparison

| Function | Input | Returns | Use when |
|----------|-------|---------|----------|
| `dateAdd` | date + amount + unit | date string | Shift a date forward or back |
| `dateDiff` | two dates + unit | long | Measure the gap between two dates |
| `startOfMonth` | one date | date string | First day of that month |
| `endOfMonth` | one date | date string | Last day of that month |
| `datetime` | (no input) | date string | Current UTC timestamp |

## Common mistakes

- **Building the date by string surgery.** `=concat(=sum(=substring($.d,0,4),1),=substring($.d,4,6))` is the idiom this replaces. It only works for ISO input, it silently breaks for a 29 February start date, and it cannot add months at all.
- **Expecting 31 January + 1 month to be 2 March.** It is the last day of February. That is the calendar convention, and it is what `dateDiff(…, 'months')` agrees with.
- **Expecting the clamp to be remembered.** `=dateAdd(=dateAdd('2024-01-31',1,'months'),1,'months')` is 2024-03-29, not 2024-03-31 — the clamp happens once, on the value you actually pass in.
- **Passing a fractional amount.** `1.9` is truncated to `1`. Convert to a smaller unit instead: `=dateAdd($.d,36,'hours')`.
- **Assuming a time appears.** Adding days to a date-only value returns a date-only value. Adding hours turns it into a timestamp.
- **Passing an unrecognised unit.** The function fails and the script aborts; the error message lists the accepted spellings.
