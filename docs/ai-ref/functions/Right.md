# right

> Returns the last N characters of a string.

## Syntax

```
=right(<source>, <count>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. Read as text, so a number can be sliced directly. |
| 2 | number | yes | How many characters to take from the end. |

## Returns

A **string** node with the last `count` characters — always a string, even when the result looks
numeric, so leading zeros survive: `=right(20260818, 4)` gives `"0818"`, not `818`.

Bounds are clamped exactly as `substring` clamps them: a `count` at or above the string length
returns the whole string, and a `count` of zero or less returns `""`. Neither throws.

## Formats

Works with all adapters.

## Verified example

```json
{ "command": "put", "path": "$.last", "value": "=right($.str, 5)" }
```

Input: `{ "str": "Hello World" }`
Output: `{ "str": "Hello World", "last": "World" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/right/01-basic.json`, with sibling fixtures
covering the clamp behavior against `"abc"`: `02-count-above-length.json`
(`=right($.str, 99)` → `"abc"`, the whole string) and `03-zero-count.json`
(`=right($.str, 0)` → `""`).

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.last4\",\"value\":\"=right($.iban,4)\"}]",
    JObject.Parse("{\"iban\":\"NL91ABNA0417164300\"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- Taking a fixed-width **suffix**: the last four digits of an account number, the day part of a
  `yyyyMMdd` value, a two-character country or check code at the end of a reference.
- Anywhere you would otherwise have written
  `=substring($.s, =subtract(=length($.s), 4), 4)` — that is exactly why `right` exists. Three
  nested calls, two of them only to compute an index, collapse to one.

There is deliberately **no `left`**. The head of a string is already `=substring($.s, 0, n)` —
one call, no arithmetic — so a second name for it would add surface without adding reach. `right`
earns its place because the arithmetic version does not read.

## When NOT to use

- You want a slice from the **front** or from the middle — use `substring`. `left(s, n)` is
  `=substring($.s, 0, n)`.
- The suffix is delimited rather than fixed-width — use `split`, or `regexExtract` with a pattern.
- You only need to check that a string *ends with* something — use `endsWith`, which answers a
  boolean and needs no length.

## Comparison

| Expression | Takes | Notes |
|------------|-------|-------|
| `=right($.s, 4)` | last 4 characters | One call, no length arithmetic |
| `=substring($.s, 0, 4)` | first 4 characters — this is `left` | Why no `left` function exists |
| `=substring($.s, =subtract(=length($.s), 4), 4)` | last 4 characters | What `right` replaces |
| `=substring($.s, 4)` | everything from index 4 on | Drops a fixed-width prefix |
| `=endsWith($.s, 'AB')` | boolean | When you are testing, not extracting |

## Common mistakes

- **Count, not an index.** The second argument is *how many characters*, not where to start.
  `=right($.s, 4)` is the last four characters regardless of how long `$.s` is.
- **Over-long counts clamp, they do not fail.** `=right('abc', 99)` is `"abc"`. If you need exactly
  four characters, check the length yourself — the result is silently shorter when the input is.
- **Zero or negative counts give `""`**, not the whole string. A count computed from data that
  turns out to be `null` becomes `0`, and the result is empty rather than an error.
- **The result is a string.** `=right(20260818, 2)` is `"18"`. Feed it to `parse` if you need it
  back as a number.
- **Path not found = failure** — the script aborts if the source path does not resolve. A path that
  resolves to `null` is read as `""`.
