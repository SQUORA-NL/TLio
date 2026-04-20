# format

> Replaces `{0}`, `{1}`, … placeholders in a template string with the supplied argument values.

## Syntax

```
=format(<template>, <value0>)
=format(<template>, <value0>, <value1>, ...)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | Template string containing `{0}`, `{1}`, … placeholders. |
| 2+ | any | yes (min 1) | Replacement values, substituted in placeholder order. |

## Returns

A string with all `{N}` placeholders replaced by the corresponding argument values.

## Formats

Works with all adapters. Uses `string.Format` internally.

## Example

```json
{ "command": "add", "path": "$.greeting", "value": "=format('Hello, {0}!', $.name)" }
```

Input: `{ "name": "Alice" }`
Output: `{ "name": "Alice", "greeting": "Hello, Alice!" }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.msg\",\"value\":\"=format('Hi {0}, you are {1}', $.name, $.role)\"}]",
    JObject.Parse("{\"name\":\"Alice\",\"role\":\"admin\"}"),
    JsonExecutionContext.CreateDefault());
```
