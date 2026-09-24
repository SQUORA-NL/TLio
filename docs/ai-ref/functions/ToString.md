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

## Verified example

```json
{ "command": "add", "path": "$.str", "value": "=toString($.obj)" }
```

Input: `{ "obj": { "a": 1, "b": 2 } }`
Output: `{ "obj": { "a": 1, "b": 2 }, "str": "{\"a\":1,\"b\":2}" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/tostring/01-object-to-string.json`.

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

## When to use

- Serialising a structured object or array into a JSON string so it can be stored in a string field.
- Embedding a structured value as an escaped JSON string inside another document (e.g., storing a payload as a string for transport).
- Converting a number or boolean to its string form for use with string functions (`concat`, `replace`, etc.).
- As the complement to `parse`: use `toString` to serialise before transport, use `parse` to deserialise on the other end.

## When NOT to use

- The input is already a string — `toString` returns it unchanged, but the call is unnecessary.
- You need pretty-printed or formatted JSON output — `toString` produces compact JSON with no whitespace.
- You need human-readable display formatting of a number (e.g., decimal places, currency symbol) — use `format` with a template instead.

## Comparison

| Function | Direction | Use when |
|----------|-----------|----------|
| `toString` | node → JSON string | Serialising structured data into a string field |
| `parse` | JSON string → node | Deserialising a JSON string back into a structured node |
| `format` | template + args → string | Producing formatted human-readable strings from values |

## Common mistakes

- **Compact output**: `toString` always produces compact JSON (no spaces). Do not rely on specific whitespace formatting in the output.
- **Null becomes empty string**: `toString(null)` returns `""`, not `"null"`. If you need the literal string `"null"`, use a literal value instead.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*]` as argument passes the matched node set. The behaviour depends on how the adapter serialises a multi-match result — use a specific path for predictable output.
- **Do not use toString to compare objects**: serialise both sides to strings and compare — only works reliably if key order is deterministic (Newtonsoft.Json preserves insertion order; System.Text.Json may differ).
