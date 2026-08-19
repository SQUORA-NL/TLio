# move

> Moves nodes from `fromPath` to `toPath` — equivalent to `copy` followed by `remove`
> on the source. Source nodes are deleted after the copy succeeds.

## Syntax

```json
{ "command": "move", "fromPath": "$.oldLocation", "toPath": "$.newLocation" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Selects the node(s) to move. |
| toPath | string | yes | — | Destination path where nodes are written. |
| destinationAsArray | boolean | no | false | When true, aligns multiple results by array index. |

**Functions in the value**: — no value field  
**Functions in the path**: ✅ `=indirect()` in `path`

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "move", "fromPath": "$.draft.title", "toPath": "$.published.title" },
  { "command": "move", "fromPath": "$.temp", "toPath": "$.permanent" }
]
```

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Move().From("$.draft.title").To("$.published.title");
```

## When to use

- Renaming a field — clean, single-step operation with no leftover copy.
- Restructuring nested data — promoting a deeply nested node to a shallower path, or vice versa.
- Source is temporary (scratch/staging fields) and must not appear in final output.
- `toPath` is `"$"` and you want to **replace the root entirely** with the source value.
- Pipeline steps where each node is consumed exactly once (no fan-out required).

## When NOT to use

- Source is needed by a later step — use `copy`; `move` deletes the source immediately.
- Fan-out: writing the same value to multiple destinations — use `copy`.
- You only want to delete a node without placing it elsewhere — use `remove`.
- `toPath: "$"` when you want an additive merge — use `copy` to `"$"` instead; `move` to root discards all existing root content.

## Comparison: Copy vs Move

| Concern | copy | move |
|---------|------|------|
| Source after operation | **Kept** | **Deleted** |
| `toPath: "$"` | Additive merge — existing keys survive | Replaces root entirely |
| Downstream steps need source? | Yes — safe to use | No — source is gone |
| Rename a field | Leaves old field behind (requires extra remove) | Clean rename in one step — but see [Rename.md](Rename.md), which keeps position and XML attributes and can rename the XML document element |
| Duplicate / fan-out | Correct choice | Wrong — destroys source |

## Common mistakes

- **Using move when a downstream step still reads the source.** The source is gone after `move` — the next step that reads `fromPath` will match nothing.
- **Assuming `toPath: "$"` merges additively.** `move` to `"$"` replaces the entire root. Use `copy` to `"$"` for an additive merge.
- **`destinationAsArray` on an existing array.** When the destination path already resolves to an array, the node is always appended regardless of `destinationAsArray`. The flag only controls wrapping when the destination is a scalar.
- **Expecting move to be a two-phase undo-able operation.** Move is atomic: if the copy succeeds, the source is immediately removed. There is no partial state.
- **Path typo resulting in 0 matched nodes.** The command silently becomes a noop — see trace guidance below.

## Failure modes and what the trace tells you

| Trace message | What it means | Action |
|---------------|---------------|--------|
| `"0 nodes found at FromPath"` | `fromPath` matched nothing — noop, source not deleted, document unchanged | Verify the path expression; check capitalisation and array indices |
| `"moved N node(s) from X to Y. Source 'X' removed."` | Success — N nodes written to destination and source deleted | Confirm the "Source removed" note; source path is now absent from document |
| No trace entry for this step | Step was skipped (script compilation issue) | Check script JSON for syntax errors |
