# TLio.Xml Adapter — XML (Slash Paths)

> XML adapter using slash-separated paths (`/child/grandchild`). Simplest XML path model;
> use when paths are straightforward hierarchies with no predicates needed.

## Setup

```csharp
using TLio.Xml;
using TLio.Client;

var options = ParseOptions<XElement>.CreateDefault();
var context = XmlExecutionContext.CreateWithSlashPaths(data, script);
var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, context);
```

## Path Syntax

| Pattern | Example | Matches |
|---------|---------|---------|
| Root | `/` | Document root element |
| Child | `/name` | Direct child element `name` |
| Nested | `/address/city` | Nested element |
| Wildcard | `/items/*` | All child elements of `items` |
| Multiple levels | `/a/b/c` | Element `c` inside `b` inside `a` |

## Notes

- Root is `/` (slash). All paths start with `/`.
- No predicate (`[@attr='val']`) support — use `xml-xpath` adapter for that.
- Element names are case-sensitive.

## See Also

[xml-xpath.md](xml-xpath.md) — for XPath predicates, recursive descent, and indexed access.
[overview.md](../overview.md) — adapter selection table.
