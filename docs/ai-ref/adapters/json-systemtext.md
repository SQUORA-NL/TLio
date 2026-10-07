# TLio.Json.SystemText Adapter — JSON (System.Text.Json)

> JSON adapter using System.Text.Json and the `TLio.JsonPath` engine. Paths are read in Newtonsoft's
> JSONPath dialect by default (identical to `TLio.Json`); RFC 9535 is opt-in. Use when Newtonsoft.Json
> is excluded from your dependencies.

## Setup

```csharp
using TLio.Json.SystemText;
using TLio.Client;

var options = ParseOptions<JsonNode>.CreateDefault();
var context = SystemTextJsonExecutionContext.CreateDefault();   // Newtonsoft dialect
// SystemTextJsonExecutionContext.CreateDefault(JsonPathDialect.Rfc9535)   — strict RFC 9535
// SystemTextJsonExecutionContext.CreateDefault(JsonPathDialect.Extended)  — Newtonsoft + RFC where Newtonsoft has no answer
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
| Last element | `$.items[-1:]` (slice); `$.items[-1]` only in `Rfc9535`/`Extended` | Last element |
| Wildcard | `$.items[*]` | All array elements |
| Recursive | `$..name` | All `name` properties at any depth |
| Filter | `$.items[?(@.active == true)]` | Elements where `active` is true (`[?@.active == true]` in `Rfc9535`/`Extended`) |
| Slice | `$.items[0:2]` | Elements 0 and 1 |
| Union | `$.items[0,2]` | Elements at index 0 and 2 |

## Notes

- Backed by `System.Text.Json` (`JsonNode` node type). No third-party dependency.
- Paths are evaluated directly on the document's nodes: selected nodes are live (parent references intact),
  nothing is serialized or copied, and selection sees mutations made earlier in the same script.
- An invalid path **throws** (`JsonPathException`); a path selecting several nodes throws from `SelectNode`
  (like Newtonsoft's `SelectToken`) — use `SelectNodes`.
- A member whose value is JSON `null` is selectable and distinct from an absent member.
- Script expressions `()` are not supported by any adapter.
- Newtonsoft parses date-looking strings into typed dates when it loads a document; a `JsonNode`
  holds them as strings. The default dialect emulates Newtonsoft's date comparison in filters
  (`JsonPathOptions.EmulateNewtonsoftDates`); function behaviour on such values can still differ
  from `TLio.Json` unless the Newtonsoft document is parsed with `DateParseHandling.None`.

## See Also

[overview.md](../overview.md) — adapter selection table and full JSONPath comparison.
