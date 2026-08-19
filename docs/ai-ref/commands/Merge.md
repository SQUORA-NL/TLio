# merge

> Deep-merges the node(s) at `fromPath` (source) into the node(s) at `toPath`
> (destination). Objects are merged recursively; arrays follow `arrayMergeMode`
> or, when configured, per-array key matching from `settings`.

## Syntax

```json
{ "command": "merge", "fromPath": "$.source", "toPath": "$.target" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Selects the source node(s) to merge from. Alias: `path`. |
| toPath | string | yes | — | Selects the destination node(s) to merge into. Alias: `targetPath`. |
| arrayMergeMode | string | no | `"concat"` | How arrays are merged when no `keyPaths` apply: `"concat"` appends source to target; `"replace"` overwrites target; `"mergeByKey"` appends only items that are not already present. |
| settings | object | no | — | Fine-grained merge configuration (see below). |

### `settings`

| Sub-option | Type | Default | Description |
|------------|------|---------|-------------|
| strategy | string | `"fullMerge"` | `"fullMerge"` adds new properties and overwrites existing ones; `"onlyStructure"` adds missing properties but never changes an existing value; `"onlyValues"` updates existing properties but never adds new ones. |
| arraySettings | array | `[]` | Per-array configuration; each entry applies to the target array whose path matches `arrayPath`. |
| arraySettings[].arrayPath | string | — | Path of the **target** array, e.g. `"$.target.items"` (JSON/YAML) or `"/target/items"` (XML). The root indicator is ignored when comparing, so `"$.target.items"`, `"target.items"` and `"/target/items"` are equivalent. |
| arraySettings[].keyPaths | string[] | `[]` | Field paths that identify an array element. Source items whose key values match a target item are merged into it; unmatched items are appended. Plain (`"key.id"`) and `@` notation (`"@.key.id"`) are both accepted. |
| arraySettings[].uniqueItemsWithoutKeys | bool | `false` | When no `keyPaths` are set, skip source items that already exist (deep-equal) in the target array. |
| matchSettings.keyPaths | string[] | `[]` | Objects are only merged when all these fields hold equal values on both source and target. Applied at every object level, so a nested object with a different key is left untouched. |

**Functions in the value**: — no value field  
**Functions in the path**: ✅ `=indirect()` in `path` and `targetPath`

## Formats

Works with all adapters (JSON — Newtonsoft and System.Text.Json — XML and YAML).
Path syntax differs per adapter — see [overview.md](../overview.md).

XML has no native array type: an element whose children all share the same name
looks like both an object and an array. Array semantics (key matching,
deduplication) are therefore only applied to such an element when an
`arraySettings.arrayPath` entry names its path; otherwise it is merged as an
object, as before.

## Examples

Plain merges:

```json
[
  { "command": "merge", "fromPath": "$.patch", "toPath": "$.document" },
  { "command": "merge", "fromPath": "$.newItems", "toPath": "$.list", "arrayMergeMode": "replace" }
]
```

Key-based array merge — items with a matching `id` are updated in place, new
items are appended:

```json
[{
  "command": "merge",
  "fromPath": "$.incoming",
  "toPath": "$.current",
  "settings": {
    "arraySettings": [
      { "arrayPath": "$.current.items", "keyPaths": ["id"] }
    ]
  }
}]
```

```jsonc
// $.incoming.items: [ { "id": 1, "qty": 5 }, { "id": 3, "qty": 1 } ]
// $.current.items:  [ { "id": 1, "qty": 2 }, { "id": 2, "qty": 7 } ]
// result:           [ { "id": 1, "qty": 5 }, { "id": 2, "qty": 7 }, { "id": 3, "qty": 1 } ]
```

Composite and nested keys:

```json
{
  "arraySettings": [
    { "arrayPath": "$.current.rows", "keyPaths": ["@.key.id", "region"] }
  ]
}
```

Append-without-duplicates for arrays of primitives or plain objects:

```json
{
  "arraySettings": [
    { "arrayPath": "$.target.tags", "uniqueItemsWithoutKeys": true }
  ]
}
```

Fill in gaps without overwriting anything that is already set:

```json
{ "command": "merge", "fromPath": "$.defaults", "toPath": "$.config",
  "settings": { "strategy": "onlyStructure" } }
