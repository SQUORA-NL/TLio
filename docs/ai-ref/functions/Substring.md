# substring

> Extracts a portion of a string starting at a given index, with an optional length limit.

## Syntax

```
=substring(<source>, <start>)
=substring(<source>, <start>, <count>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. |
| 2 | number | yes | Zero-based start index. |
| 3 | number | no | Maximum number of characters to extract. Omit to extract to end of string. |

## Returns

A **string** node containing the extracted substring — always, even when the source was a number
and even when the result looks numeric. `=substring(20260818, 0, 4)` gives the string `"2026"`,
so leading zeros survive: `=substring(20260818, 4, 2)` gives `"08"`, not `8`.

The input is read as text too, so a number, boolean or date value can be sliced directly without
converting it first.

Only the index arguments are numeric. Everything text functions consume and produce is text;
`length` and `indexOf` are the exceptions, since they answer "how many" and "where".

## Formats

Works with all adapters. Uses `string.Substring`.

## Verified example

Two-argument form (to end of string):

```json
{ "command": "put", "path": "$.result", "value": "=substring($.str, $.start)" }
```

Input: `{ "str": "Hello World", "start": 6, "len": 5 }`
Output: `{ ..., "result": "World" }`

Three-argument form (explicit length) gives the same result here:
`=substring($.str, $.start, $.len)` → `"World"`.

Verified by: `TLio.Functions.Tests/Fixtures/Text/substring/01-from-start.json`,
`02-with-length.json`, and `03-from-zero.json` (`=substring($.str, 0, 5)` → `"Hello"`).

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

## When to use

- Extracting a fixed-width prefix or suffix from a string (e.g., first 3 chars of a code).
- Extracting from a computed position — combine with `indexOf` to find the position first, then `substring` to extract.
- Stripping a known prefix or suffix of fixed length from a string.
- Splitting a string at a specific character position without needing an array result.

## When NOT to use

- You only need to check whether a string starts or ends with a value — use `startsWith` / `endsWith` (no extraction needed).
- You need to split a string on a delimiter into multiple parts — use `split` instead.
- You only need to check presence of a substring — use `contains`.
- The input may be null — `substring` will fail on null; guard with `isEmpty` first.

## Comparison

| Function | Returns | Use when |
|----------|---------|----------|
| `substring` | string (extracted slice) | Specific character range is needed |
| `split` | array | Breaking on a delimiter into multiple parts |
| `indexOf` | integer (position) | Finding where to start the extraction |
| `startsWith` / `endsWith` | boolean | Only checking prefix/suffix, not extracting |

## Common mistakes

- **Start+count, NOT start+end**: the third argument is a CHARACTER COUNT, not an end index. `substring($.s, 2, 5)` returns 5 characters starting at index 2, not characters 2 through 5. This is the most common mistake.
- **Zero-based indexing**: the first character is at index `0`. `substring($.s, 1, 3)` skips the first character.
- **Out-of-bounds clamping**: if `start` exceeds string length or `count` would go past the end, the result is clamped to the available characters rather than throwing. Always verify the result length if exact output is required.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].code` as first argument produces a flat list. Use indexed paths for per-element operations.
