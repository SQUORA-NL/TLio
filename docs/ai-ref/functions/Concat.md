# concat

> Concatenates one or more arguments as strings into a single string.

## Syntax

```
=concat(<value1>)
=concat(<value1>, <value2>, <valueN>, ...)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | First string segment. A single argument is valid — `concat` requires at least one, not two. |
| 2+ | string or path | no | Additional segments (variadic). An array argument is flattened element-by-element and each element's string form is appended in place — it is not rejected, just not itself a common use case (see `join` for the array case). |

## Returns

A string node containing all arguments joined in order.

## Formats

Works with all adapters (JSON, XML, YAML). Each argument is resolved via `TryGetString`.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=concat($.a, $.b, $.c)" }
```

Input: `{ "a": "Hello", "b": " ", "c": "World" }`
Output: `{ "a": "Hello", "b": " ", "c": "World", "result": "Hello World" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/concat/01-three-strings.json` (and
`02-single-arg.json`, `=concat($.a)` → `"Hello"`, confirming the single-argument form is valid).

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

- You are joining the elements of an array **with a separator** — use `join` instead. `concat`
  does accept an array argument (it flattens it element-by-element and appends each element's
  string form), but with no separator between elements: `=concat($.tags)` on `["a","b","c"]`
  gives `"abc"`, not `"a, b, c"`.
- The number of values to join is dynamic or unknown at script-write time — use `join` instead.
- You need a template with named slots — use `format` instead (cleaner syntax for sentence-style templates).

## Comparison

| Function | Input style | Separator | Use when |
|----------|------------|-----------|----------|
| `concat` | Fixed individual args | Literal arg between values | 2–4 known fields, arbitrary separators |
| `join` | Array path + single separator | Single separator for all gaps | Dynamic or array-sourced list of values |
| `format` | Template string + positional args | Part of template literal | Sentence-style templates with `{0}` placeholders |

## Common mistakes

- **Passing an array expecting a separator**: `=concat($.tags)` silently flattens the array with
  no separator (`"abc"`, not `"a, b, c"`) — it does not fail, so this can go unnoticed. Use `join`
  when a separator matters.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].name` as an argument grabs all values as a flat list, not individual concatenations. Use indexed paths for per-element work.
- **Missing separator**: `=concat($.first,$.last)` produces `"AliceSmith"` with no space. Pass the separator as a literal arg between the fields.
- **Null arguments**: if a path resolves to null, that argument becomes an empty string (does not throw), so the result silently omits that segment.
