# TLio.Extensions.ETL

ETL commands for [TLio](https://github.com/SQUORA-NL/TLio) — flatten nested structures, restore
them, resolve references, and emit CSV. Format-agnostic via `INodeAdapter<TNode>`.

```sh
dotnet add package TLio.Extensions.ETL
```

## Register the pack

```csharp
using TLio.Extensions.ETL;

var options = ParseOptions<JToken>.CreateDefault();
options.CommandsProvider.RegisterETL<JToken>();
```

## Commands

| Command | Purpose |
|---|---|
| `flatten` | Collapse a nested structure into a flat, path-keyed map |
| `restore` | Rebuild the nested structure from a flattened one |
| `resolve` | Resolve references within the document |
| `tocsv` | Render an array of records as CSV |

`flatten` and `restore` are a round-trip pair.

## Example

```json
[
  { "command": "flatten", "path": "$.customer" },
  { "command": "tocsv",   "path": "$.orders" }
]
```

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
