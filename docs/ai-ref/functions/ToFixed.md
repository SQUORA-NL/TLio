# toFixed

> Formats a number as text with exactly N decimals, keeping trailing zeros.

## Syntax

```
=toFixed(<value>, <decimals>)
=toFixed(<value>, <decimals>, <decimalSeparator>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to format. Must be numeric. |
| 2 | number | yes | Digits after the separator, 0–15. |
| 3 | string | no | Decimal separator. Defaults to `.` regardless of the machine's locale. |

## Returns

A **string** node. Trailing zeros cannot survive in a numeric node — `14.50` as a number is
`14.5` — so fixed-decimal output is always text.

## Rounding

Half away from zero: `=toFixed(14.5, 0)` → `"15"`, `=toFixed(-2.345, 2)` → `"-2.35"`.
This differs from .NET's own `F` specifier used by `format`, which rounds half to even.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=toFixed($.price, 2)" }
```

Input: `{ "price": 14.5 }`
Output: `{ "price": 14.5, "result": "14.50" }`

Locale-style output: `=toFixed($.price, 2, ',')` → `"14,50"`.

Verified by: `TLio.Functions.Tests/Fixtures/Text/tofixed/01-two-decimals.json` and
`02-custom-separator.json` (added as part of this documentation sweep — this function's fixture
directory did not previously exist even though `toFixed` is a registered function; also wired
into `ExtensionFixtureTests.Text_ToFixed`). Rounding-mode and culture-independence behavior is
additionally covered by the inline test fixture `DecimalFormattingTests` (in
`TLio.Functions.Tests/FunctionsTests/TextTests/`), including `=toFixed($.price, 0)` → `"15"`
(half away from zero) and a run under `nl-NL` culture that still produces `"14.50"`.

## When to use

- Money and invoice amounts that must show two decimals.
- Fixed-width numeric text for exports, file names, or downstream systems that parse text.

## When NOT to use

- You want a rounded **number**, not text — use `round` from the Math pack.
- You need thousands separators or a currency symbol — use `format` with a specifier such as `{0:N2}`.

## Comparison

| Function | Returns | 14.5 with 2 decimals |
|----------|---------|----------------------|
| `toFixed` | string | `"14.50"` |
| `round` (Math) | number | `14.5` |
| `format('{0:F2}', …)` | string | `"14.50"` (rounds half to even) |
| `toString` | string | `"14.5"` |

## Common mistakes

- **Expecting a number back**: the result is text; arithmetic on it will re-parse the string.
- **Feeding it text**: a JSON string like `"14.5"` is not numeric — `toFixed` logs an error. Convert with `parse` first.
- **Locale assumptions**: the default separator is always `.`; pass the third argument for `,`.
