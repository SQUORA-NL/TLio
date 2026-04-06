# =fetch()

> Evaluates a path expression and returns the **first matched node's value**. Logs a
> warning and returns null if the path matches nothing.

## Syntax

```
=fetch(path)
```

Used as a value in any command: `"value": "=fetch($.source)"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path expression selecting the source node. Uses the same path style as the active adapter. |

## Returns

The value of the first matched node (string, number, boolean, object, or array).

## Example

```json
{ "command": "set", "path": "$.target", "value": "=fetch($.source)" }
```

```json
{ "command": "set", "path": "$.summary.name", "value": "=fetch($.user.profile.displayName)" }
```
