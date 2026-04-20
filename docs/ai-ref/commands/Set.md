# set

> Sets the value of an **existing** node; logs a warning and skips if the node is absent.
> Use `put` for upsert (create-or-update) or `add` to create-only.

## Syntax

```json
{ "command": "set", "path": "$.target", "value": <TLioValue> }
```

Two-argument form (select parent, name child property):

```json
{ "command": "set", "path": "$.items[*]", "property": "active", "value": true }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the target node(s). With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to set on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression to assign. |

**Supports functions**: ✅

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "set", "path": "$.address.city", "value": "Amsterdam" },
  { "command": "set", "path": "$.items[*]", "property": "active", "value": true }
]
```

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Set(JValue.CreateString("Amsterdam")).OnPath("$.address.city");
```
