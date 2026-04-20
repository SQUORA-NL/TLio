# trim

> Removes leading and trailing whitespace from a string.

## Syntax

```
=trim(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to trim. |

## Returns

A string node with leading and trailing whitespace removed via `string.Trim()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "set", "path": "$.name", "value": "=trim($.name)" }
```

Input: `{ "name": "  Alice  " }`
Output: `{ "name": "Alice" }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.name\",\"value\":\"=trim($.name)\"}]",
    JObject.Parse("{\"name\":\"  Alice  \"}"),
    JsonExecutionContext.CreateDefault());
```
