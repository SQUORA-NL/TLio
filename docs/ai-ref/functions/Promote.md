# =promote()

> Wraps the matched node in a new object using the node's own **property name** as the
> key. Use when you need to lift a nested value into a named wrapper object.

## Syntax

```
=promote(path)
```

Used as a value in any command: `"value": "=promote($.person)"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path to the node to promote. The node's parent property name becomes the wrapper key. |

## Returns

An object with one key (the matched node's property name) whose value is the matched node.

## Example

Given `{ "person": { "name": "Alice", "age": 30 } }`:

```json
{ "command": "set", "path": "$.result", "value": "=promote($.person)" }
```

Result: `$.result` = `{ "person": { "name": "Alice", "age": 30 } }`
