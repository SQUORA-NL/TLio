# flatten

> Flattens a nested object to a single-level object with dot-separated keys, and
> optionally stores metadata for reconstruction with `restore`.

> **ETL extension**: requires `options.CommandsProvider.RegisterETL<TNode>()` in addition
> to `ParseOptions<TNode>.CreateDefault()`.

## Syntax

```json
{ "command": "flatten", "path": "$.nested" }
```

With settings:

```json
{ "command": "flatten", "path": "$.nested", "settings": { "delimiter": "_", "maxDepth": 3 } }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the object node(s) to flatten. |
| settings | object | no | — | Flattening configuration (see Settings below). |

### Settings object

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| delimiter | string | `"."` | Key separator between levels. |
| maxDepth | integer | unlimited | Maximum nesting depth to flatten. |
| excludePaths | array of string | — | Dot-paths to exclude from flattening. |
| metadataPath | string | — | Where to write flattening metadata (enables `restore`). |
| includeArrayIndices | boolean | false | Whether to include array indices in flattened keys. |
| preserveTypes | boolean | false | Preserve type information alongside values. |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "flatten", "path": "$", "settings": { "delimiter": ".", "metadataPath": "$.meta" } },
  { "command": "restore", "path": "$", "settings": { "metadataPath": "$.meta" } }
]
```
