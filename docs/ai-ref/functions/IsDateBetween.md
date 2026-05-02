# isdatebetween

> Returns true when a date falls within a range (inclusive on both ends).

## Syntax

```
=isdatebetween(<date>, <from>, <to>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The date to test (ISO-8601 or `yyyy-MM-dd`). |
| 2 | date string or path | yes | Start of the range (inclusive). |
| 3 | date string or path | yes | End of the range (inclusive). |

## Returns

A boolean node: `true` if `from <= date <= to`, `false` otherwise.

## Example

```json
{ "command": "set", "path": "$.result", "value": "=isdatebetween($.event,$.from,$.to)" }
```

Input: `{ "event": "2024-06-15", "from": "2024-01-01", "to": "2024-12-31", "result": null }`
Output: `{ ..., "result": true }`

## When to use

- Validating whether an event or record falls within a reporting period (e.g., fiscal quarter).
- Checking subscription windows: is today between `subscription_start` and `subscription_end`?
- Filtering records that fall inside a promotional or campaign date range.
- Any scenario where the answer is simply "is this date in range?" — both bounds are inclusive, so boundary dates return `true`.
- Store the boolean with `set`, then branch on it with `ifElse` if conditional logic follows.

## When NOT to use

- You need to know which of two dates is earlier or later — use `dateCompare` instead.
- You need the latest or earliest date from a collection — use `maxDate` or `minDate` instead.
- You need to rank, sort, or sequence dates — use `dateCompare` instead.
- You need the average date from a set — use `avgDate` instead.

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

- **Time component is compared when present.** Passing `"2024-06-15T23:59:00"` as the date and `"2024-06-15"` as `to` may return `false` because the time part pushes the date past midnight boundary. Strip the time component or use matching precision on all three arguments.
- **Expecting half-open range semantics.** Both bounds are inclusive. `isdatebetween("2024-01-01", "2024-01-01", "2024-12-31")` returns `true`.
- **Using isDateBetween when only two dates need ordering.** If you have `d1` and `d2` and want to know which is earlier, `dateCompare` is cleaner and more explicit.
- **Path args resolve against document root.** `@.field` inside the function call refers to the root, not a nested context. Use `$.field` for unambiguous root-relative paths.
