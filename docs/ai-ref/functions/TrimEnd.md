# trimEnd

> Removes trailing (right-side) whitespace from a string.

## Syntax

```
=trimEnd(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Registered as both `"trimEnd"` (camelCase, 008+) and `"trimend"` (legacy lowercase).

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to right-trim. |

## Returns

A string node with trailing whitespace removed via `string.TrimEnd()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=trimend($.padded)" }
```

Input: `{ "padded": "  hello  " }`
Output: `{ "padded": "  hello  ", "result": "  hello" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/trimend/01-right-only.json`.

## Notes

- `"trimend"` (all-lowercase) is the original registration and remains for backwards compatibility.
- `"trimEnd"` (camelCase) was added in 008 for JLio parity.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.name\",\"value\":\"=trimEnd($.name)\"}]",
    JObject.Parse("{\"name\":\"Alice  \"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- You need to strip trailing whitespace while intentionally preserving any leading whitespace (e.g., leading indentation that carries meaning).
- Cleaning up log lines or multi-line strings where trailing spaces or newlines should be removed but leading indentation should be kept.
- Processing structured text where leading whitespace is significant (e.g., YAML-style indented blocks).

## When NOT to use

- You want to strip whitespace from both ends — use `trim` instead (simpler, more common).
- You want to strip only leading whitespace — use `trimStart` instead.
- You want to detect blank strings — use `isEmpty` instead.

## Comparison

| Function | Removes | Use when |
|----------|---------|----------|
| `trimEnd` | Trailing whitespace only | Preserve leading content |
| `trimStart` | Leading whitespace only | Preserve trailing content |
| `trim` | Leading + trailing whitespace | Default; clean both ends |

## Common mistakes

- **Using trimEnd when trim is intended**: if you need both ends clean, use `trim`. Using `trimEnd` alone will leave leading spaces.
- **Unicode whitespace**: removes all Unicode whitespace (`\t`, `\n`, `\r`, `\f`, etc.), not only ASCII space.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].label` as argument produces a flat list. Use indexed paths for per-element operations.
