# toLower

> Converts a string to lowercase.

## Syntax

```
=toLower(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Registered as both `"toLower"` (camelCase, 008+) and `"tolower"` (legacy lowercase).

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to convert. |

## Returns

A string node with all characters converted to lowercase via `string.ToLowerInvariant()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "add", "path": "$.lower", "value": "=toLower($.name)" }
```

Input: `{ "name": "Alice" }`
Output: `{ "name": "Alice", "lower": "alice" }`

## Notes

- `"tolower"` (all-lowercase) is the original registration and remains for backwards compatibility.
- `"toLower"` (camelCase) was added in 008 for JLio parity.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.lower\",\"value\":\"=toLower($.name)\"}]",
    JObject.Parse("{\"name\":\"Alice\"}"),
    JsonExecutionContext.CreateDefault());
```
