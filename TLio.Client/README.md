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

## Writing the script in XML or YAML

The same script has an XML and a YAML spelling. The notation is independent of the format of
the data — only the paths inside the script have to speak the data's path language. JSON works
out of the box; the other two register in a line each:

```csharp
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider)
    .UseXmlScripts()     // TLio.Xml
    .UseYamlScripts();   // TLio.Yaml

engine.Execute(scriptText, data, context);                     // notation detected from the text
engine.Execute(scriptText, ScriptFormat.Xml, data, context);   // or stated outright
```

```xml
<script>
  <put path="$.status">active</put>
  <add path="$.createdAt">=datetime()</add>
  <remove path="$.tempId"/>
</script>
```

```yaml
- command: put
  path: $.status
  value: active
- command: add
  path: $.createdAt
  value: "=datetime()"
- command: remove
  path: $.tempId
```

Text that does not parse yields an empty script carrying the reason in
`TLioScript.ParseWarnings` rather than throwing.

## What's in the box

- `ScriptEngine<TNode>` — parse and execute scripts in any of the three notations
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
