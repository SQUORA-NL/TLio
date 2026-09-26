# TLio.Functions

The built-in, value-producing functions for [TLio](https://github.com/SQUORA-NL/TLio).
Format-agnostic — all format-specific work is delegated through `IExecutionContext<TNode>`.

```sh
dotnet add package TLio.Functions
```

## Quick start

Any command value that starts with `=` is evaluated as a function expression. Functions are
registered for you by `ParseOptions<TNode>.CreateDefault()` in `TLio.Client`:

```csharp
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Json;

var data   = JToken.Parse("""{ "source": { "name": "Acme" } }""");
var script = """
[
  { "command": "add", "path": "$.id",     "value": "=newGuid()" },
  { "command": "add", "path": "$.copyOf", "value": "=fetch($.source.name)" }
]
""";

var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(script, data, JsonExecutionContext.CreateDefault());
// result.Data → { "source": { "name": "Acme" }, "id": "<guid>", "copyOf": "Acme" }
```

## Built-in functions

**Values** — `fetch` · `path` / `scriptpath` · `indirect` · `newGuid` · `datetime` ·
`partial` · `promote`

**Predicates** — `equals` · `notEquals` · `greaterThan` · `greaterOrEqual` · `lessThan` ·
`lessOrEqual` · `and` · `or` · `not`

Plus the
argument-passing and nesting rules described in the
[notation reference](https://github.com/SQUORA-NL/TLio/blob/main/docs/ai-ref/notation-reference.md).

## More functions

| Pack | Package |
|---|---|
| Text / string | `TLio.Extensions.Text` |
| Math and aggregation | `TLio.Extensions.Math` |
| Date and time | `TLio.Extensions.TimeDate` |

They register into the same provider:

```csharp
options.FunctionsProvider.RegisterText<JToken>();
```

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
