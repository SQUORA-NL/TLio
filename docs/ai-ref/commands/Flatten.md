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
| delimiter | string | `"."` | Separator joining a parent key to a child key or array index. |
| arrayDelimiter | string | `"."` | Separator used before an array index instead of `delimiter`, when `includeArrayIndices` is `true`. The index is always part of the key either way — this only chooses which character precedes it. |
| includeArrayIndices | boolean | `true` | `true` joins array positions with `arrayDelimiter`; `false` joins them with the plain `delimiter`. Both default to `"."`, so the difference is invisible unless you set them apart. |
| metadataPath | string | `"$"` | Where the metadata object is written. The default, `"$"`, writes it to the **document root** — as a sibling of the flattened node, not inside it. Set to `""` to skip writing metadata (then `restore` can only fall back to best-effort delimiter inference). |
| metadataKey | string | `"_flattenMetadata"` | Property name the metadata is stored under at `metadataPath`. |
| preserveTypes | boolean | `true` | Adds a companion `<key><typeIndicator>` property (`"Integer"`, `"Float"`, `"Boolean"`, `"String"`, `"Null"`) next to each scalar key. Scalars keep their own native type regardless of this setting — it only adds or omits the type-name sidecar, it does not stringify values. |
| typeIndicator | string | `"_type"` | Suffix appended to a key to form its companion type-name property. |
| maxDepth | integer | `-1` (unlimited) | Maximum nesting depth to walk. At the cutoff, the remaining subtree is serialized to a single string value instead of being flattened further. |
| excludePaths | array of string | `[]` | Dot-paths to exclude from flattening, matched by prefix. |
| includePaths | array of string | `[]` | When non-empty, only dot-paths starting with one of these are flattened; everything else is skipped. |
| includeJsonPath / jsonPathColumn | boolean / string | `false` / `"_jsonpath"` | Present on `FlattenSettings` for forward compatibility but not read by the current `flatten` implementation — accepted, no effect on output. |

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

## Verified example

```json
{
  "input": { "data": { "user": { "name": "Alice", "age": 30 } } },
  "script": [
    {
      "command": "flatten",
      "path": "$.data",
      "flattenSettings": { "delimiter": "_", "preserveTypes": false, "metadataPath": "" }
    }
  ],
  "result": { "data": { "user_name": "Alice", "user_age": 30 } }
}
```

`metadataPath: ""` turns off metadata for this example — without it, the result would also carry a
top-level `_flattenMetadata` property (see the Settings table above).

Verified by: `TLio.UnitTests/Fixtures/Flatten/01-flatten-with-settings/fixture.json`, run through
the real `ScriptEngine` (not direct command construction) by
`TLio.UnitTests/EtlFixtureTests.cs::Flatten`.

## When to use

- Sending data to a key-value store, environment variable layer, or any system that only accepts flat string maps.
- Preprocessing before `tocsv` when the source objects are nested and you need flat columns.
- Preparing a snapshot for structural comparison — flattened keys make diffing trivial.
- As the first step of a flatten→transform→restore round-trip: always pair with `metadataPath` set.
- Normalising deeply nested configs or responses before further pipeline steps that expect flat paths.

## When NOT to use

- The data contains arrays and you need to preserve their identity as ordered sequences — `flatten` converts array items into numbered keys (`items.0`, `items.1`) and `restore` can only tell an array back from an object using the metadata `flatten` wrote (see `metadataPath` above).
- You plan to call `restore` later but have set `flattenSettings.metadataPath` to `""` — that disables metadata entirely, so array structure is lost and objects that shared the delimiter character in their key names become ambiguous. (Leaving `metadataPath` at its default, `"$"`, keeps metadata on.)
- The goal is to extract specific fields — use `copy` or `set` with explicit paths; flatten produces all keys indiscriminately.
- The output format requires nested objects (e.g., another JSON API) — flatten is a one-way transformation unless restore follows.

## Common mistakes

- **Mismatched `metadataPath` between `flatten` and `restore`**: metadata is written by default (`metadataPath: "$"`, the document root), but `restore`'s own default (`metadataPath: ""`) looks for it **on the node being restored itself**, not at the root. The two do not line up out of the box — pass the same `metadataPath` (commonly `"$"`) to both commands, or restore silently falls back to best-effort inference. See [Restore.md](Restore.md).
- **Delimiter collision**: if object keys already contain the delimiter character (e.g., keys with dots and `delimiter: "."`), the flattened key is ambiguous. Use a delimiter that cannot appear in your key names (e.g., `"__"` or `"|"`).
- **Expecting `preserveTypes: false` to stringify values**: it does not — scalars keep their own type either way. `preserveTypes` only adds (or omits) a companion `<key>_type` property; it never converts a number or boolean into a string.
- **Flattening the whole document when only a subtree is needed**: use `path` to target just the nested sub-object; flattening `$` promotes all resulting keys to the document root, which can collide with existing top-level fields (and with the metadata property written there by default).
- **`maxDepth` silently leaving structure**: if `maxDepth` is set, nodes below that depth stay nested. The output is partially flat; downstream consumers expecting fully flat keys will encounter nested objects.

## Failure modes and what the trace tells you

- **Output is partially nested**: `maxDepth` is in effect, or the path resolved to a primitive rather than an object. Trace will show the resulting node; check that it is a flat key-value map.
- **Restore produces wrong structure**: `flattenSettings.metadataPath` and `restoreSettings.metadataPath` do not point at the same place (or `flatten`'s was explicitly set to `""`). Trace for the restore step will show it using heuristic inference. Re-run with matching, non-empty `metadataPath` values on both commands.
- **Key collisions at root**: flattening to `$` when the document already has top-level keys matching a flattened key. The trace will not report this as an error — the original top-level key is silently overwritten.
- **ETL extension not registered**: trace reports an unknown command `flatten`. Ensure `RegisterETL<TNode>()` is called on the commands provider at startup.
