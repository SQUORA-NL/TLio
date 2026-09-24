# datecompare

> Compares two dates and returns -1, 0, or 1 indicating their relative order.

## Syntax

```
=datecompare(<date1>, <date2>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The first date. |
| 2 | date string or path | yes | The second date. |

## Returns

A long integer:
- `-1` — date1 is before date2
- `0` — dates are equal
- `1` — date1 is after date2

**Important**: The return type is `long` (integer), not a string like "before"/"after"/"equal".

## Verified example

```json
{
  "command": "put",
  "path": "$.result",
  "value": "=datecompare($.earlier, $.later)"
}
```

Input: `{ "earlier": "2024-01-01", "later": "2024-06-15" }`
Output: `{ ..., "result": -1 }`

Swap the arguments (`=datecompare($.later, $.earlier)`) and the result is `1`; compare a date to
itself and it is `0`. Timestamps compare the same way — `2024-03-15T10:30:00Z` vs.
`2024-03-15T18:00:00Z` is also `-1`.

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/datecompare/01-earlier.json`,
`.../02-later.json`, `.../03-equal.json`, `.../04-timestamps.json`

## When to use

- Determining which of two dates comes first: sequencing events, checking delivery before deadline.
- Branching logic that depends on relative date order — feed the result into `ifElse` or `decisionTable`.
- Storing the comparison result as a numeric flag for downstream logic.
- Sorting or ranking records where date order drives the outcome.
- Any case where you need the three-way distinction: earlier / equal / later.

## When NOT to use

- You just need to know if a date falls inside a range — use `isDateBetween` instead (cleaner, no manual -1/0/1 interpretation needed).
- You need the minimum or maximum from an array of dates — use `minDate` or `maxDate` instead.
- You need the chronological midpoint of a set — use `avgDate` instead.

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

- **Asserting on strings instead of longs.** `dateCompare` never returns `"before"`, `"after"`, or `"equal"`. In test assertions use `output["cmp"].Value<long>() == -1L`, not a string comparison.
- **Reversing argument order.** `dateCompare(d1, d2)` returns `-1` when `d1` is the earlier date. Swapping the arguments inverts the sign.
- **Using dateCompare for a range check.** If the question is "is this date inside a window?", `isDateBetween` is more readable and handles both bounds inclusively without manual threshold logic.
- **Ignoring the time component.** All date functions accept ISO-8601 strings. When time is present (`"2024-06-15T08:00:00"`), the comparison includes the time. Two dates that share the same calendar day but differ in time will not return `0`.
- **Feeding the raw result straight into `ifElse`.** A condition is truthy only for the boolean `true` or the string `"true"` — a numeric `-1`, `0` or `1` is never truthy, so `"condition": "=datecompare(...)"` always takes the else branch. Wrap it in a comparison first, e.g. `=equals(=datecompare($.a,$.b),-1)` or `=lessThan(=datecompare($.a,$.b),0)`.
- **Path args resolve against document root.** `@.field` inside the function call refers to the root. Use `$.field` for unambiguous root-relative paths.
