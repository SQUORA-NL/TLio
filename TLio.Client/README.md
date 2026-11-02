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

Each pack has one method that registers everything it provides — commands *and* functions —
so you never need to know which provider a pack uses. They return the options, so they chain:

```csharp
var options = ParseOptions<JToken>.CreateDefault()
    .WithMath()                                  // TLio.Extensions.Math
    .WithText()                                  // TLio.Extensions.Text
    .WithTimeDate()                              // TLio.Extensions.TimeDate
    .WithEtl()                                   // TLio.Extensions.ETL
    .WithLooping()                               // TLio.Extensions.Looping
    .WithFormatConversion(converter, "json");    // TLio.FormatConverter
```

`WithFormatConversion` takes the converter and the document's format id (`"json"`, `"xml"`,
`"yaml"`) because they decide whether `convertValue` writes structure or text. A pack you leave
out is not an error: its commands parse as unknown, which logs a warning and **succeeds**
without changing the document. The older `options.CommandsProvider.RegisterETL<JToken>()`-style
methods still work and are what the `With*` methods call.

## Pick a format adapter

| Format | Package | Context factory |
|---|---|---|
| JSON (Newtonsoft) | `TLio.Json` | `JsonExecutionContext.CreateDefault()` |
| JSON (System.Text.Json) | `TLio.Json.SystemText` | `SystemTextJsonExecutionContext.CreateDefault()` |
| XML | `TLio.Xml` | `XmlExecutionContext.CreateWithSlashPaths()` / `CreateWithNativeXPath()` |
| YAML | `TLio.Yaml` | `YamlExecutionContext.CreateDefault()` |

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
