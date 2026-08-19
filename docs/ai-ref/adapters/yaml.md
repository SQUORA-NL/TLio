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
| Recursive descent | `$..city` | Key `city` at any depth |
| Anchored descent | `$.order..city` | Key `city` at any depth below `$.order` |

## Notes

- Keys are **case-sensitive**.
- Multi-document YAML (documents separated by `---`) is parsed as an array at root;
  use `$[0]`, `$[1]`, etc. to address individual documents.
- No filter expression support (`?(...)`) — for complex filtering, pre-process the YAML
  or convert to JSON.

## Document shape

YAML carries the JSON data model directly — mappings, sequences, scalars and null all exist —
so the mapping is one to one. See [document-shape.md](document-shape.md) for the comparison
with XML, which cannot say all of it.

Scalars are untyped: `n: 42` is the characters `42`, so `=isNumber()` reports the apparent type
rather than a declared one.

## See Also

[document-shape.md](document-shape.md) — how one document looks in all three formats.
[script-notation.md](script-notation.md) — the YAML script notation.
[overview.md](../overview.md) — adapter selection table.
