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

**Functions in the value**: — no value field  
**Functions in the path**: — not resolved here; resolve it in a preceding step

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{ "command": "restore", "path": "$", "restoreSettings": { "metadataPath": "$.meta", "removeMetadata": true } }
```

## When to use

- Completing a flatten→transform→restore round-trip: the data was flattened earlier in the same pipeline with `metadataPath` set, and you need to recover the original nested shape after the transform.
- Deserialising data that was serialised by `flatten` with `IncludeMetadata=true` — the metadata is the contract that makes reconstruction exact.
- Cleaning up the pipeline output by setting `removeMetadata: true` so the metadata node is not present in the final document.

## When NOT to use

- The flat data did not originate from a `flatten` command — heuristic inference (non-strict mode) produces a best-guess nested structure based on key patterns, but it cannot recover arrays correctly and may produce an object shape that differs from the original.
- You want to reconstruct a specific subset of fields — restore rebuilds the entire flattened object; use `set`/`copy` to place individual values instead.
- The flat data comes from an external system (e.g., environment variables, a config file) that was never flattened by TLio — the metadata the command depends on does not exist, and inference will be unreliable.
- `strictMode` is false and you are relying on correct array reconstruction — without metadata, arrays cannot be distinguished from similarly keyed object properties.

## Common mistakes

- **Missing metadata**: calling restore without a corresponding `flatten` that had `metadataPath` set. The result looks plausible but arrays are reconstructed as objects and ordering is not guaranteed. Enable `strictMode: true` to surface this as an error rather than silent corruption.
- **Mismatched `metadataPath`**: the path written during flatten and the path read during restore must be identical. A typo silently falls back to heuristics.
- **Mismatched delimiter**: if restore uses a different `delimiter` than flatten used, key splitting will be wrong and the structure will be malformed. Always keep the delimiter consistent across the flatten/restore pair.
- **Not removing metadata**: omitting `removeMetadata: true` when the final output should not contain the metadata node. The meta property will appear in the result document.
- **Assuming restore works on arbitrary flat data**: flat data from external sources does not carry TLio flatten metadata. Restore is not a generic flat-to-nested converter.

## Failure modes and what the trace tells you

- **Arrays restored as objects**: trace shows a successful restore but array fields are plain objects with numeric keys. Metadata was absent — re-run the flatten step with `metadataPath` set.
- **`strictMode` error**: trace reports that metadata was not found at `metadataPath`. The metadata path in `restoreSettings` does not match where flatten wrote it, or flatten did not run.
- **Structure depth is wrong**: trace shows top-level keys that still contain dots. The `delimiter` in `restoreSettings` does not match the one used in `flattenSettings`.
- **ETL extension not registered**: trace reports an unknown command `restore`. Ensure `RegisterETL<TNode>()` is called on the commands provider at startup.
