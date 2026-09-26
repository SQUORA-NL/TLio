# TLio.Extensions.Math

Math and aggregation functions for [TLio](https://github.com/SQUORA-NL/TLio). Format-agnostic —
works against JSON, XML, and YAML through `IExecutionContext<TNode>`.

```sh
dotnet add package TLio.Extensions.Math
```

## Quick start

```csharp
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Extensions.Math;
using TLio.Json;

var data   = JToken.Parse("""{ "amounts": [10, 25, 7] }""");
var script = """
[
  { "command": "add", "path": "$.total", "value": "=sum($.amounts)" }
]
""";

var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterMath<JToken>();

var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
// result.Data["total"] → 42
```

## Functions

**Aggregation** — `sum` · `avg` · `count` · `min` · `max` · `median`

**Arithmetic** — `abs` · `ceiling` · `floor` · `round` · `sqrt` · `pow` · `subtract` · `modulo` ·
`calculate`

**Conditional aggregation** — `sumif` · `sumifs` · `countif` · `countifs` · `averageif` ·
`averageifs` · `minifs` · `maxifs`

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
