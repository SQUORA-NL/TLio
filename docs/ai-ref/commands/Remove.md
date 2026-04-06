# remove

> Removes all nodes matched by the path expression. Logs a warning if no nodes match;
> does not error.

## Syntax

```json
{ "command": "remove", "path": "$.fieldToDelete" }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the node(s) to remove. Wildcards remove multiple nodes. |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "remove", "path": "$.tempId" },
  { "command": "remove", "path": "$.items[?(@.active == false)]" }
]
```
