# avgdate

> Returns the average (mean) date across one or more arguments — scalars, arrays, or a mix of both.

## Syntax

```
=avgdate(<date1>, <date2>, ...)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1..n | date string, date array, or path | at least one | Each argument may be a single date or a path that resolves to an array of dates (or to multiple matches, e.g. a wildcard path). All dates from all arguments are pooled before averaging. A single argument is valid — the average of one date is that date. |

Like `mindate` and `maxdate`, `avgdate` is variadic and shares the same argument-collection
logic, not a single-array-only function.

## Returns

A string node with the average date. The average is computed as the mean of the pooled dates'
UTC ticks, then formatted back through the shared formatter: `yyyy-MM-dd` when the resulting
instant falls exactly on midnight UTC (as it does for date-only inputs whose count divides
evenly), otherwise `yyyy-MM-ddTHH:mm:ssZ`.

## Verified example

```json
{
  "command": "put",
  "path": "$.result",
  "value": "=avgdate($.ts1, $.ts2)"
}
```

Input: `{ "ts1": "2024-03-15T00:00:00Z", "ts2": "2024-03-15T12:00:00Z" }`
Output: `{ ..., "result": "2024-03-15T06:00:00Z" }`

A single argument passes through unchanged: `=avgdate($.d1)` over `"2024-01-01"` returns
`"2024-01-01"`.

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/avgdate/01-single-date.json`,
`.../02-timestamps-midpoint.json`

## When to use

- Computing the chronological midpoint of a set of event dates for statistical or reporting purposes.
- Summarising a distribution of timestamps — e.g., average activity date across a cohort.
- Scientific or analytical workflows where the mean date has meaningful interpretation.

## When NOT to use

- Most business workflows — average dates are rarely meaningful outside statistical or analytical contexts. If you want the latest or earliest, use `maxDate` or `minDate`.
- You need the most recent or oldest date — use `maxDate` or `minDate` respectively.
- You need to know whether a specific date falls within a range — use `isDateBetween`.
- You need to order or compare two specific dates — use `dateCompare`.
- You need the current UTC timestamp — use `datetime`.

## Comparison

| Function | Input | Returns | Use when |
|----------|-------|---------|----------|
| isDateBetween | date + from + to | boolean | Date within a range? |
| dateCompare | date1 + date2 | long (-1/0/1) | Which date is earlier/later? |
| minDate | date array | date string | Earliest date in a set |
| maxDate | date array | date string | Latest date in a set |
| avgDate | date array | date string | Chronological midpoint of a set |
| datetime | (no input) | date string | Current UTC timestamp |

## Common mistakes

- **Confusing avgDate with maxDate or minDate.** `avgDate` returns the chronological midpoint, not the extreme. If you want the boundary of a set, use `maxDate` or `minDate`.
- **Expecting a single-argument call to fail.** `=avgdate($.d1)` is valid — the average of one date is that date. It is a legitimate degenerate case, not an error.
- **Expecting time-precision output.** `avgDate` returns a `yyyy-MM-dd` string. Sub-day precision is lost in the result even if the input strings include time components.
- **Path args resolve against document root.** `@.field` inside the function call refers to the root. Use `$.field` or wildcard paths like `$.events[*].date` for unambiguous resolution.
