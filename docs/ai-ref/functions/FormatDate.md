# formatDate

> Renders a date the document already holds, using a .NET format string and InvariantCulture.

## Syntax

```
=formatDate(<date>, <format>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The date to render. |
| 2 | string or path | yes | A .NET date/time format string, e.g. `dd-MM-yyyy`, `yyyyMMdd`, `HH:mm`. |

## Returns

A string. The format is applied to the **UTC** value of the date, always with `CultureInfo.InvariantCulture`, so the output does not change with the machine's locale.

| Expression | Result |
|---|---|
| `=formatDate('2026-08-21','dd-MM-yyyy')` | `21-08-2026` |
| `=formatDate('2026-08-21','yyyyMMdd')` | `20260821` |
| `=formatDate('2026-08-21','MMMM yyyy')` | `August 2026` |
| `=formatDate('2024-03-15T14:05:09Z','HH:mm')` | `14:05` |
| `=formatDate('2024-03-15T23:30:00+05:00','yyyy-MM-dd HH:mm')` | `2024-03-15 18:30` |

## Verified example

```json
{ "command": "put", "path": "$.new",
  "value": "=formatdate($.request.quotedOn,'yyyyMMdd')" }
```

Input: `{ "request": { "quotedOn": "2026-08-21" } }`
Output: `{ ..., "new": "20260821" }`

The fixture also runs the old `=replace($.request.quotedOn,'-','')` idiom side by side (into
`$.old`) and asserts both give `"20260821"` — the same date-with-dashes-stripped result, but
`formatDate` gets there by rendering the date, not by editing the string.

Verified by: `TLio.Functions.Tests/Fixtures/TimeDate/formatdate/05-sample-compact-matches-the-replace-idiom.json`
(notation, time-only and offset-to-UTC rendering verified by `.../01-dutch-notation.json`
through `.../04-offset-renders-as-utc.json` in the same directory)

## When to use

- Presenting a stored date in a local notation for a document, a letter or a UI payload.
- Producing a sortable or comparable key: `=formatDate($.d,'yyyyMMdd')`.
- Extracting several components at once — `formatDate` does in one call what several `datePart` calls would.
- Feeding a downstream system that insists on its own date notation.

## When NOT to use

- You want the **current** date or time — use `datetime(format)`. `formatDate` needs a date to work on; `datetime` can only ever produce *now*. They are the two halves of the same job.
- You want a number back rather than a string — use `datePart`, which returns a `long`.
- You are *reading* a date in an unusual notation rather than writing one — that is `parseDate`, the inverse of this function.

## Comparison

| Function | Input | Returns | Use when |
|----------|-------|---------|----------|
| `formatDate` | a stored date + format | string | Render a date the document holds |
| `datetime` | format only | string | Render *now* — there is no date argument |
| `parseDate` | text + format | ISO date string | Read a date written in another notation |
| `datePart` | date + part name | long | One component, as a number |

## Performance

`CultureInfo.InvariantCulture` is a shared static instance, not built per call, and the render
itself is a single `DateTime.ToString(format, ...)` call — there is no per-call format-provider
construction to cache. The only real cost is the up-front `DateTimeOffset` parse of argument 1
(`TryParseDate` tries a short static list of exact formats before falling back to a general
parse), which is the same cost every TimeDate function pays for its date arguments. No caching
is needed and none would meaningfully help.

## Common mistakes

- **Reaching for `datetime` to render a stored date.** `=datetime('dd-MM-yyyy')` formats today, not `$.request.quotedOn`, and the mistake is invisible in a test written on the day the data was captured. If there is a date argument, the function you want is `formatDate`.
- **Assuming the machine's culture applies.** It never does: month and day names come out in English and the separators are literal. `=formatDate($.d,'MMMM')` is `August` on a Dutch machine too. Use `format`/`concat` if you need localised text.
- **Single-character formats are standard specifiers, not custom ones.** `'d'` is the invariant short date (`08/21/2026`), not "day of month". Write `'dd'` — or `'%d'` — when you mean the day.
- **Lowercase `mm` is minutes, not months.** `'yyyy-mm-dd'` on 21 August 2026 renders `2026-00-21` — a valid-looking string with the minute where the month should be. Months are `MM`; minutes are `mm`. Nothing fails, so this one only shows up in the output.
- **Unrecognised letters become literals rather than errors.** `'yyyy Q'` renders `2026 Q`. A typo'd specifier does not fail the function, it silently prints itself — check the output, do not rely on an error.
- **An unterminated quote does fail.** `'yyyy-MM-dd''` throws and aborts the script rather than producing a partial string.
- **Expecting local time.** The value is converted to UTC first, so an offset input shifts. See the `+05:00` row above.
