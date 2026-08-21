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

## Example

```json
{ "command": "add", "path": "$.postcode", "value": "=regexReplace($.postcodeRaw, '^(\\d{4})([a-zA-Z]{2})$', '$1 $2')" }
```

Input: `{ "postcodeRaw": "1234ab" }`
Output: `{ "postcodeRaw": "1234ab", "postcode": "1234 ab" }`

Stripping separators out of a licence plate:

```json
{ "command": "add", "path": "$.plateCompact", "value": "=regexReplace($.plate, '[^A-Z0-9]', '')" }
```

Input: `{ "plate": "XX-99-YY" }`
Output: `{ "plate": "XX-99-YY", "plateCompact": "XX99YY" }`

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
