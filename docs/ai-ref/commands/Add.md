# add

> Creates a new property or appends to an array; **skips silently if the property already
> exists**. Use `put` to update existing values, or `set` when the node must already exist.

## Syntax

```json
{ "command": "add", "path": "$.newProp", "value": <TLioValue> }
```

Two-argument form (add child to matched parent):

```json
{ "command": "add", "path": "$.address", "property": "country", "value": "NL" }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Target path for the new node. With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to create on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression to assign. |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "add", "path": "$.newField", "value": "created" },
  { "command": "add", "path": "$.items", "value": ["first"] }
]
```
