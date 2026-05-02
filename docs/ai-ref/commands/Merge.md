# merge

> Deep-merges the node(s) at `fromPath` (source) into the node(s) at `toPath`
> (destination). Objects are merged recursively; arrays follow `arrayMergeMode`.

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
| arrayMergeMode | string | no | `"concat"` | How arrays are merged: `"concat"` appends source to target; `"replace"` overwrites target. |

**Supports functions**: ❌

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "merge", "fromPath": "$.patch", "toPath": "$.document" },
  { "command": "merge", "fromPath": "$.newItems", "toPath": "$.list", "arrayMergeMode": "replace" }
]
```

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Merge().From("$.patch").To("$.document");
```

## When to use

- Two partially-overlapping objects need to be combined without replacing the entire destination — e.g., applying a config patch onto a base config.
- A secondary object should contribute its fields to a primary object, keeping fields that exist only in the primary intact.
- Merging into root (`$.`) when you want to fold a sub-object's properties up to the top level additively.
- Array handling is intentionally coarse: you want all source arrays appended (`concat`) or fully replaced (`replace`), not selectively merged.

## When NOT to use

- You need to copy or move specific fields — use `copy` or `set` instead; merge applies to entire object trees.
- The source is a primitive (string, number, boolean) — merge only combines objects; primitives at the source path are not merged.
- You need fine-grained control over individual array items — `arrayMergeMode` is all-or-nothing; use `copy`/`set` with explicit index paths for item-level control.
- You want to overwrite the destination entirely — use `copy` or `set`; merge preserves destination-only fields.
- The source and destination are arrays at their root level — merge operates on objects.

## Common mistakes

- **Forgetting `arrayMergeMode`**: the default is `"concat"`, which appends source arrays to destination arrays. If the destination already has the data, this doubles entries. Set `"replace"` when the source array is the authoritative version.
- **Merging primitives**: if `fromPath` resolves to a string or number, the command has no effect. Verify the source node is an object.
- **Merge vs. copy confusion**: merge into root (`$.`) resembles a copy-to-root but preserves existing root fields. A plain `copy` overwrites. Choose based on whether existing destination fields must be kept.
- **Misusing `"ignore"` mode**: `arrayMergeMode: "ignore"` silently skips all arrays in the source. If the source has important array data, it will be lost without any error.
- **Path aliases**: `fromPath` has an alias `path`; `toPath` has an alias `targetPath`. Mixing aliases across commands in the same script is fine but can cause confusion when reading the trace.

## Failure modes and what the trace tells you

- **No-op merge**: trace shows the merge executed but destination is unchanged — check that `fromPath` actually resolves to an object node, not a primitive or missing path.
- **Doubled array entries**: trace shows the merge succeeded; inspect the destination array length before and after — `arrayMergeMode` is defaulting to `"concat"` and the data already existed at the destination.
- **Source path not found**: trace reports a path-resolution failure or empty match for `fromPath`. Verify the path against the current document state at that pipeline step.
- **Destination path not found**: trace reports a path-resolution failure for `toPath`. The destination object must already exist; use `set` first to create it if needed.
