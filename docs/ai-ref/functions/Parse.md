# parse

> Parses a JSON string into a structured node (object, array, or primitive).

## Syntax

```
=parse(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | A valid JSON string to parse. |

## Returns

A node whose type matches the parsed JSON value (object, array, string, number, boolean, or null).

## Formats

Works with all adapters. The parsed node is created via `NodeAdapter.Parse`.

## Example

```json
{ "command": "add", "path": "$.obj", "value": "=parse($.jsonString)" }
```

Input: `{ "jsonString": "{\"name\":\"Alice\"}" }`
Output: `{ "jsonString": "{\"name\":\"Alice\"}", "obj": { "name": "Alice" } }`

## Notes

- Complement of `=toString()`.
- Throws / returns failed if the argument is not valid JSON.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.obj\",\"value\":\"=parse($.jsonString)\"}]",
    JObject.Parse("{\"jsonString\":\"{\\\"name\\\":\\\"Alice\\\"}\"}"),
    JsonExecutionContext.CreateDefault());
```
