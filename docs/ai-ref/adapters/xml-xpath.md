# TLio.Xml Adapter — XML (Native XPath)

> XML adapter using native XPath expressions. Use when paths need attribute predicates,
> recursive descent, or positional indexing.

## Setup

```csharp
using TLio.Xml;
using TLio.Client;

var options = ParseOptions<XElement>.CreateDefault();
var context = XmlExecutionContext.CreateWithNativeXPath(data, script);
var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, context);
```

## Path Syntax

| Pattern | Example | Matches |
|---------|---------|---------|
| Root | `.` | Document root element |
| Child | `name` | Direct child element `name` |
| Nested | `address/city` | Nested element |
| Recursive | `//name` | All `name` elements at any depth |
| Indexed | `items/item[1]` | First `item` child (1-based in XPath) |
| Attribute predicate | `items/item[@id='1']` | `item` with attribute `id='1'` |
| Wildcard | `*` | All child elements |
| Any descendant | `.//*` | All descendant elements |

## Notes

- Root is `.` (dot), not `/`. Paths do **not** start with `/`.
- XPath indexing is **1-based** (unlike JSONPath which is 0-based).
- Attribute predicates use `@attr` syntax inside `[...]`.

## See Also

[xml-slashpath.md](xml-slashpath.md) — simpler slash-path model when predicates are not needed.
[overview.md](../overview.md) — adapter selection table.
