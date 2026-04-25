# ConvertCommand

**Package**: `FormatConverter.TLio`  
**Namespace**: `FormatConverter.TLio`  
**Command name**: `convert`

## Purpose

Signals a format boundary in a multi-format TLio script.
`MultiFormatScriptRunner` intercepts every `ConvertCommand` to split the script into sections
and manage format switching. Script authors use this command to transition the working document
between formats within a single script execution.

## JSON Schema

```json
{
  "command": "convert",
  "to": "<formatId>",
  "settings": {
    "textProperty":    "#text",
    "attributePrefix": "@",
    "namespacePrefix": "xmlns:",
    "inferTypes":      false,
    "cdataAsText":     false,
    "flattenAnchors":  true
  }
}
```

## Fields

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `command` | string | yes | — | Literal `"convert"` |
| `to` | string | yes | — | Target format ID (`"json"`, `"xml"`, `"yaml"`, or any registered format) |
| `settings` | object | no | all defaults | Per-boundary adapter options |
| `settings.textProperty` | string | no | `"#text"` | Key for XML text content when serialising to non-XML |
| `settings.attributePrefix` | string | no | `"@"` | Prefix prepended to XML attribute names |
| `settings.namespacePrefix` | string | no | `"xmlns:"` | Prefix for XML namespace declaration keys |
| `settings.inferTypes` | boolean | no | `false` | Coerce untyped strings to typed scalars |
| `settings.cdataAsText` | boolean | no | `false` | Treat CDATA sections as plain text |
| `settings.flattenAnchors` | boolean | no | `true` | Dereference YAML anchors/aliases inline |

## Behaviour

- **Entire document converted**: The full working document is converted to the target format.
- **Error timing**: Unregistered-format errors are raised at execution time, not at script-load time.
- **Settings scope**: Settings apply only to the boundary step they appear on.
- **No-op outside runner**: If executed directly by the standard TLio engine, a warning is logged
  and the document is returned unchanged.

## Examples

### Minimal

```json
{ "command": "convert", "to": "json" }
```

### With settings

```json
{
  "command": "convert",
  "to": "xml",
  "settings": {
    "attributePrefix": "@",
    "textProperty": "#text"
  }
}
```

### Full multi-format pipeline

```json
[
  { "command": "set", "path": "/person/name", "value": "Alice" },
  { "command": "convert", "to": "json", "settings": { "textProperty": "#text" } },
  { "command": "set", "path": "$.person.name", "value": "Bob" },
  { "command": "convert", "to": "yaml" },
  { "command": "set", "path": "person.name", "value": "Carol" }
]
```

## Registration

```csharp
var commandsProvider = new CommandsProvider<TNode>();
commandsProvider.Register("convert", () => new ConvertCommand<TNode>());
```
