# TLio.Client

The scripting engine for [TLio](https://github.com/SQUORA-NL/TLio) — parses and executes TLio
scripts against any structured data format. Format-neutral: format support is registered through the
provider pattern.

```sh
dotnet add package TLio.Client
```

## Quick start (JSON)

```csharp
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Json;

var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

var result = engine.Execute(scriptJson, data, JsonExecutionContext.CreateDefault());
```

A script is a plain array of command objects:

```json
[
  { "command": "put",    "path": "$.status",    "value": "active" },
  { "command": "add",    "path": "$.createdAt", "value": "=datetime()" },
  { "command": "remove", "path": "$.tempId" }
]
```

## What's in the box

- `ScriptEngine<TNode>` — parse and execute scripts
- `CompiledScript<TNode>` — parse once, execute many times
- `ParseOptions<TNode>` — command and function registries
- `TLioConvert` — fluent convenience API

## Registering extension packs

```csharp
options.CommandsProvider.RegisterETL<JToken>();       // TLio.Extensions.ETL
options.FunctionsProvider.RegisterText<JToken>();     // TLio.Extensions.Text
options.FunctionsProvider.RegisterMath<JToken>();     // TLio.Extensions.Math
options.FunctionsProvider.RegisterTimeDate<JToken>(); // TLio.Extensions.TimeDate
```

## Pick a format adapter

| Format | Package | Context factory |
|---|---|---|
| JSON (Newtonsoft) | `TLio.Json` | `JsonExecutionContext.CreateDefault()` |
| JSON (System.Text.Json) | `TLio.Json.SystemText` | `SystemTextJsonExecutionContext.CreateDefault()` |
| XML | `TLio.Xml` | `XmlExecutionContext.CreateWithSlashPaths()` / `CreateWithNativeXPath()` |
| YAML | `TLio.Yaml` | `YamlExecutionContext.CreateDefault()` |

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
