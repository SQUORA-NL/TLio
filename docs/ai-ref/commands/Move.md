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

**Supports functions**: ❌

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
