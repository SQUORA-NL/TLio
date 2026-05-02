# trim

> Removes leading and trailing whitespace from a string.

## Syntax

```
=trim(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to trim. |

## Returns

A string node with leading and trailing whitespace removed via `string.Trim()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Example

```json
{ "command": "set", "path": "$.name", "value": "=trim($.name)" }
```

Input: `{ "name": "  Alice  " }`
Output: `{ "name": "Alice" }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.name\",\"value\":\"=trim($.name)\"}]",
    JObject.Parse("{\"name\":\"  Alice  \"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- Cleaning user-submitted or inbound data that may carry accidental leading/trailing whitespace.
- Normalising values before comparison, storage, or joining.
- The default choice when you need to strip whitespace — `trim` removes both ends and is the most common variant.

## When NOT to use

- You only want to remove leading whitespace (e.g., preserve trailing newlines) — use `trimStart` instead.
- You only want to remove trailing whitespace (e.g., preserve leading indentation) — use `trimEnd` instead.
- You want to detect blank strings (null/empty/whitespace) — use `isEmpty` instead.

## Comparison

| Function | Removes | Use when |
|----------|---------|----------|
| `trim` | Leading + trailing whitespace | Default; clean both ends |
| `trimStart` | Leading whitespace only | Preserve trailing content |
| `trimEnd` | Trailing whitespace only | Preserve leading content |
| `isEmpty` | N/A — boolean check | Need to detect null/blank |

## Common mistakes

- **Unicode whitespace**: `trim` removes all Unicode whitespace characters (`\t`, `\n`, `\r`, `\f`, non-breaking space, etc.), not only the ASCII space. This is usually what you want, but be aware when processing structured text with intentional non-breaking spaces.
- **Null input**: `trim` returns an empty string for null input (does not throw), matching `.NET`'s `string.Trim()` behaviour on an empty string. However, verify this is acceptable for your use case.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].label` as argument produces a flat list. Use indexed paths for per-element operations.
- **Trim does not collapse internal whitespace**: `trim("  hello   world  ")` → `"hello   world"`. Internal spaces are untouched; use `replace` to collapse them if needed.
