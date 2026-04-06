# length

> Returns the number of characters in a string.

## Syntax

```
=length(str)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string whose length to measure. |

## Returns

A numeric node containing the character count. Returns `0` for null or empty input.

## Formats

Works with all adapters. Uses `string.Length`.

## Example

```json
{ "command": "add", "path": "$.len", "value": "=length($.name)" }
```

Input: `{ "name": "Alice" }`
Output: `{ "name": "Alice", "len": 5 }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.len\",\"value\":\"=length($.name)\"}]",
    JObject.Parse("{\"name\":\"Alice\"}"),
    JsonExecutionContext.CreateDefault());
```
