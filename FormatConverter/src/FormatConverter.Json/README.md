# TLio.FormatConverter.Json

The **JSON** adapter for [TLio](https://github.com/SQUORA-NL/TLio) format conversion, backed by
`System.Text.Json`. It reads JSON into the intermediate model and writes it back out.

```sh
dotnet add package TLio.FormatConverter.Json
```

Most consumers do not reference this directly — `TLio.FormatConverter` brings it in along with
the XML and YAML adapters and the `convert` / `convertValue` commands.

```csharp
var converter = new FormatConverter.Core.FormatConverter();
converter.Register(new JsonFormatAdapter());   // format id: "json"
```

XML attributes arrive as ordinary `@name` properties and an element's own text as `#text`, both
spelled by the boundary's settings.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
