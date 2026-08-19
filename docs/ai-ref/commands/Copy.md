# copy

> Copies all nodes matched by `fromPath` to the location(s) specified by `toPath`.
> The source nodes remain. Use `move` to copy-and-delete the source.

## Syntax

```json
{ "command": "copy", "fromPath": "$.source", "toPath": "$.destination" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Selects the node(s) to copy. |
| toPath | string | yes | — | Destination path where copies are written. |
| destinationAsArray | boolean | no | false | When true, aligns multiple results by array index rather than broadcasting to all destinations. |

**Functions in the value**: — no value field  
**Functions in the path**: ✅ `=indirect()` in `path`

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "copy", "fromPath": "$.original.name", "toPath": "$.copy.name" },
  { "command": "copy", "fromPath": "$.items[*].id", "toPath": "$.ids[*]", "destinationAsArray": true }
]
```

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Copy().From("$.original.name").To("$.copy.name");
```

## When to use

- Source node must remain in place after the operation (ETL fan-out, enrichment pipelines).
- Creating a backup of a value before a destructive transform.
- Publishing the same value to multiple locations simultaneously.
- `toPath` is `"$"` and you want to **additively merge** source fields into the root — existing root properties are preserved.
- Building an accumulator array: copy repeatedly into a `toPath` that is already an array — each call appends.

## When NOT to use

- Source should be deleted after the copy — use `move` instead; adding a separate `remove` step is redundant and error-prone.
- You only want to rename a field — `move` expresses intent more clearly and is a single atomic step.
- You want to replace-update a value in place — use `set`/`put`/`add`; `copy` writes an additional node, it does not update.

## Comparison: Copy vs Move

| Concern | copy | move |
|---------|------|------|
| Source after operation | **Kept** | **Deleted** |
| `toPath: "$"` | Additive merge — existing keys survive | Replaces root entirely |
| Downstream steps need source? | Yes — safe to use | No — source is gone |
| Rename a field | Leaves old field behind (requires extra remove) | Clean rename in one step — but see [Rename.md](Rename.md), which keeps position and XML attributes and can rename the XML document element |
| Duplicate / fan-out | Correct choice | Wrong — destroys source |

## Common mistakes

- **Using copy when move was intended.** If the source field should disappear, use `move`. Leaving stale copies in the document causes subtle bugs downstream.
- **Assuming `toPath: "$"` replaces root.** `copy` to `"$"` is a deep-merge — existing root keys survive. Use `move` to `"$"` if you need a full root replacement.
- **`destinationAsArray` on an existing array.** When the destination path already resolves to an array, the node is always appended regardless of `destinationAsArray`. The flag only controls wrapping behaviour when the destination is a scalar: `"old" → ["old", "new"]`.
- **`destinationAsArray: false` (default) with multi-match `fromPath`.** Without the flag, the copied value is broadcast to every matched destination. With the flag, results are aligned by index. Mixing up the two modes produces unexpected node counts.
- **Path typo resulting in 0 matched nodes.** The command silently becomes a noop — see trace guidance below.

## Failure modes and what the trace tells you

| Trace message | What it means | Action |
|---------------|---------------|--------|
| `"0 nodes found at FromPath"` | `fromPath` matched nothing — noop, document unchanged | Verify the path expression; check capitalisation and array indices |
| `"copied N node(s) from X to Y"` | Success — N nodes written to destination | None |
| No trace entry for this step | Step was skipped (script compilation issue) | Check script JSON for syntax errors |
