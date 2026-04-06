# TLio.Yaml Adapter — YAML

> YAML adapter using dot-notation paths (`$.root.child`). Multi-document YAML (separated
> by `---`) is parsed as an array root.

## Setup

```csharp
using TLio.Yaml;
using TLio.Client;

var options = ParseOptions<YamlNode>.CreateDefault();
var context = YamlExecutionContext.Create(data, script, options);
var engine  = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, context);
```

## Path Syntax

| Pattern | Example | Matches |
|---------|---------|---------|
| Root | `$` | Document root |
| Child | `$.name` | Direct key `name` |
| Nested | `$.address.city` | Nested key |
| Array index | `$.items[0]` | First element (0-based) |
| Wildcard | `$.items[*]` | All array elements |

## Notes

- Keys are **case-sensitive**.
- Multi-document YAML (documents separated by `---`) is parsed as an array at root;
  use `$[0]`, `$[1]`, etc. to address individual documents.
- No filter expression support (`?(...)`) — for complex filtering, pre-process the YAML
  or convert to JSON.

## See Also

[overview.md](../overview.md) — adapter selection table.
