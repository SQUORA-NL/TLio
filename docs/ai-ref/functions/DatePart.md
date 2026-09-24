# datePart

> Extracts one numbered component of a date — year, quarter, ISO day of week, ISO week number and the rest.

## Syntax

```
=datePart(<date>, <part>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The date to take the component from. |
| 2 | string | yes | The part name. Case-insensitive. |

Accepted parts: `year`, `month`, `day`, `hour`, `minute`, `second`, `quarter`, `dayOfWeek`, `dayOfYear`, `weekOfYear`, `daysInMonth`.

## Returns

A `long`. Everything is read from the **UTC** value of the date.

| Part | Range | Note |
|---|---|---|
| `year` | any | |
| `month` | 1–12 | |
| `day` | 1–31 | day of the month |
| `hour` / `minute` / `second` | 0–59 (hour 0–23) | `0` for a date-only value |
| `quarter` | 1–4 | |
| `dayOfWeek` | **1–7, Monday = 1** | ISO 8601, not .NET's `0 = Sunday` |
| `dayOfYear` | 1–366 | |
| `weekOfYear` | 1–53 | ISO 8601 week number |
| `daysInMonth` | 28–31 | leap-year correct |

## Verified example

```json
{ "command": "put", "path": "$.new",
  "value": "=datepart($.request.requestedStartDate,'year')" }
```

Input: `{ "request": { "requestedStartDate": "2026-09-01" } }`
Output: `{ ..., "new": 2026 }`

Note the type: `2026`, a number — where the old idiom, `=substring($.request.requestedStartDate,0,4)`,
gives the string `"2026"`. The fixture runs both (into `$.old` and `$.new`) to show the same year.

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/datepart/08-sample-year-replaces-the-substring-idiom.json`
(part-by-part behaviour — quarter, dayOfWeek, weekOfYear, daysInMonth, dayOfYear — verified by
`.../01-year.json` through `.../07-dayofyear.json` in the same directory)

## When to use

- Bucketing by period: reporting year, quarter or ISO week.
- Weekday rules — "no cover starts at the weekend" is `=equals(=datePart($.d,'dayOfWeek'),7)` and its Saturday twin.
- Pro-rata calculations that need the length of the month: `=datePart($.d,'daysInMonth')`.
- Anywhere a date's year or month is currently being sliced out with `substring`.

## When NOT to use

- You want a formatted string rather than a number — use `formatDate`, which can render any combination of components at once.
- You want the whole date shifted or compared — use `dateAdd`, `dateDiff` or `dateCompare`.
- You want the first or last day of the month as a date — `daysInMonth` gives the count; `endOfMonth` gives the date.

## Comparison

| Function | Input | Returns | Use when |
|----------|-------|---------|----------|
| `datePart` | date + part name | long | One component, as a number |
| `formatDate` | date + format string | string | Several components, rendered |
| `substring` | string + offset + length | string | Slicing text that happens to be a date — avoid for dates |
| `endOfMonth` | date | date string | The last day itself, not how many there are |

## Common mistakes

- **Expecting `dayOfWeek` to be .NET's.** .NET says Sunday is `0`; `datePart` says Sunday is `7` and Monday is `1`, per ISO 8601. Code ported from C# that tests `== 0` for Sunday will silently never match.
- **Expecting `weekOfYear` to be "roughly week number".** It is ISO 8601, so 1 January 2021 is **week 53** — of 2020. And 30 December 2019 is week 1 — of 2020. Pair it with `year` and you can get a mismatched combination; if you need the ISO year too, take it from `formatDate` rather than assuming.
- **Using `substring` instead.** `=substring($.d,0,4)` returns a *string*, and works only while the value is ISO and the field is exactly where you assumed. `datePart` returns a number and works on any parseable date.
- **Misspelling the part.** `'dow'`, `'week'` and `'dayofmonth'` are not accepted; the function fails and aborts the script. The error message lists the accepted parts.
- **Expecting local time.** A date carrying an offset is converted to UTC first, so `=datePart('2024-03-15T02:30:00+05:00','day')` is `14` — the UTC instant is 2024-03-14T21:30Z.
