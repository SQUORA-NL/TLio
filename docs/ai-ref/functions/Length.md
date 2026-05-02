# length

> Returns the number of characters in a string.

## Syntax

```
=length(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

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

## When to use

- You need to compute the character count of a string for validation (e.g., check if a field exceeds a maximum length).
- You want to store the string length as a derived field.
- You need the length as input for a `substring` or `padLeft`/`padRight` calculation.
- You want a null-safe "is this non-empty?" numeric check — `length` returns `0` for null/empty without throwing.

## When NOT to use

- You only need to know if a string is blank/null — use `isEmpty` instead (more expressive, handles whitespace-only).
- You need to count array elements — `length` operates on the string representation of the node; use a count function appropriate to the adapter.
- You need substring extraction — use `substring` directly.

## Comparison

| Function | Returns | Null-safe | Use when |
|----------|---------|-----------|----------|
| `length` | integer (char count) | yes (returns 0) | Numeric length is needed for logic or storage |
| `isEmpty` | boolean | yes | Only need to know if blank/null |
| `substring` | string | no | Extracting a portion of the string |

## Common mistakes

- **Counting array items**: `length($.tags)` where `$.tags` is an array will operate on the JSON string representation of the array, not its element count. This is almost never what you want.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].name` as argument produces a flat concatenated string length — not per-element lengths. Use indexed paths.
- **Unicode surrogate pairs**: `length` returns the .NET `char` count, which counts surrogate-pair emoji as 2. For most practical data (text, codes, identifiers) this is not an issue.
