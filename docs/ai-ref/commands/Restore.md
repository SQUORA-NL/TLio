# restore

> Reconstructs a nested object from data previously flattened by `flatten`. Uses stored
> metadata when available; falls back to delimiter-based inference in non-strict mode.

> **ETL extension**: requires `options.CommandsProvider.RegisterETL<TNode>()` in addition
> to `ParseOptions<TNode>.CreateDefault()`.

## Syntax

```json
{ "command": "restore", "path": "$" }
```

With settings:

```json
{ "command": "restore", "path": "$", "restoreSettings": { "metadataPath": "$.meta", "removeMetadata": true } }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the flattened object node(s) to restore. |
| restoreSettings | object | no | — | Restoration configuration (see Settings below). |

### Settings object

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| delimiter | string | `"."` | Key separator used during the original `flatten`. |
| metadataPath | string | — | Path to the metadata written by `flatten`. Enables exact reconstruction. |
| metadataKey | string | — | Key name where metadata is embedded inside the flattened object. |
| strictMode | boolean | false | Fail if metadata is absent; otherwise use best-effort inference. |
| removeMetadata | boolean | false | Delete the metadata node after restoration. |

**Supports functions**: ❌

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{ "command": "restore", "path": "$", "restoreSettings": { "metadataPath": "$.meta", "removeMetadata": true } }
```
