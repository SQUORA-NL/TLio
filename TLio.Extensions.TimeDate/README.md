# TLio.Extensions.TimeDate

Date and time functions for [TLio](https://github.com/SQUORA-NL/TLio). Format-agnostic — works
against JSON, XML, and YAML through `IExecutionContext<TNode>`.

```sh
dotnet add package TLio.Extensions.TimeDate
```

## Quick start

```csharp
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Extensions.TimeDate;
using TLio.Json;

var data   = JToken.Parse("""{ "d1": "2024-01-01", "d2": "2024-06-15" }""");
var script = """
[
  { "command": "add", "path": "$.earliest", "value": "=mindate($.d1, $.d2)" }
]
""";

var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterTimeDate<JToken>();

var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
// result.Data["earliest"] → "2024-01-01"
```

## Functions

| Function | Purpose |
|---|---|
| `datecompare` | Compare two dates |
| `isdatebetween` | Test whether a date falls in a range |
| `mindate` | Earliest date in a set |
| `maxdate` | Latest date in a set |
| `avgdate` | Average of a set of dates |

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
