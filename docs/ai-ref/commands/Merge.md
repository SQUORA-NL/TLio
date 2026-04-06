# merge

> Deep-merges the node(s) at `path` (source) into the node(s) at `targetPath`
> (destination). Objects are merged recursively; arrays follow `arrayMergeMode`.

## Syntax

```json
{ "command": "merge", "path": "$.source", "targetPath": "$.target" }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the source node(s) to merge from. |
| targetPath | string | yes | — | Selects the destination node(s) to merge into. |
| arrayMergeMode | string | no | `"concat"` | How arrays are merged: `"concat"` appends source to target; `"replace"` overwrites target. |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "merge", "path": "$.patch", "targetPath": "$.document" },
  { "command": "merge", "path": "$.newItems", "targetPath": "$.list", "arrayMergeMode": "replace" }
]
```
