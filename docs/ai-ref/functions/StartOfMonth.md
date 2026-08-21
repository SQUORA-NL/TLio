# startOfMonth

> The first day of the month a date falls in, as a date-only ISO string.

## Syntax

```
=startOfMonth(<date>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | Any date in the month you want the start of. |

## Returns

A date-only string, `yyyy-MM-dd`. Any time component is dropped — the answer is a day, not an instant.

| Expression | Result |
|---|---|
| `=startOfMonth('2024-02-17')` | `2024-02-01` |
| `=startOfMonth('2024-04-10T22:15:00Z')` | `2024-04-01` |
| `=startOfMonth('2024-02-01')` | `2024-02-01` |
| `=startOfMonth('2025-01-31')` | `2025-01-01` |

## Example

```json
{ "command": "put", "path": "$.policy.termStart",
  "value": "=startOfMonth($.request.requestedStartDate)" }
```

Input: `{ "request": { "requestedStartDate": "2026-09-15" } }`
Output: `{ ..., "policy": { "termStart": "2026-09-01" } }`

## When to use

- Term and billing boundaries: normalising a mid-month start to the first of the month.
- Grouping records into monthly buckets — the value is a stable key for every date in that month.
- As the low bound of a month window, paired with `endOfMonth` as the high bound and `isDateBetween` as the test.
- Pro-rata arithmetic: `=dateDiff(=startOfMonth($.d),$.d,'days')` is how far into the month you are.

## When NOT to use

- You need the last day — use `endOfMonth`. Month lengths vary, so the two are separate functions.
- You need an arbitrary shift rather than a boundary — use `dateAdd`.
- You need the month *number* rather than a date — use `datePart(d,'month')`.

## Comparison

| Function | Input | Returns | Use when |
|----------|-------|---------|----------|
| `startOfMonth` | one date | date string | First day of that month |
| `endOfMonth` | one date | date string | Last day of that month |
| `datePart` | date + `daysInMonth` | long | How many days the month has |
| `dateAdd` | date + amount + unit | date string | Any other shift |

## Common mistakes

- **Expecting a timestamp back.** `=startOfMonth('2024-04-10T22:15:00Z')` is `2024-04-01`, not `2024-04-01T00:00:00Z`. If a downstream comparison needs an instant, every TLio date function reads the date-only form as midnight UTC anyway.
- **Using it as the *end* of the previous month.** It is not: step back a day with `=dateAdd(=startOfMonth($.d),-1)`, or take `endOfMonth` of a date in that month.
- **Expecting local-month semantics.** The month comes from the UTC value, so `2024-03-01T02:00:00+05:00` — 29 February UTC — gives `2024-02-01`.
- **Reaching for string surgery instead.** `=concat(=substring($.d,0,7),'-01')` looks equivalent and breaks the moment the input is not exactly ISO date-only.
