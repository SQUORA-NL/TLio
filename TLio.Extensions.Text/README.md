# TLio.Extensions.Text

Text and string functions for [TLio](https://github.com/SQUORA-NL/TLio). Format-agnostic — works
against JSON, XML, and YAML through `IExecutionContext<TNode>`.

```sh
dotnet add package TLio.Extensions.Text
```

## Quick start

```csharp
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Extensions.Text;
using TLio.Json;

var data   = JToken.Parse("""{ "first": "Ada", "last": "Lovelace" }""");
var script = """
[
  { "command": "add", "path": "$.fullName", "value": "=concat($.first, ' ', $.last)" }
]
""";

var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();

var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
// result.Data["fullName"] → "Ada Lovelace"
```

`RegisterTextPack<TNode>()` is a JLio-compatible alias for `RegisterText<TNode>()`.

## Functions

`concat` · `length` · `substring` · `toupper` · `tolower` · `trim` · `trimstart` · `trimend` ·
`startswith` · `endswith` · `contains` · `replace` · `split` · `join` · `indexof` · `format` ·
`parse` · `padleft` · `padright` · `newguid` · `isempty` · `toString`

camelCase aliases are also registered: `toLower`, `toUpper`, `trimStart`, `trimEnd`.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
