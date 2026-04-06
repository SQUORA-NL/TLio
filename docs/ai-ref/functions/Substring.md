# substring

> Extracts a portion of a string starting at a given index, with an optional length limit.

## Syntax

```
=substring(str, start)
=substring(str, start, count)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. |
| 2 | number | yes | Zero-based start index. |
| 3 | number | no | Maximum number of characters to extract. Omit to extract to end of string. |

## Returns

A string node containing the extracted substring.

## Formats

Works with all adapters. Uses `string.Substring`.

## Example

```json
{ "command": "add", "path": "$.abbr", "value": "=substring($.name, 0, 3)" }
```

Input: `{ "name": "Alice" }`
Output: `{ "name": "Alice", "abbr": "Ali" }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.abbr\",\"value\":\"=substring($.name,0,3)\"}]",
    JObject.Parse("{\"name\":\"Alice\"}"),
    JsonExecutionContext.CreateDefault());
```
