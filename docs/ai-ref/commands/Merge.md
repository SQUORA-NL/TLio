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

A null *source* is one place XML cannot follow JSON/YAML exactly: merging `null` into an
existing target overwrites it (a scalar replaces) in JSON and YAML, but XML reads an empty
`<s/>` source element as an empty *object*, and merging an object with no properties is a
no-op — so the XML target is left unchanged instead of nulled out. This is a deliberate,
format-driven divergence, not a bug — see `docs/behaviour-decisions.md` section E5 for the full
reasoning. Pinned by `SystemTextJsonNullNodeTests.Merge_OfANullSource_OverwritesTheTargetLikeAnyScalar`
(JSON side); merging a *non-null* source into a `null` target, by contrast, behaves the same in
every format — that case is `TLio.Parity.Tests/Fixtures/Merge/11-merge-into-null-node`.

## Verified example

Each snippet below is the literal `script` (and the relevant slice of `input`/`result`) from a
passing fixture under `TLio.UnitTests/Fixtures/Merge/<NN-name>/fixture.json` — parity-mirrored
one-for-one under `TLio.Parity.Tests/Fixtures/Merge/<NN-name>` (JSON, XML and YAML all run the
same fixture through each format's own script notation).

**Plain object merge** — `01-merge-objects`: `{ "target": {"a":1}, "source": {"b":2} }` →
`{ "command": "merge", "path": "$.source", "targetPath": "$.target" }` → target becomes
`{ "a": 1, "b": 2 }`.

**`fromPath`/`toPath` aliases** — `03-merge-from-to-path`: the same command written as
`{ "command": "merge", "fromPath": "$.src", "toPath": "$.dst" }`.

**`arrayMergeMode: "concat"` (the default)** — `02-merge-arrays-concat`: `target: [1,2]` merged
with `source: [3,4]` → `[1, 2, 3, 4]`.

**Key-based array merge** — `04-merge-arrays-by-key`, items with a matching `id` are updated in
place, new items are appended:

```json
{
  "command": "merge", "path": "$.source", "targetPath": "$.target",
  "settings": { "arraySettings": [ { "arrayPath": "$.target.items", "keyPaths": ["id"] } ] }
}
```

`source.items: [{"id":1,"name":"one-updated"}, {"id":3,"name":"three"}]` merged into
`target.items: [{"id":1,"name":"one"}, {"id":2,"name":"two"}]` produces
`[{"id":1,"name":"one-updated"}, {"id":2,"name":"two"}, {"id":3,"name":"three"}]` — id `1`
updated in place, id `2` untouched, id `3` appended.

**Nested key path** — `10-merge-nested-key-paths`: `keyPaths: ["@.key.id"]` matches array items
by a nested field (`item.key.id`) rather than a top-level one.

**`uniqueItemsWithoutKeys`** — `07-merge-unique-items-without-keys`: `target.tags: ["a","c"]`
plus `source.tags: ["a","b","c"]` (with `arraySettings: [{ "arrayPath": "$.target.tags",
"uniqueItemsWithoutKeys": true }]`) → `["a", "c", "b"]` — only the genuinely new `"b"` is
appended.

**`arrayMergeMode: "mergeByKey"` (no `keyPaths` configured)** — `09-merge-array-mode-merge-by-key`:
falls back to whole-item matching, same effect as `uniqueItemsWithoutKeys` above.

**`strategy: "onlyStructure"`** — `05-merge-only-structure`: `target: {"a":"keep-me",
"b":"untouched"}` merged with `source: {"a":"from-source","c":"new"}` → `{"a":"keep-me",
"b":"untouched","c":"new"}` — the new key `c` is added, the existing `a` is **not** overwritten.

**`strategy: "onlyValues"`** — `06-merge-only-values`: same inputs, opposite strategy →
`{"a":"from-source","b":"untouched"}` — `a` **is** overwritten, `c` is **not** added.

**`matchSettings.keyPaths`** — `08-merge-match-settings`: `source: {"id":1,"extra":"added"}`
merged with `keyPaths: ["id"]` into two candidate targets — `{"id":1}` gains `extra`, `{"id":2}`
is left untouched because its `id` does not match the source's.

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
