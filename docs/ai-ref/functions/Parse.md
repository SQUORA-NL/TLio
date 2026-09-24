# parse

> Parses a JSON-formatted string into a structured node (object, array, or primitive). On invalid
> JSON, it does NOT fail — it returns the original text as a string node unchanged.

## Syntax

```
=parse(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | Text to parse. Does not need to already be valid JSON — see Returns. |

## Returns

A node whose type matches the parsed JSON value (object, array, string, number, boolean, or null)
**when the source parses as JSON**. When it does not — including ordinary unquoted text like
`"hello"`, which is not itself a JSON literal — `parse` catches the parse failure internally and
returns the original text as a plain string node instead. `parse` never fails the script on bad
input; the failure paths that exist (`Arguments.Count == 0`, or the source path not resolving) are
about the *call*, not the content of the string.

## Formats

Works with all adapters. The parsed node is created via `NodeAdapter.Parse`.

## Verified example

Parsing a numeric string:

```json
{ "command": "put", "path": "$.result", "value": "=parse($.num)" }
```

Input: `{ "num": "42", "arr": "[1,2,3]", "plain": "hello" }`
Output: `{ ..., "result": 42 }` — a JSON **number**, not the string `"42"`.

Verified by: `TLio.Functions.Tests/Fixtures/Text/parse/01-number.json`, with sibling fixtures
against the same input: `02-array.json` (`=parse($.arr)` on `"[1,2,3]"` → the array `[1,2,3]`) and
`03-plain-string.json` (`=parse($.plain)` on `"hello"`, which is not valid JSON, → the string
`"hello"` unchanged, `Success: true`).

## Notes

- Complement of `=toString()`.
- Does not throw or fail on invalid JSON — see Returns above. This differs from many "parse"
  functions in other systems; do not add a defensive `isEmpty`/validation step expecting a script
  abort on malformed input, because there isn't one.

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

## Performance

`parse` does a full text parse of its argument on every call via `NodeAdapter.Parse` — there is
nothing to cache, because the string being parsed is, by construction, per-row/per-document data
rather than a fixed template. Pair this with `toString` (which serialises back to text) and a
`parse`/`toString` round trip in a hot loop costs a full stringify-then-reparse each time; if a
script only needs to touch one property of the embedded structure, it is worth checking whether
`parse` followed by a direct path reference into the result is actually cheaper than leaving the
value as a string and using a text function directly on it. For genuinely large embedded JSON
payloads processed at scale, the parse cost scales with payload size like any JSON parser.

## When to use

- A field in the document contains an escaped JSON string (e.g., a serialised payload stored as a string), and you need to work with its internal structure.
- You are deserialising data received from an external system that encoded structured values as JSON strings.
- You want to "unwrap" a stringified object so you can use path-based access on its properties in subsequent steps.
- As the complement to `toString`: use `parse` to deserialise after receiving a string-encoded value.

## When NOT to use

- The field already contains a structured object or array — no parsing needed; reference it directly with a path.
- You are relying on `parse` to *validate* that a field is JSON — it will not fail or flag
  non-JSON content; it silently passes it through as a string instead. Check the resolved node's
  type after the call if validation matters.
- The string contains non-JSON-formatted text (e.g., XML, YAML, CSV) — `parse` will not throw, but
  it also will not understand it; the text comes back unchanged as a string, which is rarely the
  intended outcome for those formats.

## Comparison

| Function | Direction | Use when |
|----------|-----------|----------|
| `parse` | JSON string → node | Deserialising a JSON string back into a structured node |
| `toString` | node → JSON string | Serialising structured data into a string field |
| `format` | template + args → string | Producing formatted human-readable strings from values |

## Common mistakes

- **Assuming non-JSON input fails**: `parse` on a plain string like `"hello"` does NOT fail — it
  returns the string `"hello"` unchanged, `Success: true`. A downstream step that assumes it got
  an object/array back (e.g. immediately indexing into it) can misbehave silently on bad input,
  because `parse` never surfaces the problem itself.
- **Double-parsing**: if the field value is already a structured node (not a string), calling `parse` on it will attempt to parse its serialised text — this is unnecessary and fragile.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].payload` as argument grabs all matching string values, but only the first resolved node is parsed. Use indexed paths for per-element parsing.
