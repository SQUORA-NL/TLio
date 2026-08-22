# TLio.FormatConverter.Yaml

The **YAML** adapter for [TLio](https://github.com/SQUORA-NL/TLio) format conversion, backed by
[YamlDotNet](https://github.com/aaubry/YamlDotNet).

```sh
dotnet add package TLio.FormatConverter.Yaml
```

Most consumers do not reference this directly — `TLio.FormatConverter` brings it in along with
the JSON and XML adapters and the `convert` / `convertValue` commands.

```csharp
var converter = new FormatConverter.Core.FormatConverter();
converter.Register(new YamlFormatAdapter());   // format id: "yaml"
```

`flattenAnchors` (default `true`) dereferences anchors and aliases inline and marks the nodes it
expanded. XML attributes and text carry across as the same `@name` / `#text` properties JSON
uses, so a value that survives `xml → json` survives `xml → yaml`.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
