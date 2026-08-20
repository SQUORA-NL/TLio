# TLio.Extensions.Text

Text and string functions for [TLio](https://github.com/SQUORA-NL/TLio). Format-agnostic — works
against JSON, XML, and YAML through `IExecutionContext<TNode>`.

```sh
dotnet add package TLio.Extensions.Text
```

## Register the pack

```csharp
using TLio.Extensions.Text;

var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
```

`RegisterTextPack<TNode>()` is a JLio-compatible alias for the same call.

## Functions

`concat` · `length` · `substring` · `toupper` · `tolower` · `trim` · `trimstart` · `trimend` ·
`startswith` · `endswith` · `contains` · `replace` · `split` · `join` · `indexof` · `format` ·
`parse` · `padleft` · `padright` · `newguid` · `isempty` · `toString`

camelCase aliases are also registered: `toLower`, `toUpper`, `trimStart`, `trimEnd`.

## Example

```json
[
  { "command": "add", "path": "$.fullName",
    "value": "=concat(=fetch($.first), ' ', =fetch($.last))" },
  { "command": "set", "path": "$.code", "value": "=toUpper(=fetch($.code))" }
]
```

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
