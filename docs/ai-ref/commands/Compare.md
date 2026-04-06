# compare

> Compares two nodes and writes a result string (`"equal"`, `"greater"`, `"less"`, or
> `"different"`) to a target path.

## Syntax

```json
{ "command": "compare", "firstPath": "$.a", "secondPath": "$.b", "resultPath": "$.result" }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| firstPath | string | yes | — | Path to the first node (left-hand side of comparison). |
| secondPath | string | yes | — | Path to the second node (right-hand side). |
| resultPath | string | yes | — | Path where the result string is written (upsert). |

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
{ "command": "compare", "firstPath": "$.score", "secondPath": "$.threshold", "resultPath": "$.verdict" }
```
