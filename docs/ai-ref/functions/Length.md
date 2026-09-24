# length

> Returns the number of characters in a string, or the element count for an array.

## Syntax

```
=length(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string, array or path | yes | The value whose length to measure. |

## Returns

A numeric node. For an array argument it is the **element count** (`GetArrayLength`, not a
stringified-then-measured length). For a null argument it is `0`. For anything else it is the
character count of the string form (`string.Length`). A path matching nothing fails the script.

## Formats

Works with all adapters. Branches on `IsArray` first, so the array-vs-string distinction follows
each adapter's own notion of "array" (see `docs/ai-ref/adapters/document-shape.md`).

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=length($.str)" }
```

Input: `{ "str": "Hello", "arr": [1, 2, 3], "empty": "", "nul": null }`
Output: `{ ..., "result": 5 }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/length/01-string.json`, and at the unit level by
`LengthTests.Length_OfString_ReturnsCharacterCount` in
`TLio.Functions.Tests/FunctionsTests/TextTests/LengthTests.cs`.

The array case — the one the name misleads about:

```json
{ "command": "put", "path": "$.result", "value": "=length($.arr)" }
```

Input: `{ "str": "Hello", "arr": [1, 2, 3], "empty": "", "nul": null }`
Output: `{ ..., "result": 3 }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/length/02-array.json`, and at the unit level by
`LengthTests.Length_OfArray_ReturnsElementCount` in
`TLio.Functions.Tests/FunctionsTests/TextTests/LengthTests.cs` — asserted against the actual
source in `TLio.Extensions.Text/Length.cs`, which branches on `context.NodeAdapter.IsArray(node)`
and calls `GetArrayLength` rather than measuring any string form. The same fixture directory also
covers the empty-string (`03-empty-string.json`, `result: 0`) and null (`04-null.json`,
`result: 0`) cases.

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
- You need the element count of an array — `length($.items)` is the count, not a stringified length.
- You want to store the string length as a derived field.
- You need the length as input for a `substring` or `padLeft`/`padRight` calculation.
- You want a numeric zero for a *present-but-null* field without throwing — `length` returns `0` for a found-null value (a **missing** path still fails the script; see Returns).

## When NOT to use

- You only need to know if a string or array is blank/null — use `isEmpty` instead (also handles
  the missing-path case as a failure the same way `length` does; see `isEmpty`'s own Returns).
- You need substring extraction — use `substring` directly.
- You need per-element lengths across an array — see the wildcard-path mistake below.

## Comparison

| Function | Returns | Array argument | Missing path | Found-null | Use when |
|----------|---------|-----------------|---------------|------------|----------|
| `length` | integer | element count | fails the script | `0` | Numeric length or count is needed for logic or storage |
| `isEmpty` | boolean | `true` for `[]` | fails the script | `true` | Only need to know if blank/null |
| `substring` | string | n/a | fails | n/a | Extracting a portion of the string |

## Common mistakes

- **Assuming an array is stringified first.** It is not — `length($.tags)` on an array branches
  to the element count (`GetArrayLength`), exactly like `count($.tags[*])` would. Confirmed
  against `TLio.Extensions.Text/Length.cs` and
  `LengthTests.Length_OfArray_ReturnsElementCount`.
- **Expecting `0` for a missing path.** `0` is only returned for a value that is *present and
  null*. A path that matches nothing fails the whole script, the same as most other functions in
  this library — it is not a silent zero.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].name` as argument selects each name as a separate node in the
  match, so `length` reports the length of only the *first* matched string — it does not sum or
  return per-element lengths. Use indexed paths, or `sum`/`map`-style composition, for per-element work.
- **Unicode surrogate pairs**: `length` returns the .NET `char` count, which counts surrogate-pair emoji as 2. For most practical data (text, codes, identifiers) this is not an issue.
