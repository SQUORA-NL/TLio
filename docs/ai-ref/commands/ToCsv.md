# tocsv

> Converts an object or array of objects to a CSV-formatted string and writes it to
> the target node.

> **ETL extension**: requires `options.CommandsProvider.RegisterETL<TNode>()` in addition
> to `ParseOptions<TNode>.CreateDefault()`.

## Syntax

```json
{ "command": "tocsv", "path": "$.records" }
```

With settings:

```json
{ "command": "tocsv", "path": "$.data", "settings": { "delimiter": ";", "includeHeaders": true } }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the node(s) to convert (object or array of objects). |
| settings | object | no | — | CSV formatting configuration (see Settings below). |

### Settings object

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| delimiter | string | `","` | Column separator character. |
| includeHeaders | boolean | true | Emit a header row with field names. |
| booleanFormat | string | `"true/false"` | How boolean values are rendered (e.g., `"1/0"`, `"yes/no"`). |
| nullValueRepresentation | string | `""` | String used for null values. |
| quoteAllFields | boolean | false | Force quoting of every field, not just fields with special characters. |
| escapeQuoteChar | string | `"\""` | Character used to escape quotes within field values. |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{ "command": "tocsv", "path": "$.rows", "settings": { "delimiter": ",", "includeHeaders": true } }
```
