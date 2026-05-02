# concat

> Concatenates two or more string arguments into a single string.

## Syntax

```
=concat(<value1>, <value2>)
=concat(<value1>, <value2>, <valueN>, ...)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

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

## When to use

- You are joining 2–4 known, fixed fields with a literal separator (e.g., first name + space + last name).
- The separator is different between each pair of values (e.g., `=concat($.a,'-',$.b,'_',$.c)`).
- You want a readable, explicit assembly of a few known values into one string.

## When NOT to use

- You are joining the elements of an array — use `join` instead. `concat` does not accept an array argument; you would have to spell out every index.
- The number of values to join is dynamic or unknown at script-write time — use `join` instead.
- You need a template with named slots — use `format` instead (cleaner syntax for sentence-style templates).

## Comparison

| Function | Input style | Separator | Use when |
|----------|------------|-----------|----------|
| `concat` | Fixed individual args | Literal arg between values | 2–4 known fields, arbitrary separators |
| `join` | Array path + single separator | Single separator for all gaps | Dynamic or array-sourced list of values |
| `format` | Template string + positional args | Part of template literal | Sentence-style templates with `{0}` placeholders |

## Common mistakes

- **Joining arrays with concat**: `=concat($.tags[0],$.tags[1])` is fragile and breaks when array length varies. Use `join` for arrays.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].name` as an argument grabs all values as a flat list, not individual concatenations. Use indexed paths for per-element work.
- **Missing separator**: `=concat($.first,$.last)` produces `"AliceSmith"` with no space. Pass the separator as a literal arg between the fields.
- **Null arguments**: if a path resolves to null, that argument becomes an empty string (does not throw), so the result silently omits that segment.
