# toString

> Converts any node to its string representation. Objects and arrays are serialized to compact
> JSON; primitives are converted via their string value.

## Syntax

```
=toString(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any | yes | The node to convert to a string. |

## Returns

- **Object / array**: compact JSON string (e.g. `{"a":1,"b":2}`)
- **String**: the string value unchanged
- **Number / boolean**: the value as a string
- **Null**: empty string `""`

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "add", "path": "$.str", "value": "=toString($.obj)" }
```

Input: `{ "obj": { "a": 1, "b": 2 } }`
Output: `{ "obj": { "a": 1, "b": 2 }, "str": "{\"a\":1,\"b\":2}" }`

## Notes

- Registered as `"toString"` (camelCase). The `FunctionName` property is overridden explicitly
  because the CLR name `ToStringFunction` would resolve to `"tostringfunction"`.
- Available after calling `RegisterText<TNode>()` or `RegisterTextPack<TNode>()`.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.str\",\"value\":\"=toString($.obj)\"}]",
    JObject.Parse("{\"obj\":{\"a\":1,\"b\":2}}"),
    JsonExecutionContext.CreateDefault());
```
