# TLio.FormatConverter.Core

The core of format conversion for [TLio](https://github.com/SQUORA-NL/TLio): the intermediate
model every conversion passes through, `ConversionSettings`, and the `IFormatAdapter` contract
the JSON, XML and YAML adapters implement.

```sh
dotnet add package TLio.FormatConverter.Core
```

Most consumers do not reference this directly — `TLio.FormatConverter` brings it in along with
the three adapters and the `convert` / `convertValue` commands.

## What is here

- `IntermediateNode` and its shapes — object, array, scalar, mixed content — with the metadata
  that carries XML attributes, namespaces and CDATA across a format that has no notion of them.
- `ConversionSettings` — the per-boundary options. Adapters ignore settings they do not use
  rather than throwing.
- `MetadataConvention` — the rules JSON and YAML must spell identically, so an attribute that
  survives `xml → json` also survives `xml → yaml`.
- `FormatConverter` — the registry: `Register(adapter)`, then `Convert(from, text, to, settings)`.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
