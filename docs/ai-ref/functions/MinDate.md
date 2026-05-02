# mindate

> Returns the earliest date from an array of date strings.

## Syntax

```
=mindate(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date array or path | yes | Path to an array of ISO-8601 date strings. |

## Returns

A string node with the earliest date.

## Example

```json
{ "command": "set", "path": "$.earliest", "value": "=mindate($.events[*].date)" }
```

Input: `{ "events": [{ "date": "2024-03-15" }, { "date": "2024-11-01" }, { "date": "2024-07-20" }], "earliest": null }`
Output: `{ ..., "earliest": "2024-03-15" }`

## When to use

- Finding the earliest entry in a collection: first-created record, earliest deadline, oldest activity date.
- Summarising a set of event dates to a single "low watermark" value.
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

- **Passing a single scalar value instead of an array.** `minDate` requires the path to resolve to an array. For a two-date comparison, use `dateCompare`.
- **Confusing minDate with dateCompare.** `minDate` returns the date string of the smallest value in a set; `dateCompare` returns a long (-1/0/1) describing the order of exactly two dates.
- **Ignoring the time component.** When date strings include time (`"2024-06-15T00:01:00"`), the time is included in the comparison. Two entries on the same calendar day with different times will not tie.
- **Path args resolve against document root.** `@.field` inside the function call refers to the root. Use `$.field` or wildcard paths like `$.records[*].timestamp` for unambiguous resolution.
