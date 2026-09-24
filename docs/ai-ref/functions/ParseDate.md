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

## Verified example

```json
[
  { "command": "put", "path": "$.calc.quotedOn",
    "value": "=parsedate($.request.quotedOn,'dd-MM-yyyy')" },
  { "command": "put", "path": "$.calc.birthDate",
    "value": "=parsedate($.request.applicant.birthDate,'dd-MM-yyyy')" },
  { "command": "put", "path": "$.calc.driverAge",
    "value": "=datediff($.calc.birthDate,$.calc.quotedOn,'years')" }
]
```

Input: `{ "request": { "quotedOn": "21-08-2026", "applicant": { "birthDate": "04-11-1991" } } }`
Output: `{ ..., "calc": { "quotedOn": "2026-08-21", "birthDate": "1991-11-04", "driverAge": 34 } }`

Both Dutch `dd-MM-yyyy` dates are normalised to ISO first, and `dateDiff` runs on the normalised
values — this is the pattern for feeding non-ISO input into the rest of the TimeDate pack.

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/parsedate/05-sample-normalises-a-dutch-date-for-datediff.json`
(explicit format, default format list, a format with a time, and a compact `yyyyMMdd` format
verified by `.../01-explicit-format.json` through `.../04-compact-format.json` in the same
directory)

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

## Performance

With an explicit `format`, parsing is one `DateTimeOffset.TryParseExact` call against
`CultureInfo.InvariantCulture` — a shared static instance, not rebuilt per call. Without a
`format`, `parsedate` (and every other TimeDate function, for its date arguments) tries a short
static list of exact formats in order before falling back to a general parse; none of the
formats or the culture is reconstructed per call, so there is nothing here to cache. If a script
calls `parseDate` on the same handful of notations across many rows, the cost is dominated by
`TryParseExact` itself, not by any avoidable setup.

## Common mistakes

- **Passing the format you want *out*.** The format argument describes the **input**. `=parseDate('31-12-2026','yyyy-MM-dd')` fails; the output is always ISO and is not configurable. To choose the output notation, wrap the result in `formatDate`.
- **`TryParseExact` means exact.** A single stray space, a missing leading zero, or a `T` where the format says a space, and the parse fails and the script aborts. If the input is uneven, omit the format and let the built-in list try.
- **Trusting the default list with an ambiguous string.** `'01-02-2026'` reads as `dd-MM-yyyy` (1 February) because that is the order the list is tried in — but `'12/25/2026'` reads as `MM/dd/yyyy`. If both notations can appear in your data, state the format.
- **Expecting a failed parse to yield null.** It does not: the function fails, which aborts the script (see `docs/behaviour-decisions.md` B2). Guard an optional field with `exists` or `ifElse` before parsing it.
- **Expecting a time to survive when there is none.** A date-only input gives a date-only output — no `T00:00:00Z` is added.
- **Assuming a local time zone.** Text with no offset is read as UTC, not as the machine's zone.
