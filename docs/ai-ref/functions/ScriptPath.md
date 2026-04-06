# =scriptpath()

> Returns the **absolute path** of the currently executing node as a string. Optionally
> resolves a relative sub-path from that position.

## Syntax

```
=scriptpath()
=scriptpath(@.child)
```

Used as a value in any command: `"value": "=scriptpath()"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (relative path) | no | Relative path starting with `@`. Resolved from the current node's absolute path. |

## Returns

A string containing the absolute JSONPath (or format-equivalent path) of the current
node, or the resolved path if an argument is provided.

## Example

```json
{ "command": "set", "path": "$.result", "value": "=scriptpath()" }
```

Result: `$.result` = `"$"` (at document root)

```json
{ "command": "set", "path": "$.items[0].selfPath", "value": "=scriptpath()" }
```

Result: `$.items[0].selfPath` = `"$.items[0]"`
