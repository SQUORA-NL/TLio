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
| delimiter | string | `"."` | Key separator used during the original `flatten`. Only used for the *best-effort* path (no metadata found) — with metadata present, the delimiter recorded in the metadata is used instead. |
| metadataPath | string | `""` | Where to look for the metadata object. The default, `""`, looks for `metadataKey` **as a property directly on the node being restored** — not at the document root. `flatten`'s own default writes metadata to `"$"` (the document root, alongside the flattened node), so restoring flatten's default output requires setting this to `"$"` (or wherever `flattenSettings.metadataPath` pointed) to match. |
| metadataKey | string | `"_flattenMetadata"` | Key name where metadata is embedded. Must match `flattenSettings.metadataKey` if it was customized — though once metadata is found, the key name embedded *inside* the metadata itself is also honored. |
| strictMode | boolean | `false` | Fail (rather than fall back to best-effort inference) if metadata is absent. |
| removeMetadata | boolean | `true` | Delete the metadata node after restoration. |
| arrayDelimiter | string | `"."` | Present on `RestoreSettings` but not read by the current `restore` implementation — accepted, no effect. With metadata present, array-vs-object shape comes from the metadata's recorded structure, not from a separate array delimiter. |
| useJsonPathColumn / jsonPathColumn | boolean / string | `false` / `"_jsonpath"` | Validated (a non-empty `jsonPathColumn` is required when `useJsonPathColumn` is `true`) but not otherwise read by the current `restore` implementation. |

**Functions in the value**: — no value field  
**Functions in the path**: — not resolved here; resolve it in a preceding step

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{ "command": "restore", "path": "$", "restoreSettings": { "metadataPath": "$.meta", "removeMetadata": true } }
```

## Verified example

A flatten → restore round trip: `flatten` collapses `item` into delimited keys and (by leaving
`flattenSettings.metadataPath` at its default) writes `_flattenMetadata` to the document root;
`restore` reads it back from `"$"` and rebuilds the original shape exactly, including the nested
object and the numeric type of `value`.

```json
{
  "input": {
    "item": { "name": "test", "value": 42, "nested": { "x": 1 } }
  },
  "script": [
    { "command": "flatten", "path": "$.item" },
    {
      "command": "restore",
      "path": "$.item",
      "restoreSettings": { "metadataPath": "$", "removeMetadata": true }
    }
  ],
  "result": {
    "item": { "name": "test", "value": 42, "nested": { "x": 1 } }
  }
}
```

The intermediate (post-flatten, pre-restore) document is not shown above — the test asserts
`data["_flattenMetadata"]` is present after `flatten` and gone after `restore` with
`removeMetadata: true`, and that the final `item` deep-equals the original.

Verified by:
`TLio.UnitTests/CommandsTests/ETLTests/FlattenRestoreTests.cs::Flatten_ThenRestore_RoundTrip`.

A second round-trip fixture, `RoundTrip_ArrayOfObjects` in
`TLio.UnitTests/CommandsTests/ETLTests/FlattenRestoreDepthTests.cs`, is the one to read for how
`restore` distinguishes an array of objects from a plain object using the metadata's recorded
structure — the behavior `arrayDelimiter` looks like it should control but, per the Settings
table above, does not.

## When to use

- Completing a flatten→transform→restore round-trip: the data was flattened earlier in the same pipeline (metadata is written by default), and you need to recover the original nested shape after the transform. Point `restoreSettings.metadataPath` at wherever `flattenSettings.metadataPath` wrote it.
- Deserialising data that `flatten` produced with metadata intact (i.e., `flattenSettings.metadataPath` left non-empty) — the metadata is the contract that makes reconstruction exact.
- Cleaning up the pipeline output — `removeMetadata` defaults to `true`, so the metadata node is gone from the final document unless you explicitly set it to `false`.

## When NOT to use

- The flat data did not originate from a `flatten` command — heuristic inference (non-strict mode) produces a best-guess nested structure based on key patterns, but it cannot recover arrays correctly and may produce an object shape that differs from the original.
- You want to reconstruct a specific subset of fields — restore rebuilds the entire flattened object; use `set`/`copy` to place individual values instead.
- The flat data comes from an external system (e.g., environment variables, a config file) that was never flattened by TLio — the metadata the command depends on does not exist, and inference will be unreliable.
- `strictMode` is false and you are relying on correct array reconstruction — without metadata, arrays cannot be distinguished from similarly keyed object properties.

## Common mistakes

- **Leaving `metadataPath` at its default on the restore side**: `restoreSettings.metadataPath` defaults to `""` (look on the node itself), but `flatten` by default writes metadata to `"$"` (the document root). Unless you set `restoreSettings.metadataPath` to match wherever `flatten` wrote it, restore silently falls back to best-effort inference — arrays come back as objects and ordering is not guaranteed. Enable `strictMode: true` to surface this as an error instead of silent corruption.
- **Mismatched `metadataPath`**: the path written during flatten and the path read during restore must be identical, whatever value you choose. A typo silently falls back to heuristics.
- **Mismatched delimiter**: if metadata is absent and restore's best-effort `delimiter` differs from the one flatten used, key splitting will be wrong and the structure will be malformed. When metadata *is* present this is not an issue — the delimiter recorded in the metadata is used automatically.
- **Not removing metadata**: `removeMetadata` defaults to `true`; explicitly setting it to `false` (e.g., to allow a second restore of the same shape) leaves the metadata property in the result document.
- **Assuming restore works on arbitrary flat data**: flat data from external sources does not carry TLio flatten metadata. Restore is not a generic flat-to-nested converter.

## Failure modes and what the trace tells you

- **Arrays restored as objects**: trace shows a successful restore but array fields are plain objects with numeric keys. Metadata was not found at `metadataPath` — re-run with `restoreSettings.metadataPath` matching wherever `flatten` wrote it.
- **`strictMode` error**: trace reports that metadata was not found at `metadataPath`. The metadata path in `restoreSettings` does not match where flatten wrote it, or flatten did not run.
- **Structure depth is wrong**: trace shows top-level keys that still contain dots. This only happens in best-effort mode (no metadata found) with a `delimiter` in `restoreSettings` that does not match the one used in `flattenSettings`.
- **ETL extension not registered**: trace reports an unknown command `restore`. Ensure `RegisterETL<TNode>()` is called on the commands provider at startup.
