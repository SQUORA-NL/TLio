# TLio.Json.SystemText

The **System.Text.Json** adapter for [TLio](https://github.com/SQUORA-NL/TLio) —
`INodeAdapter<JsonNode>` and `IItemsFetcher<JsonNode>` implementations backed by
`System.Text.Json.Nodes` and RFC 9535 JSONPath (`JsonCons.JsonPath`).

Newtonsoft.Json is **not** referenced. Transformation behaviour is identical to `TLio.Json`.

```sh
dotnet add package TLio.Json.SystemText
dotnet add package TLio.Client
```

## Quick start

```csharp
using System.Text.Json.Nodes;
using TLio.Client;
using TLio.Json.SystemText;

var data   = JsonNode.Parse("""{ "name": "Acme", "tempId": 7 }""");
var script = """
[
  { "command": "put",    "path": "$.status", "value": "active" },
  { "command": "remove", "path": "$.tempId" }
]
""";

var options = ParseOptions<JsonNode>.CreateDefault();
var engine  = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);

var result = engine.Execute(script, data, SystemTextJsonExecutionContext.CreateDefault());
```

## Path language

RFC 9535 JSONPath — strict, standards-compliant:

```text
$.customer.name
$.orders[*].total
$.orders[?(@.total > 100)]
```

Script expressions (`()`) are **not** supported; use `TLio.Json` if you need them.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
