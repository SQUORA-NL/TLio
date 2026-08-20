# TLio.Xml Adapter — XML (Native XPath)

> XML adapter using native XPath expressions. Use when paths need attribute predicates,
> recursive descent, or positional indexing.

## Setup

```csharp
using TLio.Xml;
using TLio.Client;

var options = ParseOptions<XElement>.CreateDefault();
var context = XmlExecutionContext.CreateWithNativeXPath();
var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
var data    = context.NodeAdapter.Parse(xmlText);
var result  = engine.Execute(scriptJson, data, context);
```

## Paths start at the document node

Paths are ordinary XPath, anchored where XPath anchors them:

| XDM concept | Path | Named? |
|---|---|---|
| Document node | `/` | No — it is not an element |
| Document **element** | `/order` | Yes, `order` |
| A child of it | `/order/customer` | Yes, `customer` |

**The document element is always named in the path.** For
`<order><customer>Ada</customer></order>` the customer element is `/order/customer`.

A bare step such as `customer` is `child::customer` *of the document node*, whose only
element child is `<order>` — so it matches nothing and the command warns. This is XPath's
own rule: a relative step never skips a level. When you mean "at any depth", write
`//customer`.

## Path Syntax

| Pattern | Example | Matches |
|---------|---------|---------|
| Document node | `/` | Not an element — selects nothing |
| Document element | `/order` | The root element itself |
| Child | `/order/name` | Direct child element `name` |
| Nested | `/order/address/city` | Nested element |
| Recursive | `//name` | All `name` elements at any depth |
| Indexed | `/order/items/item[1]` | First `item` child (1-based in XPath) |
| Attribute predicate | `/order/items/item[@id='1']` | `item` with attribute `id='1'` |
| Wildcard | `/order/*` | All children of the document element |
| Any descendant | `//*` | All descendant elements |

## Notes

- XPath indexing is **1-based** (unlike JSONPath, which is 0-based).
- Attribute predicates use `@attr` syntax inside `[...]`.
- Paths containing predicates, axes or `//` are never scaffolded by `put`/`add` — there is
  no single structure such a path describes.
- `Parse` returns an element still attached to its `XDocument`. If you build an `XElement`
  by hand, wrap it in an `XDocument` first or absolute paths will not resolve.

## The root element

| Goal | How |
|---|---|
| Rename it (`<order>` → `<opdracht>`) | `{"command":"rename","path":"/order","name":"opdracht"}` |
| Replace its content | `{"command":"move","fromPath":"/order/replacement","toPath":"/"}` |
| Set a top-level field | `{"command":"set","path":"/order/customer","value":"Grace"}` |

`set` and `put` on `/` warn: the document node has no property name to write.

## Document shape

The JSON ↔ XML mapping every command relies on — arrays as repeated child elements, the
ambiguous empty element, attributes being out of scope — is in
[document-shape.md](document-shape.md).

## See Also

[document-shape.md](document-shape.md) — the JSON ↔ XML mapping.
[script-notation.md](script-notation.md) — the XML script notation.

[xml-slashpath.md](xml-slashpath.md) — the same anchoring without predicates or axes.
[../commands/Rename.md](../commands/Rename.md) — renaming elements, including the root.
[overview.md](../overview.md) — adapter selection table.
