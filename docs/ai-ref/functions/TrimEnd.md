# trimEnd

> Removes trailing (right-side) whitespace from a string.

## Syntax

```
=trimEnd(str)
```

Registered as both `"trimEnd"` (camelCase, 008+) and `"trimend"` (legacy lowercase).

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to right-trim. |

## Returns

A string node with trailing whitespace removed via `string.TrimEnd()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "set", "path": "$.name", "value": "=trimEnd($.name)" }
```

Input: `{ "name": "Alice  " }`
Output: `{ "name": "Alice" }`

## Notes

- `"trimend"` (all-lowercase) is the original registration and remains for backwards compatibility.
- `"trimEnd"` (camelCase) was added in 008 for JLio parity.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.name\",\"value\":\"=trimEnd($.name)\"}]",
    JObject.Parse("{\"name\":\"Alice  \"}"),
    JsonExecutionContext.CreateDefault());
```
