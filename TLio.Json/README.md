# TLio.Json

The **Newtonsoft.Json** adapter for [TLio](https://github.com/SQUORA-NL/TLio) — `INodeAdapter<JToken>`
and `IItemsFetcher<JToken>` implementations backed by `Newtonsoft.Json.Linq` and Goessner JSONPath.

This is the default JSON choice for TLio. For the in-box stack, see `TLio.Json.SystemText`.

```sh
dotnet add package TLio.Json
dotnet add package TLio.Client
```

## Quick start

```csharp
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Json;

var data   = JToken.Parse("""{ "name": "Acme", "tempId": 7 }""");
var script = """
[
  { "command": "put",    "path": "$.status", "value": "active" },
  { "command": "remove", "path": "$.tempId" }
]
""";

var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
```

## Path language

Full Goessner JSONPath as implemented by Newtonsoft's `SelectTokens`, including filter and script
expressions:

```text
$.customer.name
$.orders[*].total
$.orders[?(@.total > 100)]
```

## Choosing between the JSON adapters

| | `TLio.Json` | `TLio.Json.SystemText` |
|---|---|---|
| Node type | `JToken` | `System.Text.Json.Nodes.JsonNode` |
| Dependency | Newtonsoft.Json | in-box + `JsonCons.JsonPath` |
| JSONPath | Goessner, incl. script expressions | RFC 9535 strict, no script expressions |

Transformation behaviour is otherwise identical.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
