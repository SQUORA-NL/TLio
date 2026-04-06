# copy

> Copies all nodes matched by `fromPath` to the location(s) specified by `toPath`.
> The source nodes remain. Use `move` to copy-and-delete the source.

## Syntax

```json
{ "command": "copy", "fromPath": "$.source", "toPath": "$.destination" }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Selects the node(s) to copy. |
| toPath | string | yes | — | Destination path where copies are written. |
| destinationAsArray | boolean | no | false | When true, aligns multiple results by array index rather than broadcasting to all destinations. |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "copy", "fromPath": "$.original.name", "toPath": "$.copy.name" },
  { "command": "copy", "fromPath": "$.items[*].id", "toPath": "$.ids[*]", "destinationAsArray": true }
]
```
