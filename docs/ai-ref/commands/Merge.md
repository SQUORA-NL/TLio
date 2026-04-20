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
