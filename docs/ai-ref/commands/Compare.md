# compare

> Compares two nodes and writes a result string (`"equal"`, `"greater"`, `"less"`, or
> `"different"`) to a target path.

## Syntax

```json
{ "command": "compare", "fromPath": "$.a", "toPath": "$.b", "resultPath": "$.result" }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Path to the first node (left-hand side of comparison). Alias: `firstPath`. |
| toPath | string | yes | — | Path to the second node (right-hand side). Alias: `secondPath`. |
| resultPath | string | yes | — | Path where the result string is written (upsert). |

**Supports functions**: ❌

## Result values

| Value | Meaning |
|-------|---------|
| `"equal"` | Both nodes have equal scalar values |
| `"greater"` | First node's value > second node's value |
| `"less"` | First node's value < second node's value |
| `"different"` | Nodes differ and cannot be ordered (type mismatch, objects, arrays) |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{ "command": "compare", "fromPath": "$.score", "toPath": "$.threshold", "resultPath": "$.verdict" }
```

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Compare().From("$.score").To("$.threshold").Result("$.verdict");
```
