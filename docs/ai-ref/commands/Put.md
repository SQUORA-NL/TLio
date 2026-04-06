# put

> **Upsert**: sets the value if the node already exists, creates it if absent.
> Use `set` when the node must pre-exist (errors on missing), or `add` to create-only
> (skips if exists).

## Syntax

```json
{ "command": "put", "path": "$.field", "value": <TLioValue> }
```

Two-argument form (upsert child of matched parent):

```json
{ "command": "put", "path": "$.address", "property": "country", "value": "NL" }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects target node(s). With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to upsert on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression to assign. |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "put", "path": "$.status", "value": "active" },
  { "command": "put", "path": "$.meta", "property": "version", "value": 2 }
]
```
