# avgdate

> Returns the average (mean) date from an array of date strings.

## Syntax

```
=avgdate(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date array or path | yes | Path to an array of ISO-8601 date strings. |

## Returns

A string node with the average date in `yyyy-MM-dd` format.

## Example

```json
{ "command": "set", "path": "$.avg", "value": "=avgdate($.events[*].date)" }
```

Input: `{ "events": [{ "date": "2024-01-01" }, { "date": "2024-07-01" }, { "date": "2024-12-31" }], "avg": null }`
Output: `{ ..., "avg": "2024-07-01" }`

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
- **Passing a non-array path.** The argument must resolve to an array of date strings. A single scalar date string is not valid input — for two-date comparison use `dateCompare`.
- **Expecting time-precision output.** `avgDate` returns a `yyyy-MM-dd` string. Sub-day precision is lost in the result even if the input strings include time components.
- **Path args resolve against document root.** `@.field` inside the function call refers to the root. Use `$.field` or wildcard paths like `$.events[*].date` for unambiguous resolution.
