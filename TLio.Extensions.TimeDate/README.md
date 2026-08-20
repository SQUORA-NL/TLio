# TLio.Extensions.TimeDate

Date and time functions for [TLio](https://github.com/SQUORA-NL/TLio). Format-agnostic — works
against JSON, XML, and YAML through `IExecutionContext<TNode>`.

```sh
dotnet add package TLio.Extensions.TimeDate
```

## Register the pack

```csharp
using TLio.Extensions.TimeDate;

var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterTimeDate<JToken>();
```

## Functions

| Function | Purpose |
|---|---|
| `datecompare` | Compare two dates |
| `isdatebetween` | Test whether a date falls in a range |
| `mindate` | Earliest date in a set |
| `maxdate` | Latest date in a set |
| `avgdate` | Average of a set of dates |

## Example

```json
[
  { "command": "add", "path": "$.firstOrder",
    "value": "=mindate(=fetch($.orders[*].placedOn))" },
  { "command": "add", "path": "$.inQuarter",
    "value": "=isdatebetween(=fetch($.placedOn), '2026-01-01', '2026-03-31')" }
]
```

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
