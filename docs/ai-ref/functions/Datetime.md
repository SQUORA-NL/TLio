# =datetime()

> Returns the **current UTC date/time** as a formatted string. Defaults to ISO 8601
> (`yyyy-MM-ddTHH:mm:ss.fffZ`) when no format is provided.

## Syntax

```
=datetime()
=datetime(<format>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Used as a value in any command: `"value": "=datetime()"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (.NET format) | no | .NET date format string. Default: `yyyy-MM-ddTHH:mm:ss.fffZ` (ISO 8601 UTC). |

## Returns

A string containing the formatted current UTC date/time at the moment of script
execution.

## Common format strings

| Format | Example output |
|--------|----------------|
| (default) | `"2026-04-06T14:30:00.000Z"` |
| `yyyy-MM-dd` | `"2026-04-06"` |
| `dd/MM/yyyy` | `"06/04/2026"` |
| `yyyyMMddHHmmss` | `"20260406143000"` |

## Verified example

`datetime()` returns the current instant, so no fixture asserts a literal value; the inline
NUnit tests instead assert the **shape** of the result:

```json
{ "command": "set", "path": "$.demo", "value": "=datetime()" }
```

The default (no-argument) form matches the ISO 8601 pattern
`\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z` — verified by
`DatetimeFunctionTests.CanGetDatetimeValueWithDefaultIso8601Format`
(`TLio.Functions.Tests/FunctionsTests/DatetimeFunctionTests.cs:46-60`).

A custom format string is passed straight to `DateTime.UtcNow.ToString(format)`:

```json
{ "command": "put", "path": "$.year", "value": "=datetime(yyyy)" }
```

produces a 4-digit year (e.g. `"2026"`) — verified by
`DatetimeFunctionTests.CustomFormat_ReturnsFormattedDate` (same file, lines 106-118), which
asserts the result matches `\d{4}` and has length 4.

## C# Usage

```csharp
// Already registered via ParseOptions.CreateDefault()
var options = ParseOptions<JToken>.CreateDefault();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"put\",\"path\":\"$.createdAt\",\"value\":\"=datetime()\"}]",
    JObject.Parse("{}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- Stamping records with the current UTC time: `created_at`, `processed_at`, `report_as_of`, `last_modified`.
- Generating audit trails or event log entries where a consistent UTC timestamp is required.
- Inserting a snapshot of "now" into a document at script execution time — the value is captured once and does not change for the lifetime of the script run.

## When NOT to use

- You need local time or a time in a specific timezone. `datetime` always returns UTC — there is no timezone conversion argument.
- You need date arithmetic (add N days, subtract a period). `datetime` captures a snapshot; combine it with other transformation steps for arithmetic.
- You need to compare two stored dates. Use `dateCompare`, `isDateBetween`, `minDate`, or `maxDate` for comparisons between existing date fields.

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

- **Assuming local time.** `datetime()` is always UTC. If your data uses local dates (e.g., `"2024-06-15"` without a time component), comparing a UTC `datetime()` result near midnight can cross date boundaries unexpectedly and produce wrong range results.
- **Using datetime for "today's date" in a range comparison without UTC awareness.** `=datetime(yyyy-MM-dd)` gives today's UTC date, which may differ from the local calendar date for users in timezones ahead of UTC.
- **Incorrect .NET format strings.** The format argument uses .NET custom date format strings (`yyyy`, `MM`, `dd`, `HH`, `mm`, `ss`). Standard format specifiers like `"s"` or `"o"` also work. A format string `DateTime.ToString` rejects (throws on) is caught and **silently falls back to the default ISO 8601 format** — it does not produce an error or a literal copy of the format string. Verified by `DatetimeFunctionTests.InvalidFormatArg_FallsBackToIso8601` (`TLio.Functions.Tests/FunctionsTests/DatetimeFunctionTests.cs:76-88`), which passes `"%INVALID-FORMAT-STRING%"` and asserts a non-empty result rather than an exception.
- **Expecting the value to update across steps.** The timestamp is captured at the point `datetime()` is evaluated in the script. It does not re-evaluate on each command; use it in a `put` or `set` step early in the script if you want a consistent "run start" time.
