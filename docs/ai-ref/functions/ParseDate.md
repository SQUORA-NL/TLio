# parseDate

> Reads a date written in another notation and returns it in TLio's canonical ISO form.

## Syntax

```
=parseDate(<text>)
=parseDate(<text>, <format>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The text to read. |
| 2 | string or path | no | A .NET date/time format string the text must match **exactly**. Without it, TLio's built-in format list is used. |

## Returns

A date string: `yyyy-MM-dd` when the value has no time component, `yyyy-MM-ddTHH:mm:ssZ` otherwise. Parsing is always `CultureInfo.InvariantCulture`; text carrying no offset is read as UTC.

Without a `format`, these are recognised: `yyyy-MM-ddTHH:mm:sszzz`, `yyyy-MM-ddTHH:mm:ss`, `yyyy-MM-ddTHH:mm:ssZ`, `yyyy-MM-dd`, `MM/dd/yyyy`, `dd-MM-yyyy`, plus any string .NET can round-trip.

| Expression | Result |
|---|---|
| `=parseDate('31-12-2026','dd-MM-yyyy')` | `2026-12-31` |
| `=parseDate('20260821','yyyyMMdd')` | `2026-08-21` |
| `=parseDate('12/25/2026')` | `2026-12-25` |
| `=parseDate('2024-03-15 14:05','yyyy-MM-dd HH:mm')` | `2024-03-15T14:05:00Z` |

## Example

```json
[
  { "command": "put", "path": "$.calc.birthDate",
    "value": "=parseDate($.request.applicant.birthDate,'dd-MM-yyyy')" },
  { "command": "put", "path": "$.calc.driverAge",
    "value": "=dateDiff($.calc.birthDate,$.calc.quotedOn,'years')" }
]
```

Input: `{ "request": { "applicant": { "birthDate": "04-11-1991" } } }`
Output: `{ ..., "calc": { "birthDate": "1991-11-04", ... } }`

## When to use

- Normalising an inbound feed before any other date function touches it — one call at the edge, ISO everywhere after.
- Reading a notation the built-in list does not cover: `yyyyMMdd`, `dd.MM.yyyy`, `MMM d, yyyy`.
- Resolving an ambiguous notation deliberately: `'01-02-2026'` is 1 February with `'dd-MM-yyyy'` and 2 January with `'MM-dd-yyyy'`. Stating the format is how you choose.

## When NOT to use

- The value is already ISO — every date function parses it directly, so `parseDate` adds nothing.
- You want to *write* a date in another notation — that is `formatDate`, the inverse of this function.
- You want the current time — use `datetime`.
- You want a component as a number — use `datePart`.

## Comparison

| Function | Input | Returns | Use when |
|----------|-------|---------|----------|
| `parseDate` | text + format | ISO date string | Read a date written another way |
| `formatDate` | date + format | string | Write a date another way |
| `datetime` | format only | string | Render *now* |
| `toString` | any node | string | Plain string conversion, no date awareness |

## Common mistakes

- **Passing the format you want *out*.** The format argument describes the **input**. `=parseDate('31-12-2026','yyyy-MM-dd')` fails; the output is always ISO and is not configurable. To choose the output notation, wrap the result in `formatDate`.
- **`TryParseExact` means exact.** A single stray space, a missing leading zero, or a `T` where the format says a space, and the parse fails and the script aborts. If the input is uneven, omit the format and let the built-in list try.
- **Trusting the default list with an ambiguous string.** `'01-02-2026'` reads as `dd-MM-yyyy` (1 February) because that is the order the list is tried in — but `'12/25/2026'` reads as `MM/dd/yyyy`. If both notations can appear in your data, state the format.
- **Expecting a failed parse to yield null.** It does not: the function fails, which aborts the script (see `docs/behaviour-decisions.md` B2). Guard an optional field with `exists` or `ifElse` before parsing it.
- **Expecting a time to survive when there is none.** A date-only input gives a date-only output — no `T00:00:00Z` is added.
- **Assuming a local time zone.** Text with no offset is read as UTC, not as the machine's zone.
