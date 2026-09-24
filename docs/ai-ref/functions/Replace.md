# replace

> Replaces all occurrences of a substring within a string.

## Syntax

```
=replace(<source>, <old>, <new>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. |
| 2 | string or path | yes | The substring to find and replace. |
| 3 | string or path | yes | The replacement string. |

## Returns

A string node with all occurrences of `old` replaced by `new`. Case-sensitive (`StringComparison.Ordinal`).

An empty `old` argument is special-cased to return the source string unchanged rather than
throwing — `string.Replace` itself rejects an empty search string, so `replace` guards against it.

## Formats

Works with all adapters. Uses `string.Replace`.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=replace($.str, $.old, $.new)" }
```

Input: `{ "str": "Hello World", "old": "World", "new": "TLio" }`
Output: `{ ..., "result": "Hello TLio" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/replace/01-basic.json` (and
`02-no-occurrence.json`, where `old` does not appear in `str` and the source is returned
unchanged — not a failure).

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.code\",\"value\":\"=replace($.code,'-','_')\"}]",
    JObject.Parse("{\"code\":\"my-value-key\"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- Substituting a character or substring globally throughout a string (e.g., converting hyphens to underscores, removing a prefix token).
- Sanitising strings — replacing forbidden characters with safe equivalents.
- Removing a substring entirely by replacing it with an empty string `''`.
- Normalising delimiters before further processing (e.g., before `split`).

## When NOT to use

- You need regex-based replacement — `replace` uses a literal string match only. There is no regex variant.
- You only want to replace the first occurrence — `replace` always replaces ALL occurrences. There is no `replaceFirst` variant; use `indexOf` + `substring` to construct a single-replacement manually.
- You need a case-insensitive replacement — normalise with `toLower` first, then replace on the normalised copy (be aware the original casing is lost).
- The input may be null — `replace` will fail on null source; guard with `isEmpty` first.

## Comparison

| Function | Scope | Pattern type | Use when |
|----------|-------|-------------|----------|
| `replace` | All occurrences | Literal string | Global literal substitution |
| `substring` + `indexOf` | Single occurrence | Positional | First-occurrence-only replacement |
| `split` + `join` | All occurrences | Delimiter | Replace delimiter with a different one |

## Common mistakes

- **Replace is not regex**: `=replace($.s,'[0-9]+','X')` will NOT replace numbers — it will literally look for the string `"[0-9]+"`. Use `split`/`join` for delimiter swaps or pre-process numerically.
- **All occurrences**: if you expect only the first match to be replaced, `replace` will replace every one. Verify the source data does not have repeated occurrences that should be preserved.
- **Case sensitivity**: `replace($.s,'Hello','Hi')` will NOT replace `"hello"`. Normalise first with `toLower` if needed.
- **Empty replacement string**: `=replace($.s,' ','')` strips all spaces — this is valid and intentional, but easy to do accidentally.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].code` as first argument produces a flat list. Use indexed paths for per-element operations.
