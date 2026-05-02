# parse

> Parses a JSON string into a structured node (object, array, or primitive).

## Syntax

```
=parse(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | A valid JSON string to parse. |

## Returns

A node whose type matches the parsed JSON value (object, array, string, number, boolean, or null).

## Formats

Works with all adapters. The parsed node is created via `NodeAdapter.Parse`.

## Example

```json
{ "command": "add", "path": "$.obj", "value": "=parse($.jsonString)" }
```

Input: `{ "jsonString": "{\"name\":\"Alice\"}" }`
Output: `{ "jsonString": "{\"name\":\"Alice\"}", "obj": { "name": "Alice" } }`

## Notes

- Complement of `=toString()`.
- Throws / returns failed if the argument is not valid JSON.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.obj\",\"value\":\"=parse($.jsonString)\"}]",
    JObject.Parse("{\"jsonString\":\"{\\\"name\\\":\\\"Alice\\\"}\"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- A field in the document contains an escaped JSON string (e.g., a serialised payload stored as a string), and you need to work with its internal structure.
- You are deserialising data received from an external system that encoded structured values as JSON strings.
- You want to "unwrap" a stringified object so you can use path-based access on its properties in subsequent steps.
- As the complement to `toString`: use `parse` to deserialise after receiving a string-encoded value.

## When NOT to use

- The field already contains a structured object or array — no parsing needed; reference it directly with a path.
- The string is NOT valid JSON — `parse` will fail. Use `isEmpty` and validate the format upstream before calling `parse`.
- You want to convert a plain text string to a number or boolean — use the appropriate numeric operations or direct value assignment instead.
- The string contains non-JSON-formatted text (e.g., XML, YAML, CSV) — `parse` only understands JSON syntax.

## Comparison

| Function | Direction | Use when |
|----------|-----------|----------|
| `parse` | JSON string → node | Deserialising a JSON string back into a structured node |
| `toString` | node → JSON string | Serialising structured data into a string field |
| `format` | template + args → string | Producing formatted human-readable strings from values |

## Common mistakes

- **Passing non-JSON strings**: `parse` on a plain string like `"hello"` will fail unless it is a valid JSON string literal (i.e., `"\"hello\""` which parses to the string `hello`). Only call `parse` on fields you know contain JSON.
- **Double-parsing**: if the field value is already a structured node (not a string), calling `parse` on it will attempt to parse its JSON serialisation — this is unnecessary and fragile.
- **Error handling**: `parse` fails hard on invalid JSON. Ensure upstream data quality or add a guard step.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].payload` as argument grabs all matching string values. `parse` will attempt to parse them as a combined node — use indexed paths for per-element parsing.
