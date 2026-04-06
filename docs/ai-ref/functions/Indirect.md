# =indirect()

> Two-step path resolution: reads a **string value** at the given path, then uses that
> string as a second path expression to retrieve the final value.

## Syntax

```
=indirect(pathToPath)
```

Used as a value in any command: `"value": "=indirect($.pathRef)"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path to a node whose **string content** is a valid path expression. |

## Returns

The value found at the dynamically resolved path. Logs a warning if either path
resolves to nothing.

## Example

Given `{ "pathRef": "$.source", "source": "hello" }`:

```json
{ "command": "set", "path": "$.target", "value": "=indirect($.pathRef)" }
```

Result: `$.target` = `"hello"` (resolved via `$.pathRef` → `"$.source"` → `"hello"`).

## C# Usage

```csharp
// Already registered via ParseOptions.CreateDefault()
var options = ParseOptions<JToken>.CreateDefault();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.target\",\"value\":\"=indirect($.pathRef)\"}]",
    JObject.Parse("{\"pathRef\":\"$.source\",\"source\":\"hello\"}"),
    JsonExecutionContext.CreateDefault());
```
