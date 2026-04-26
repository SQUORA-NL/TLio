# Contract: ConvertCommand

**Project**: `FormatConverter.TLio`  
**Namespace**: `FormatConverter.TLio`

## Purpose

A TLio command that signals a format boundary in a multi-format script. When `MultiFormatScriptRunner` processes a script, it intercepts every `ConvertCommand` to split the script into sections and manage context switching. Script authors use this command to transition the working document between formats in a single script execution.

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
| `settings` | object | no | all defaults | Per-boundary adapter options (see ConversionSettings.md) |
| `settings.textProperty` | string | no | `"#text"` | Key for XML text content when serialising to non-XML |
| `settings.attributePrefix` | string | no | `"@"` | Prefix prepended to XML attribute names |
| `settings.namespacePrefix` | string | no | `"xmlns:"` | Prefix for XML namespace declaration keys |
| `settings.inferTypes` | boolean | no | `false` | Coerce untyped strings to typed scalars |
| `settings.cdataAsText` | boolean | no | `false` | Treat CDATA sections as plain text |
| `settings.flattenAnchors` | boolean | no | `true` | Dereference YAML anchors/aliases inline |

## Behaviour

- **Entire document converted**: The full current working document is converted to the target format. No partial conversion; no residual data in the original format.
- **Error timing**: Format-not-registered errors are raised at execution time when the boundary is reached, not at script-load time.
- **Settings scope**: Settings apply only to this boundary step. They do not carry forward to later `convert` commands.

## Examples

### Minimal (all defaults)
```json
{ "command": "convert", "to": "json" }
```

### With XML target settings
```json
{
  "command": "convert",
  "to": "xml",
  "settings": {
    "attributePrefix": "@",
    "textProperty": "#text",
    "inferTypes": false
  }
}
```

### Full multi-format pipeline script
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
// In host setup — follows TLio command registration conventions
services.AddFormatConverterTLio(fc =>
{
    fc.Register(new JsonFormatAdapter());
    fc.Register(new XmlFormatAdapter());
    fc.Register(new YamlFormatAdapter());
});
```
