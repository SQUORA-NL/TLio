# TLio.Json Adapter — JSON (Newtonsoft)

> JSON adapter using Newtonsoft.Json with Goessner JSONPath. Most permissive JSONPath
> variant; use when scripts require filter expressions or script expressions `()`.

## Setup

```csharp
using TLio.Json;
using TLio.Client;

var options = ParseOptions<JToken>.CreateDefault();
var context = JsonExecutionContext.Create(data, script, options);
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
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
| Script expr | `$.items[(@.length-1)]` | Dynamic index via expression |
| Slice | `$.items[0:2]` | Elements 0 and 1 |
| Union | `$.items[0,2]` | Elements at index 0 and 2 |

## Notes

- Backed by `Newtonsoft.Json` (`JToken` node type).
- Supports script expressions `()` — not available in `TLio.Json.SystemText`.
- Most lenient with malformed paths; returns empty result rather than throwing.

## See Also

[overview.md](../overview.md) — adapter selection table and full JSONPath comparison.
