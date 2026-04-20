# =scriptpath()

> Returns the **absolute path** of the currently executing node as a string. Optionally
> resolves a relative sub-path from that position.

## Syntax

```
=scriptpath()
=scriptpath(@.child)
```

Used as a value in any command: `"value": "=scriptpath()"`

> See [Notation Reference](../notation-reference.md) for path and quoting rules.
> The relative path argument uses `@.` (with dot) — `@child` without the dot is not valid in JSON/YAML context.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (relative path) | no | Relative path starting with `@.`. Resolved from the current node's absolute path. |

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

## Notes

- Also registered as `"path"` (camelCase alias, 008+) for JLio compatibility — see [Path.md](Path.md).
- `=path()` and `=scriptpath()` are identical at runtime.

## C# Usage

```csharp
// Already registered via ParseOptions.CreateDefault() under both "scriptpath" and "path"
var options = ParseOptions<JToken>.CreateDefault();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.items[*].loc\",\"value\":\"=scriptpath()\"}]",
    JObject.Parse("{\"items\":[{\"id\":1},{\"id\":2}]}"),
    JsonExecutionContext.CreateDefault());
```
