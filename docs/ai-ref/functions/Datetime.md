# =datetime()

> Returns the **current UTC date/time** as a formatted string. Defaults to ISO 8601
> (`yyyy-MM-ddTHH:mm:ss.fffZ`) when no format is provided.

## Syntax

```
=datetime()
=datetime(format)
```

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

## Example

```json
{ "command": "put", "path": "$.createdAt", "value": "=datetime()" }
```

```json
{ "command": "put", "path": "$.date", "value": "=datetime(yyyy-MM-dd)" }
```

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
