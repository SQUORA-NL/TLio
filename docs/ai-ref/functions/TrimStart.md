# trimStart

> Removes leading (left-side) whitespace from a string.

## Syntax

```
=trimStart(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Registered as both `"trimStart"` (camelCase, 008+) and `"trimstart"` (legacy lowercase).

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to left-trim. |

## Returns

A string node with leading whitespace removed via `string.TrimStart()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "set", "path": "$.name", "value": "=trimStart($.name)" }
```

Input: `{ "name": "  Alice" }`
Output: `{ "name": "Alice" }`

## Notes

- `"trimstart"` (all-lowercase) is the original registration and remains for backwards compatibility.
- `"trimStart"` (camelCase) was added in 008 for JLio parity.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.name\",\"value\":\"=trimStart($.name)\"}]",
    JObject.Parse("{\"name\":\"  Alice\"}"),
    JsonExecutionContext.CreateDefault());
```