```

Update known fields only, never introducing new ones:

```json
{ "command": "merge", "fromPath": "$.update", "toPath": "$.record",
  "settings": { "strategy": "onlyValues" } }
```

Only merge when the objects describe the same entity:

```json
{ "command": "merge", "fromPath": "$.update", "toPath": "$.record",
  "settings": { "matchSettings": { "keyPaths": ["id"] } } }
```

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Merge().From("$.patch").To("$.document");

var keyed = new TLioScript<JToken>()
    .Merge().From("$.incoming")
        .WithArrayKeys("$.current.items", "id")
        .WithStrategy(MergeSettings.StrategyFullMerge)
    .To("$.current");
```

`WithSettings(MergeSettings)`, `WithArrayMergeMode(...)`, `WithUniqueItems(path)`
and `WithMatchKeys(...)` are available on the same builder.

## When to use

- Two partially-overlapping objects need to be combined without replacing the entire destination — e.g., applying a config patch onto a base config.
- A secondary object should contribute its fields to a primary object, keeping fields that exist only in the primary intact.
- Merging into root (`$.`) when you want to fold a sub-object's properties up to the top level additively.
- Collections must be reconciled by identity: `arraySettings[].keyPaths` updates matching elements and appends the rest.
- Defaults must be applied without clobbering explicit values (`onlyStructure`), or an update must not introduce unknown fields (`onlyValues`).

## When NOT to use

- You need to copy or move specific fields — use `copy` or `set` instead; merge applies to entire object trees.
- The source is a primitive (string, number, boolean) and the target is an object or array — the target is replaced wholesale rather than merged.
- You want to overwrite the destination entirely — use `copy` or `set`; merge preserves destination-only fields.
- You need to join a collection against a lookup table and project fields — use `resolve`.

## Common mistakes

- **Forgetting `arrayMergeMode`**: the default is `"concat"`, which appends source arrays to destination arrays. If the destination already has the data, this doubles entries. Set `"replace"` when the source array is the authoritative version, or configure `keyPaths` / `uniqueItemsWithoutKeys` for element-level control.
- **Pointing `arrayPath` at the source array**: `arrayPath` always names the **target** array (the one being merged into). A path that matches nothing silently falls back to `arrayMergeMode`.
- **Expecting `keyPaths` to reorder**: matched elements are updated in place and unmatched ones are appended at the end — the target order is never rearranged.
- **Missing keys**: an element that lacks a configured key field never matches an element that has it, so it is appended. Elements missing the key on both sides are treated as the same element.
- **Merging primitives**: if `fromPath` resolves to a string or number and the target is an object, the target node is replaced by that primitive.
- **Merge vs. copy confusion**: merge into root (`$.`) resembles a copy-to-root but preserves existing root fields. A plain `copy` overwrites. Choose based on whether existing destination fields must be kept.
- **Path aliases**: `fromPath` has an alias `path`; `toPath` has an alias `targetPath`. Mixing aliases across commands in the same script is fine but can cause confusion when reading the trace.
- **Same path twice**: `fromPath` and `toPath` may not be identical — the command fails validation.

## Failure modes and what the trace tells you

- **No-op merge**: trace shows the merge executed but destination is unchanged — check that `fromPath` actually resolves to a node, and that `matchSettings.keyPaths` (if set) actually match.
- **Doubled array entries**: trace shows the merge succeeded; inspect the destination array length before and after — `arrayMergeMode` is defaulting to `"concat"` and the data already existed at the destination. Add `keyPaths` or `uniqueItemsWithoutKeys`.
- **Source path not found**: trace reports a no-op with an empty match for `fromPath`. Verify the path against the current document state at that pipeline step.
- **Destination path not found**: trace reports a no-op for `toPath`. The destination object must already exist; use `set` first to create it if needed.
- **Validation failure**: `fromPath` or `toPath` missing, or both pointing at the same path — the trace records a failure entry and the script stops.
- **Strategy in the trace**: successful merges log `[strategy=…]`, which tells you whether `onlyStructure` / `onlyValues` was actually picked up from the script.
