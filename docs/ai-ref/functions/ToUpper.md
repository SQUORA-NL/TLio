# toUpper

> Converts a string to uppercase.

## Syntax

```
=toUpper(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Registered as both `"toUpper"` (camelCase, 008+) and `"toupper"` (legacy lowercase).

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to convert. |

## Returns

A string node with all characters converted to uppercase via `string.ToUpperInvariant()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "add", "path": "$.upper", "value": "=toUpper($.name)" }
```

Input: `{ "name": "Alice" }`
Output: `{ "name": "Alice", "upper": "ALICE" }`

## Notes

- `"toupper"` (all-lowercase) is the original registration and remains for backwards compatibility.
- `"toUpper"` (camelCase) was added in 008 for JLio parity.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.upper\",\"value\":\"=toUpper($.name)\"}]",
    JObject.Parse("{\"name\":\"Alice\"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- Generating uppercase identifiers, ISO country codes, currency codes, enum values, or any domain standard that requires UPPERCASE.
- Display labels or headings that must appear in capitals.
- Normalising data when the downstream system or comparison is uppercase-keyed.

## When NOT to use

- General normalisation for comparison or storage — prefer `toLower` (more common, less aggressive visually).
- Display formatting where locale-aware casing is required — `toUpper` uses `InvariantCulture`.
- The string is already known to be uppercase — the operation is harmless but unnecessary.

## Comparison

| Function | Converts to | Culture | Use when |
|----------|------------|---------|----------|
| `toUpper` | UPPERCASE | Invariant | Codes, enums, display labels requiring caps |
| `toLower` | lowercase | Invariant | Normalisation, comparison prep, storage |

## Common mistakes

- **Culture assumption**: `ToUpperInvariant` does not follow locale-specific rules. Locale-sensitive casing (e.g., Turkish, German) may not match expectation.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].code` as argument produces a flat list. Use indexed paths for per-element operations.
- **Applying toUpper to numbers or booleans**: the function coerces to string first. `true` becomes `"TRUE"`. Verify this is the intended result.
