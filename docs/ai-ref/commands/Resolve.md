# resolve

> Looks up matching entries in a reference collection and writes derived values to
> the target document. Supports relative `@.property` paths for writing results.

> **ETL extension**: requires `options.CommandsProvider.RegisterETL<TNode>()` in addition
> to `ParseOptions<TNode>.CreateDefault()`.

## Syntax

```json
{
  "command": "resolve",
  "path": "$.orders[*]",
  "settings": [
    {
      "referencesCollectionPath": "$.products",
      "resolveKeys": [
        { "keyPath": "@.productId", "referenceKeyPath": "$.id" }
      ],
      "values": [
        { "targetPath": "@.productName", "value": "=fetch($.name)" }
      ]
    }
  ]
}
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the nodes to resolve (the "left side"). |
| settings | array | yes | — | Array of resolve setting objects (see Settings below). |

### Resolve setting object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| referencesCollectionPath | string | yes | Path to the reference collection to search. |
| resolveKeys | array | yes | Array of `{keyPath, referenceKeyPath}` join conditions. |
| values | array | yes | Array of `{targetPath, value}` to write when a match is found. |

- `keyPath`: path relative to the current node (`@.property`)
- `referenceKeyPath`: path in the reference entry (absolute or `$.property`)
- `targetPath`: where to write the result — `@.property` writes relative to current node

**Supports functions**: ❌

## Notes

- The JSON key for the settings array is `"resolveSettings"` (the `settings` field name in JSON).

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).
