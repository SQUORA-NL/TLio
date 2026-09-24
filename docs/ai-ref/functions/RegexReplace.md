# regexReplace

> Rewrites every match of a regular expression in a string, with capture-group backreferences.

## Syntax

```
=regexReplace(<source>, <pattern>, <replacement>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. Read as text, so numbers and dates can be rewritten directly. |
| 2 | string or path | yes | A .NET regular expression. Quote it. |
| 3 | string or path | yes | The replacement text. `$1`, `$2`, … are backreferences to capture groups; `$$` is a literal dollar sign. |

## Returns

A **string** node with every match replaced. When nothing matches, the source is returned
unchanged — that is not a failure.

An invalid pattern, or a pattern that exceeds the one-second match timeout, logs an error and
fails, which aborts the script.

## Formats

Works with all adapters. Uses .NET `Regex.Replace` with a one-second match timeout.

## Verified example

```json
{ "command": "put", "path": "$.formatted", "value": "=regexReplace($.postcode, '^(\\d{4})([a-zA-Z]{2})$', '$1 $2')" }
```

Input: `{ "postcode": "1234ab" }`
Output: `{ "postcode": "1234ab", "formatted": "1234 ab" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/regexreplace/02-backreference.json`.

Stripping separators out of a licence plate:

```json
{ "command": "put", "path": "$.compact", "value": "=regexReplace($.plate, '[^A-Z0-9]', '')" }
```

Input: `{ "plate": "XX-99-YY" }`
Output: `{ "plate": "XX-99-YY", "compact": "XX99YY" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/regexreplace/01-strip-separators.json`.

No match — the source is returned unchanged:

```json
{ "command": "put", "path": "$.formatted", "value": "=regexReplace($.postcode, '^\\d{6}$', 'X')" }
```

Input: `{ "postcode": "1234ab" }`
Output: `{ "postcode": "1234ab", "formatted": "1234ab" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/regexreplace/03-no-match-unchanged.json`.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.compact\",\"value\":\"=regexReplace($.plate,'[^A-Z0-9]','')\"}]",
    JObject.Parse("{\"plate\":\"XX-99-YY\"}"),
    JsonExecutionContext.CreateDefault());
```

## Performance

`regexReplace` calls the **static** `Regex.Replace(str, pattern, replacement, RegexOptions.None,
MatchTimeout)` overload rather than constructing a `Regex` instance itself. .NET's static
`Regex.Replace`/`Match`/`IsMatch` methods share a process-wide cache of compiled regexes (15
entries by default, keyed on pattern + options + timeout), so calling `regexReplace` with the same
pattern repeatedly — the common case of one script's pattern applied across many rows — reuses the
compiled regex from the second call onward instead of recompiling it each time. This is the
opposite of `regexExtract` (see [its Performance section](RegexExtract.md#performance)), which
constructs `new Regex(...)` directly and therefore never touches that cache. If a script rotates
through more than 15 distinct patterns in the same process, older entries are evicted and get
recompiled again on next use — worth knowing if a very large, varied set of `regexReplace` calls
runs in one long-lived process.

## When to use

- Reformatting a value whose shape is known but whose separators are not: postcodes, licence
  plates, IBANs, phone numbers.
- Removing a whole *class* of characters — `'[^0-9]'` strips everything that is not a digit,
  which `replace` cannot express at all.
- Reordering parts of a value in one step with backreferences: `'^(\\d+)-(\\d+)$'` → `'$2-$1'`.

## When NOT to use

- The text you are removing or swapping is a fixed literal — use `replace`. It is cheaper, has no
  escaping rules and cannot be written wrong.
- You only need to know *whether* the value matches — use `matches`, which answers a boolean.
- You want a *piece* of the value rather than a rewritten whole — use `regexExtract`.

## Comparison

| Function | Pattern? | Returns | Use when |
|----------|----------|---------|----------|
| `replace` | no — literal text | string (rewritten) | The old value is a fixed literal |
| `regexReplace` | yes | string (rewritten) | The old value is a shape, or parts must be reordered |
| `matches` | yes | boolean | You only need a yes/no answer |
| `regexExtract` | yes | string (a fragment) | You want a piece of the value, not a rewrite |

## Common mistakes

- **A backslash has to survive the JSON layer first.** In a script *file* the pattern is inside a
  JSON string, so every backslash must be doubled: write `'^\\d{4}$'` to give the regex engine
  `^\d{4}$`. A single `\d` in JSON is an invalid escape and the script will not even parse. This
  is the mistake that actually bites people.
- **Quote the pattern.** An unquoted argument is split on commas, so `\d{2,4}` becomes two
  arguments and the call fails on arity. `'\\d{2,4}'` is safe — commas inside quotes are kept.
- **Quote the replacement too.** `'$1 $2'` quoted is substitution syntax; unquoted, `$1` reads as
  the start of a path expression.
- **Unanchored by default.** `=regexReplace($.code, 'AB', 'X')` rewrites `AB` anywhere in the
  value, not only a whole-value match. Anchor with `^` and `$` when you mean the whole value.
- **`$` in the replacement is special.** To output a literal dollar sign, write `$$`.
- **No match is not an error.** The source comes back unchanged, so a wrong pattern shows up as
  "nothing happened", not as a failed script. Check the output, not the log.
- **Path not found = failure** — if the source path does not resolve, the function fails and the
  script aborts. A path that resolves to `null` is read as `""`.
