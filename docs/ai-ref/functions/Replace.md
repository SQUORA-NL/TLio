# replace

> Replaces all occurrences of a substring within a string.

## Syntax

```
=replace(<source>, <old>, <new>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. |
| 2 | string or path | yes | The substring to find and replace. |
| 3 | string or path | yes | The replacement string. |

## Returns

A string node with all occurrences of `old` replaced by `new`. Case-sensitive.

## Formats

Works with all adapters. Uses `string.Replace`.

## Example

```json
{ "command": "set", "path": "$.code", "value": "=replace($.code, '-', '_')" }
```

Input: `{ "code": "my-value-key" }`
Output: `{ "code": "my_value_key" }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.code\",\"value\":\"=replace($.code,'-','_')\"}]",
    JObject.Parse("{\"code\":\"my-value-key\"}"),
    JsonExecutionContext.CreateDefault());
```
