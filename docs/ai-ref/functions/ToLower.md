# toLower

> Converts a string to lowercase.

## Syntax

```
=toLower(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Registered as both `"toLower"` (camelCase, 008+) and `"tolower"` (legacy lowercase).

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to convert. |

## Returns

A string node with all characters converted to lowercase via `string.ToLowerInvariant()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "add", "path": "$.lower", "value": "=toLower($.name)" }
```

Input: `{ "name": "Alice" }`
Output: `{ "name": "Alice", "lower": "alice" }`

## Notes

- `"tolower"` (all-lowercase) is the original registration and remains for backwards compatibility.
- `"toLower"` (camelCase) was added in 008 for JLio parity.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.lower\",\"value\":\"=toLower($.name)\"}]",
    JObject.Parse("{\"name\":\"Alice\"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- Normalising strings before comparison — email addresses, usernames, codes.
- Canonicalising values before storage to ensure consistent casing across records.
- Preparing a field for a subsequent case-sensitive predicate (`contains`, `startsWith`, `endsWith`) when the source data may have mixed casing.
- Most common of the two case functions; prefer `toLower` over `toUpper` for normalisation unless the domain requires uppercase (e.g., ISO codes stored as uppercase).

## When NOT to use

- Display formatting where locale-aware casing is required — `toLower` uses `InvariantCulture`, which may not match user-facing expectations for languages with special casing rules (e.g., Turkish dotless-i).
- The string is already known to be lowercase — the operation is harmless but unnecessary.
- You need UPPERCASE output — use `toUpper` instead.

## Comparison

| Function | Converts to | Culture | Use when |
|----------|------------|---------|----------|
| `toLower` | lowercase | Invariant | Normalisation, comparison prep, storage |
| `toUpper` | UPPERCASE | Invariant | Codes, enums, display labels requiring caps |

## Common mistakes

- **Culture assumption**: `ToLowerInvariant` does not follow locale-specific rules. For Turkish, German, or other locale-sensitive casing, results may differ from expectation.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].name` as argument produces a flat list. Use indexed paths for per-element operations.
- **Applying toLower to numbers or booleans**: the function coerces to string first, then lowercases. Numbers are unaffected; `true`/`false` become `"true"`/`"false"`.
