# toUpper

> Converts a string to uppercase.

## Syntax

```
=toUpper(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Registered as both `"toUpper"` (camelCase, 008+) and `"toupper"` (legacy lowercase).

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to convert. |

## Returns

A string node with all characters converted to uppercase via `string.ToUpperInvariant()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "add", "path": "$.upper", "value": "=toUpper($.name)" }
```

Input: `{ "name": "Alice" }`
Output: `{ "name": "Alice", "upper": "ALICE" }`

## Notes

- `"toupper"` (all-lowercase) is the original registration and remains for backwards compatibility.
- `"toUpper"` (camelCase) was added in 008 for JLio parity.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.upper\",\"value\":\"=toUpper($.name)\"}]",
    JObject.Parse("{\"name\":\"Alice\"}"),
    JsonExecutionContext.CreateDefault());
```
