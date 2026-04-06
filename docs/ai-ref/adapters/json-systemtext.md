# TLio.Json.SystemText Adapter — JSON (System.Text.Json)

> JSON adapter using System.Text.Json with RFC 9535-compliant JSONPath. Use when
> Newtonsoft.Json is excluded from your dependencies or strict RFC compliance is required.

## Setup

```csharp
using TLio.Json.SystemText;
using TLio.Client;

var options = ParseOptions<JsonNode>.CreateDefault();
var context = SystemTextJsonExecutionContext.Create(data, script, options);
var engine  = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, context);
```

## Path Syntax

| Pattern | Example | Matches |
|---------|---------|---------|
| Root | `$` | Document root |
| Child | `$.name` | Direct property `name` |
| Nested | `$.address.city` | Nested property |
| Array index | `$.items[0]` | First element (0-based) |
| Last element | `$.items[-1]` | Last element |
| Wildcard | `$.items[*]` | All array elements |
| Recursive | `$..name` | All `name` properties at any depth |
| Filter | `$.items[?(@.active == true)]` | Elements where `active` is true |
| Slice | `$.items[0:2]` | Elements 0 and 1 |
| Union | `$.items[0,2]` | Elements at index 0 and 2 |

## Notes

- Backed by `System.Text.Json` (`JsonNode` node type).
- **Does not support** script expressions `()` — use `TLio.Json` (Newtonsoft) for those.
- Internally serializes `JsonNode` to `JsonDocument` for path evaluation, then navigates
  the original tree — parent references are preserved correctly.

## See Also

[overview.md](../overview.md) — adapter selection table and full JSONPath comparison.
