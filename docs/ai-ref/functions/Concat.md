# concat

> Concatenates two or more string arguments into a single string.

## Syntax

```
=concat(a, b)
=concat(a, b, c, ...)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | First string segment. |
| 2 | string or path | yes | Second string segment. |
| 3+ | string or path | no | Additional segments (variadic). |

## Returns

A string node containing all arguments joined in order.

## Formats

Works with all adapters (JSON, XML, YAML). Each argument is resolved via `TryGetString`.

## Example

```json
{ "command": "add", "path": "$.full", "value": "=concat($.first,' ',$.last)" }
```

Input: `{ "first": "Alice", "last": "Smith" }`
Output: `{ "first": "Alice", "last": "Smith", "full": "Alice Smith" }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.full\",\"value\":\"=concat($.first,' ',$.last)\"}]",
    JObject.Parse("{\"first\":\"Alice\",\"last\":\"Smith\"}"),
    JsonExecutionContext.CreateDefault());
```
