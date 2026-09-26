# TLio.Extensions.ETL

ETL commands for [TLio](https://github.com/SQUORA-NL/TLio) — flatten nested structures, restore
them, resolve references, and emit CSV. Format-agnostic via `INodeAdapter<TNode>`.

```sh
dotnet add package TLio.Extensions.ETL
```

## Quick start

```csharp
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Extensions.ETL;
using TLio.Json;

var data   = JToken.Parse("""{ "table": [ { "name": "Alice", "age": 30 }, { "name": "Bob", "age": 25 } ] }""");
var script = """
[
  { "command": "tocsv", "path": "$.table" }
]
""";

var options = ParseOptions<JToken>.CreateDefault();
options.CommandsProvider.RegisterETL<JToken>();

var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
// result.Data["table"] → "age,name" + "30,Alice" + "25,Bob", joined by Environment.NewLine
// (tocsv writes the CSV string in place; columns are sorted alphabetically)
```

## Commands

| Command | Purpose |
|---|---|
| `flatten` | Collapse a nested structure into a flat, path-keyed map |
| `restore` | Rebuild the nested structure from a flattened one |
| `resolve` | Resolve references within the document |
| `tocsv` | Render an array of records as CSV |

`flatten` and `restore` are a round-trip pair.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
