# TLio.Xml Adapter — XML (Slash Paths)

> XML adapter for straightforward hierarchies (`/order/address/city`). The simple-hierarchy
> subset of the XPath adapter — same anchoring, no predicates or axes.

## Setup

```csharp
using TLio.Xml;
using TLio.Client;

var options = ParseOptions<XElement>.CreateDefault();
var context = XmlExecutionContext.CreateWithSlashPaths();
var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
var data    = context.NodeAdapter.Parse(xmlText);
var result  = engine.Execute(scriptJson, data, context);
```

## Paths start at the document node

XPath distinguishes two things that are easy to conflate, and TLio follows it exactly:

| XDM concept | Path | Named? |
|---|---|---|
| Document node | `/` | No — it is not an element |
| Document **element** | `/order` | Yes, `order` |
| A child of it | `/order/customer` | Yes, `customer` |

**The document element is always named in the path.** For
`<order><customer>Ada</customer></order>` the customer element is `/order/customer`.

`/customer` and `customer` both mean "a `customer` child of the document node", which does
not exist — the document node's only element child is `<order>`. They match nothing and the
command warns. Nothing in XPath lets a relative step skip a level; use `//customer` when you
mean "at any depth".

## Path Syntax

| Pattern | Example | Matches |
|---------|---------|---------|
| Document node | `/` | Not an element — selects nothing |
| Document element | `/order` | The root element itself |
| Child | `/order/name` | Direct child element `name` |
| Nested | `/order/address/city` | Nested element |
| Wildcard | `/order/items/*` | All child elements of `items` |
| Recursive descent | `//city` | `city` at any depth |

## Notes

- Every path starts with `/` followed by the document element's name.
- No predicate (`[@attr='val']`) support — use the `xml-xpath` adapter for that.
- Element names are case-sensitive.
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

How a JSON object, array, scalar and null look as XML — and the one case XML cannot
represent — is in [document-shape.md](document-shape.md). Two points matter most here:

- An **array** is an element whose children are the items: `<items><item>1</item><item>2</item></items>`.
  `$.items[0]` is `/order/items/item[1]` (XPath positions are 1-based).
- An **empty element** `<k/>` is null, `""`, `{}` and `[]` all at once. It reads as null, and it
  is still a container a property can be written into.

Attributes are outside the JSON data model and are ignored by every command.

## See Also

[document-shape.md](document-shape.md) — the JSON ↔ XML mapping the commands rely on.
[script-notation.md](script-notation.md) — the XML script notation.
[xml-xpath.md](xml-xpath.md) — predicates, axes, and indexed access.
[../commands/Rename.md](../commands/Rename.md) — renaming elements, including the root.
[overview.md](../overview.md) — adapter selection table.
