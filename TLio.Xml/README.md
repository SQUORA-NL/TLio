# TLio.Xml

The **XML** adapter for [TLio](https://github.com/SQUORA-NL/TLio) — `INodeAdapter` and
`IItemsFetcher` implementations backed by `System.Xml.Linq`.

```sh
dotnet add package TLio.Xml
dotnet add package TLio.Client
```

## Paths anchor on the document node

Exactly as XPath defines it: `/` is the document node (not an element — it selects
nothing), `/order` is the document element, and `/order/customer` a child of it.
**The document element is always named in the path.**

A bare `customer` is `child::customer` of the document node and matches nothing —
relative steps never skip a level. Use `//customer` for "at any depth".

| Fetcher | Factory | Path style | Supports |
|---|---|---|---|
| Slash-path | `XmlExecutionContext.CreateWithSlashPaths()` | `/order/address/city` | simple hierarchies, `*`, `//` |
| Native XPath | `XmlExecutionContext.CreateWithNativeXPath()` | `/order/address/city`, `//name`, `/order/item[@id='1']` | full XPath 1.0 |

## Quick start

```csharp
using System.Xml.Linq;
using TLio.Client;
using TLio.Xml;

// Parse through the adapter: it returns an element still attached to its XDocument,
// and that document node is what makes absolute paths resolve. A hand-built,
// detached XElement will not.
var data = new XmlNodeAdapter().Parse(
    "<order><customer><name>Acme</name></customer><tempId>7</tempId></order>");

var script = """
[
  { "command": "put",    "path": "/order/status", "value": "active" },
  { "command": "remove", "path": "/order/tempId" }
]
""";

var options = ParseOptions<XElement>.CreateDefault();
var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);

var result = engine.Execute(script, data, XmlExecutionContext.CreateWithSlashPaths());
```

## XML specifics

- An **array** is an element whose children share one name, with either more than one
  child or the canonical item name `item`. `IsObject` and `IsArray` are mutually exclusive.
- An **empty element** is null, `""`, `{}` and `[]` at once. It reads as null *and* as a
  container a property can be written into — which is what makes deep-path `add` work.
- Renaming the document element (`<order>` → `<opdracht>`) is the `rename` command.
  `move` with `toPath: "/"` replaces the document body but keeps the element's name.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
