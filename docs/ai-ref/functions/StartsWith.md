# startsWith

> Returns true if a string starts with the given prefix.

## Syntax

```
=startsWith(<source>, <prefix>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to check. |
| 2 | string or path | yes | The prefix to test. |

## Returns

A boolean node.

## Example

```json
{ "command": "set", "path": "$.ok", "value": "=startsWith($.url,'https')" }
```
