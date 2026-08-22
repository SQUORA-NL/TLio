# TLio.FormatConverter

The `convert` and `convertValue` commands for [TLio](https://github.com/SQUORA-NL/TLio), plus
`MultiFormatScriptRunner` — the piece that lets one script change the document's format partway
through. Referencing this package pulls the JSON, XML and YAML converters in behind it.

```sh
dotnet add package TLio.FormatConverter
```

## Two commands, and what changes format

- **`convert`** changes the *whole document*. That means a different `TNode`, which no
  `ICommand<TNode>` can return — so the engine cannot run it. `MultiFormatScriptRunner` splits
  the script at each boundary and re-hosts each section on the engine for that format. Run on a
  bare engine it converts nothing and says so.
- **`convertValue`** changes *one value* at a path. The document's format never changes, so it
  is an ordinary command on the ordinary engine — the embedded-payload case, an XML string
  inside a JSON envelope.

Paths after a `convert` speak the new format's path language. That is inherent: the paths
address the document, and the document has changed format.

## Quick start

```csharp
using FormatConverter.Core;
using FormatConverter.Json;
using FormatConverter.TLio;
using FormatConverter.Xml;
using FormatConverter.Yaml;

var converter = new FormatConverter.Core.FormatConverter();
converter.Register(new JsonFormatAdapter());
converter.Register(new XmlFormatAdapter());
converter.Register(new YamlFormatAdapter());

var runner = new MultiFormatScriptRunner(converter);
runner.RegisterExecutor(new ScriptEngineSectionExecutor<JToken>(
    "json", jsonEngine, JsonExecutionContext.CreateDefault));
runner.RegisterExecutor(new ScriptEngineSectionExecutor<XElement>(
    "xml", xmlEngine, XmlExecutionContext.CreateWithNativeXPath));
runner.RegisterExecutor(new ScriptEngineSectionExecutor<YamlNode>(
    "yaml", yamlEngine, YamlExecutionContext.CreateDefault));

var result = runner.Run("json", document, script);
// result.Document, result.FormatId, result.Success, result.Logs
```

The script:

```json
[
  { "command": "add",     "path": "$.order.status", "value": "new"    },
  { "command": "convert", "to": "yaml" },
  { "command": "add",     "path": "order.source",   "value": "portal" },
  { "command": "convert", "to": "xml"  },
  { "command": "rename",  "path": "/order",         "name":  "opdracht" }
]
```

`MultiFormatScriptRunner.CrossesAFormatBoundary(script)` is how a host decides whether a script
needs the runner or a plain `ScriptEngine`.

## Registering the commands on an engine

`convertValue` works on any engine; `convert` still needs the runner, but registering it means a
script that uses it inline gets an explanation rather than "unknown command".

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.CommandsProvider.RegisterFormatConversion<JToken>(converter, "json");
```

## Settings

Per-boundary, and they do not carry to the next `convert`: `attributePrefix`, `textProperty`,
`namespacePrefix`, `arrayItemName`, `arrayHandling`, `nullRepresentation`, `nameSanitization`,
`inferTypes`, `cdataAsText`, `flattenAnchors`. Each exists where the formats genuinely disagree.

Conversion output is the canonical document shape TLio's own adapters address, so the commands
after a boundary land where the paths say they do.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
