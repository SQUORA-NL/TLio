# regexExtract

> Pulls the first match of a regular expression — or one of its capture groups — out of a string.

## Syntax

```
=regexExtract(<source>, <pattern>)
=regexExtract(<source>, <pattern>, <group>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. Read as text, so a number or date can be matched directly. |
| 2 | string or path | yes | A .NET regular expression. Quote it. |
| 3 | integer | no | Capture group index. Default `0`, the whole match. |

## Returns

A **string** node: the matched text, or the text of the requested capture group.

**No match returns an empty string, not a failure.** `indexOf` sets that precedent by answering
`-1` rather than failing — an absent match is an answer. A failure would abort the whole script
(see [behaviour decisions](../../behaviour-decisions.md) B2), which is wrong for an extractor whose
result you are about to hand to `isEmpty`.

A group index the pattern does not have is the opposite case: that is an authoring error, so it
logs an error and fails. So does an invalid pattern, and a pattern that exceeds the one-second
match timeout.

**That distinction is the whole point of this page**: *the data did not match* is an empty string
you can branch on; *the script asks for a group that does not exist* stops the run. The first is
about the document, the second is about the script.

## Formats

Works with all adapters. Uses .NET `Regex.Match` with a one-second match timeout.

## Verified example

```json
{ "command": "put", "path": "$.bank", "value": "=regexExtract($.iban, '[A-Z]{4}')" }
```

Input: `{ "iban": "NL91ABNA0417164300" }`
Output: `{ "iban": "NL91ABNA0417164300", "bank": "ABNA" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/regexextract/01-whole-match.json`.

With a capture group — the letters half of a Dutch postcode:

```json
{ "command": "put", "path": "$.letters", "value": "=regexExtract($.postcode, '^(\\d{4})\\s*([A-Z]{2})$', 2)" }
```

Input: `{ "postcode": "1234 AB" }`
Output: `{ "postcode": "1234 AB", "letters": "AB" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/regexextract/02-capture-group.json`.

No match — an answer, not an error:

```json
{ "command": "put", "path": "$.digitsOnly", "value": "=regexExtract($.postcode, '^\\d{9}$')" }
```

Input: `{ "postcode": "1234 AB" }`
Output: `{ "postcode": "1234 AB", "digitsOnly": "" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/regexextract/03-no-match-empty-string.json`.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.bank\",\"value\":\"=regexExtract($.iban,'[A-Z]{4}')\"}]",
    JObject.Parse("{\"iban\":\"NL91ABNA0417164300\"}"),
    JsonExecutionContext.CreateDefault());
```

## Performance

`regexExtract` constructs a **fresh `Regex` instance on every call** —
`new Regex(pattern, RegexOptions.None, MatchTimeout)` in `RegexExtract.cs`. This is different from
`regexReplace` (see [its Performance section](RegexReplace.md#performance)), which calls the
static `Regex.Replace(...)` overload and benefits from .NET's built-in process-wide regex cache
(15 entries by default, keyed on pattern/options/timeout). Because `regexExtract` always calls
`new Regex(...)` directly, it never reads from or writes to that cache — the same pattern re-used
across thousands of rows in a script is recompiled from its text every single call. For most
patterns and row counts this overhead is negligible next to the rest of the pipeline, but in a
tight, high-volume loop with a complex pattern, this is the one to watch: there is currently no
way from script authoring alone to avoid the recompilation, since the function is stateless by
design (see `EXECUTION_CONCURRENCY_INVESTIGATION.md` on why functions avoid instance-level mutable
state).

## When to use

- Pulling a fragment out of a value whose position is not fixed: the bank code inside an IBAN, the
  digits half of a postcode, a reference number embedded in a free-text field.
- Splitting a value that has no delimiter to split on, by describing its shape instead.
- Optional extraction — you want `""` when the value does not have the fragment, and you want the
  script to carry on.

## When NOT to use

- The fragment is at a fixed offset — use `substring` (or `right`). Index arithmetic is clearer
  than a pattern when the shape really is fixed-width.
- The value has a clean delimiter and you want all the parts — use `split`, which returns an array.
- You only need the *position* of something — use `indexOf`.
- You only need to know *whether* the value matches — use `matches`.

## Comparison

| Function | Locates by | Returns | No match |
|----------|-----------|---------|----------|
| `regexExtract` | pattern | string (one fragment) | `""` |
| `substring` | fixed index and count | string (one slice) | clamped to available characters |
| `indexOf` | literal text | integer (position) | `-1` |
| `split` | literal delimiter | array (all parts) | single-element array |
| `matches` | pattern | boolean | `false` |

## Common mistakes

- **A backslash has to survive the JSON layer first.** In a script *file* the pattern lives inside
  a JSON string, so backslashes must be doubled: `"=regexExtract($.iban,'\\d{4}')"` gives the
  regex engine `\d{4}`. Writing `'\d{4}'` is an invalid JSON escape and the script will not parse.
  This is the mistake that actually bites people.
- **Quote the pattern.** An unquoted argument is split on commas, so `\d{2,4}` becomes two
  arguments. Quoted, commas inside the pattern are safe.
- **Empty string means "did not match", not "failed".** Guard with `isEmpty` if the difference
  matters; do not look for an error in the log, there is none.
- **A missing group *does* fail.** `=regexExtract($.s, '^(\\d+)$', 2)` stops the script — the
  pattern has one group, not two. Group `0` is the whole match, group `1` is the first
  parenthesised group.
- **Only the first match is returned.** There is no "all matches" form; use `split` or a wildcard
  path when you need every occurrence.
- **Unanchored by default.** `'[A-Z]{4}'` finds four capitals anywhere in the value. Anchor with
  `^` and `$` for a whole-value match.
- **Path not found = failure** — if the source path does not resolve, the function fails and the
  script aborts. A path that resolves to `null` is read as `""`, which then matches nothing.
