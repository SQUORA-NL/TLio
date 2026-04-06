# =fetch()

> Evaluates a path expression and returns the **first matched node's value**. Returns an
> optional default value when the path matches nothing.

## Syntax

```
=fetch(path)
=fetch(path, defaultValue)
```

Used as a value in any command: `"value": "=fetch($.source)"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path expression selecting the source node. |
| 2 | any | no | Default value returned when the path resolves to nothing. |

## Returns

The value of the first matched node, or the default value if no match and a default is provided.
Logs a warning and returns failed when the path matches nothing and no default is given.

## Example

```json
{ "command": "set", "path": "$.target", "value": "=fetch($.source)" }
```

```json
{ "command": "add", "path": "$.name", "value": "=fetch($.user.name,'Anonymous')" }
```

## C# Usage

```csharp
// Register via ParseOptions (already included in CreateDefault)
var options = ParseOptions<JToken>.CreateDefault();
// Use in script JSON: "=fetch($.path,'default')"
```
