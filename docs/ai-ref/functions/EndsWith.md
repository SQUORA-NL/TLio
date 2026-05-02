# endsWith

> Returns true if a string ends with the given suffix.

## Syntax

```
=endsWith(<source>, <suffix>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to check. |
| 2 | string or path | yes | The suffix to test. |

## Returns

A boolean node.

## Example

```json
{ "command": "set", "path": "$.ok", "value": "=endsWith($.file,'.json')" }
```
