# trimStart

> Removes leading (left-side) whitespace from a string.

## Syntax

```
=trimStart(<source>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Registered as both `"trimStart"` (camelCase, 008+) and `"trimstart"` (legacy lowercase).

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to left-trim. |

## Returns

A string node with leading whitespace removed via `string.TrimStart()`.

## Formats

Works with all adapters (JSON, XML, YAML).

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=trimstart($.padded)" }
```

Input: `{ "padded": "  hello  " }`
Output: `{ "padded": "  hello  ", "result": "hello  " }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/trimstart/01-left-only.json`.

## Notes

- `"trimstart"` (all-lowercase) is the original registration and remains for backwards compatibility.
- `"trimStart"` (camelCase) was added in 008 for JLio parity.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.name\",\"value\":\"=trimStart($.name)\"}]",
    JObject.Parse("{\"name\":\"  Alice\"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- You need to strip leading whitespace while intentionally preserving any trailing whitespace (e.g., trailing newlines that carry meaning in multiline content).
- Aligning or indented text where only the left margin needs cleaning.
- Processing code or structured text where trailing spaces/newlines are significant.

## When NOT to use

- You want to strip whitespace from both ends — use `trim` instead (simpler, more common).
- You want to strip only trailing whitespace — use `trimEnd` instead.
- You want to detect blank strings — use `isEmpty` instead.

## Comparison

| Function | Removes | Use when |
|----------|---------|----------|
| `trimStart` | Leading whitespace only | Preserve trailing content |
| `trimEnd` | Trailing whitespace only | Preserve leading content |
| `trim` | Leading + trailing whitespace | Default; clean both ends |

## Common mistakes

- **Using trimStart when trim is intended**: if you need both ends clean, use `trim`. Using `trimStart` alone will leave trailing spaces.
- **Unicode whitespace**: removes all Unicode whitespace (`\t`, `\n`, `\r`, `\f`, etc.), not only ASCII space.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].label` as argument produces a flat list. Use indexed paths for per-element operations.
