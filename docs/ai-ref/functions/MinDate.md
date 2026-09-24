# mindate

> Returns the earliest date among all its arguments — scalars, arrays, or a mix of both.

## Syntax

```
=mindate(<arg1>, <arg2>, ...)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1..n | date string, date array, or path | at least one | Variadic — each argument contributes one or more dates. An argument that resolves to an array contributes every element (recursively, so nested arrays flatten too). |

`mindate` is **not** limited to a single array-path form: `=mindate($.d1, $.d2, $.d3)` (several
scalar arguments) is exactly as valid as `=mindate($.dates)` (one array argument), and the two
forms can be mixed in one call.

## Returns

A string node with the earliest date, formatted by the same canonical ISO-8601 rule every
TimeDate function uses (date-only when the value carries no time component,
`yyyy-MM-ddTHH:mm:ssZ` otherwise). A `null` value anywhere in the collected dates, or a path that
matches nothing, fails the whole call — there is no "skip and continue" behaviour.

## Verified example

Two scalar arguments — the common case, not the array-only one the syntax might suggest:

```json
{ "command": "put", "path": "$.result", "value": "=mindate($.d1, $.d2)" }
```

Input: `{ "d1": "2024-01-01", "d2": "2024-06-15" }` → Output: `{ ..., "result": "2024-01-01" }`

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/mindate/01-two-scalars.json`. The same
fixture group also covers three scalar arguments
(`02-three-scalars.json`: `=mindate($.d1, $.d2, $.d3)` on `d3: "2023-11-30"` → `"2023-11-30"`), a
single array argument (`03-array.json`: `=mindate($.dates)` on
`["2024-03-01","2022-07-04","2025-01-01"]` → `"2022-07-04"`), and timestamps where the time
component decides the winner
(`04-timestamps.json`: `=mindate($.ts1, $.ts2)` on `"2024-03-15T08:00:00Z"` /
`"2024-03-15T20:00:00Z"` → `"2024-03-15T08:00:00Z"`).

## When to use

- Finding the earliest entry in a collection: first-created record, earliest deadline, oldest activity date.
- Summarising a set of event dates to a single "low watermark" value.
- Comparing a fixed, known number of named date fields — `=mindate($.applied,$.renewed,$.amended)` — without first collecting them into an array.
- Wildcard paths work: `$.events[*].date` resolves all dates across the array as a flat list and returns the minimum.

## When NOT to use

- You have exactly two dates and need to know their relative order — use `dateCompare` instead (returns -1/0/1 without losing the comparison semantics).
- You need to check whether a specific date falls within a range — use `isDateBetween` instead.
- You need the latest date in the set — use `maxDate` instead.
- You need the chronological midpoint — use `avgDate` instead.
- You need the current UTC timestamp — use `datetime` instead.

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

- **Assuming an array is required.** It is not — `minDate` is variadic and happily takes several
  scalar date arguments (`=mindate($.d1,$.d2,$.d3)`); an array path is just one way to supply
  many dates at once, not the only way.
- **Wanting *which one* rather than the value itself.** `minDate` (and `dateCompare` for exactly
  two dates) tells you the winning date, not which input produced it. If you need to know *which*
  record had the earliest date, sort the collection and take the first element instead.
- **Confusing minDate with dateCompare.** `minDate` returns the date string of the smallest value in a set; `dateCompare` returns a long (-1/0/1) describing the order of exactly two dates.
- **Ignoring the time component.** When date strings include time (`"2024-06-15T00:01:00"`), the time is included in the comparison. Two entries on the same calendar day with different times will not tie.
- **Path args resolve against document root.** `@.field` inside the function call refers to the root. Use `$.field` or wildcard paths like `$.records[*].timestamp` for unambiguous resolution.
