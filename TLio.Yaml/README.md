# TLio.Yaml

The **YAML** adapter for [TLio](https://github.com/SQUORA-NL/TLio) — `INodeAdapter` and
`IItemsFetcher` implementations backed by [YamlDotNet](https://github.com/aaubry/YamlDotNet).

```sh
dotnet add package TLio.Yaml
dotnet add package TLio.Client
```

## Quick start

```csharp
using TLio.Client;
using TLio.Yaml;

var script = """
[
  { "command": "put",    "path": "$.status", "value": "active" },
  { "command": "remove", "path": "$.tempId" }
]
""";

var options = ParseOptions<YamlNode>.CreateDefault();
var engine  = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);

var result = engine.Execute(script, data, YamlExecutionContext.CreateDefault());
```

## Path language

Dot-notation, written with the familiar `$` root:

```text
$.customer.name
$.orders[0].total
```

Multi-document YAML (`---` separated) is parsed as an array root.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
