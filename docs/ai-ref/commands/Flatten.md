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
{ "command": "flatten", "path": "$.nested", "flattenSettings": { "delimiter": "_", "maxDepth": 3 } }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the object node(s) to flatten. |
| flattenSettings | object | no | — | Flattening configuration (see Settings below). |

### Settings object

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| delimiter | string | `"."` | Key separator between levels. |
| maxDepth | integer | unlimited | Maximum nesting depth to flatten. |
| excludePaths | array of string | — | Dot-paths to exclude from flattening. |
| metadataPath | string | — | Where to write flattening metadata (enables `restore`). |
| includeArrayIndices | boolean | false | Whether to include array indices in flattened keys. |
| preserveTypes | boolean | false | Preserve type information alongside values. |

**Functions in the value**: — no value field  
**Functions in the path**: — not resolved here; resolve it in a preceding step

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "flatten", "path": "$", "flattenSettings": { "delimiter": ".", "metadataPath": "$.meta" } },
  { "command": "restore", "path": "$", "restoreSettings": { "metadataPath": "$.meta" } }
]
```

## When to use

- Sending data to a key-value store, environment variable layer, or any system that only accepts flat string maps.
- Preprocessing before `tocsv` when the source objects are nested and you need flat columns.
- Preparing a snapshot for structural comparison — flattened keys make diffing trivial.
- As the first step of a flatten→transform→restore round-trip: always pair with `metadataPath` set.
- Normalising deeply nested configs or responses before further pipeline steps that expect flat paths.

## When NOT to use

- The data contains arrays and you need to preserve their identity as ordered sequences — `flatten` converts array items into numbered keys (`items.0`, `items.1`) and `restore` can only recover them if `IncludeMetadata` was set.
- You plan to call `restore` later but are not setting `metadataPath` — without metadata, array structure is lost and objects that shared the same delimiter character in their key names become ambiguous.
- The goal is to extract specific fields — use `copy` or `set` with explicit paths; flatten produces all keys indiscriminately.
- The output format requires nested objects (e.g., another JSON API) — flatten is a one-way transformation unless restore follows.

## Common mistakes

- **Omitting `metadataPath` before restore**: the single most common error. If you intend to call `restore`, always include `"metadataPath": "$.meta"` (or another stable path) in `flattenSettings`. Without it, array items are indistinguishable from object properties and the round-trip will silently produce a wrong structure.
- **Delimiter collision**: if object keys already contain the delimiter character (e.g., keys with dots and `delimiter: "."`), the flattened key is ambiguous. Use a delimiter that cannot appear in your key names (e.g., `"__"` or `"|"`).
- **Expecting `preserveTypes` by default**: values are stringified unless `"preserveTypes": true` is set. Numeric and boolean fields will be strings after flatten unless you enable this.
- **Flattening the whole document when only a subtree is needed**: use `path` to target just the nested sub-object; flattening `$` promotes all resulting keys to the document root, which can collide with existing top-level fields.
- **`maxDepth` silently leaving structure**: if `maxDepth` is set, nodes below that depth stay nested. The output is partially flat; downstream consumers expecting fully flat keys will encounter nested objects.

## Failure modes and what the trace tells you

- **Output is partially nested**: `maxDepth` is in effect, or the path resolved to a primitive rather than an object. Trace will show the resulting node; check that it is a flat key-value map.
- **Restore produces wrong structure**: `metadataPath` was not set during flatten. Trace for the restore step will show it using heuristic inference. Re-run flatten with `metadataPath` set.
- **Key collisions at root**: flattening to `$` when the document already has top-level keys matching a flattened key. The trace will not report this as an error — the original top-level key is silently overwritten.
- **ETL extension not registered**: trace reports an unknown command `flatten`. Ensure `RegisterETL<TNode>()` is called on the commands provider at startup.
