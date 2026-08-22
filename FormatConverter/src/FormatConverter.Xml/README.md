# TLio.FormatConverter.Xml

The **XML** adapter for [TLio](https://github.com/SQUORA-NL/TLio) format conversion, backed by
`System.Xml`.

```sh
dotnet add package TLio.FormatConverter.Xml
```

Most consumers do not reference this directly — `TLio.FormatConverter` brings it in along with
the JSON and YAML adapters and the `convert` / `convertValue` commands.

```csharp
var converter = new FormatConverter.Core.FormatConverter();
converter.Register(new XmlFormatAdapter());   // format id: "xml"
```

## What XML leaves open, and the setting that decides it

| Setting | Default | Decides |
|---|---|---|
| `attributePrefix` | `"@"` | how an attribute is spelled elsewhere |
| `textProperty` | `"#text"` | the key an element's own value takes when it also has attributes |
| `namespacePrefix` | `"xmlns:"` | the key a namespace declaration takes |
| `arrayItemName` | `"item"` | what an array item with no name of its own is called |
| `arrayHandling` | `"wrapped"` | one element with items inside, or repeated siblings |
| `nullRepresentation` | `"empty"` | `<k/>` or `<k xsi:nil="true"/>` |
| `nameSanitization` | `"sanitize"` | a name XML cannot spell: rewritten, refused, or escaped |
| `inferTypes` | `false` | whether `42` in text becomes a number |
| `cdataAsText` | `false` | treat CDATA as plain text and drop the marker |

Output is the canonical document shape TLio's own XML adapter addresses — a wrapping element
with `item` children for arrays. `arrayHandling: "repeated"` writes the legacy shape, where the
*parent* element is what reads as the array.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
