# maxdate

> Returns the latest date from an array of date strings.

## Syntax

```
=maxdate(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date array or path | yes | Path to an array of ISO-8601 date strings. |

## Returns

A string node with the latest date.

## Example

```json
{ "command": "set", "path": "$.latest", "value": "=maxdate($.events[*].date)" }
```

Input: `{ "events": [{ "date": "2024-03-15" }, { "date": "2024-11-01" }, { "date": "2024-07-20" }], "latest": null }`
Output: `{ ..., "latest": "2024-11-01" }`

## When to use

- Finding the latest entry in a collection: most-recent activity date, last-updated timestamp, furthest deadline.
- Summarising a set of event dates to a single "high watermark" value.
- Wildcard paths work: `$.events[*].date` resolves all dates across the array as a flat list and returns the maximum.

## When NOT to use

- You have exactly two dates and need to know their relative order — use `dateCompare` instead (returns -1/0/1 without losing the comparison semantics).
- You need to check whether a specific date falls within a range — use `isDateBetween` instead.
- You need the earliest date in the set — use `minDate` instead.
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

- **Passing a single scalar value instead of an array.** `maxDate` requires the path to resolve to an array. For a two-date comparison, use `dateCompare`.
- **Confusing maxDate with dateCompare.** `maxDate` returns the date string of the largest value in a set; `dateCompare` returns a long (-1/0/1) describing the order of exactly two dates.
- **Ignoring the time component.** When date strings include time (`"2024-06-15T23:59:00"`), the time is included in the comparison. Two entries on the same calendar day with different times will not tie.
- **Path args resolve against document root.** `@.field` inside the function call refers to the root. Use `$.field` or wildcard paths like `$.records[*].timestamp` for unambiguous resolution.
