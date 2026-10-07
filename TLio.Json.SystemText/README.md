# TLio.Json.SystemText

The **System.Text.Json** adapter for [TLio](https://github.com/SQUORA-NL/TLio) —
`INodeAdapter<JsonNode>` and `IItemsFetcher<JsonNode>` implementations backed by
`System.Text.Json.Nodes` and the [`TLio.JsonPath`](../TLio.JsonPath/README.md) engine, which evaluates
paths directly on the document's own nodes.

Newtonsoft.Json is **not** referenced. Transformation behaviour is identical to `TLio.Json`: the
default path dialect reproduces Newtonsoft's JSONPath exactly (the same nodes, in the same order, with
the same errors), and a differential test suite holds it to that against Newtonsoft.Json itself.

```sh
dotnet add package TLio.Json.SystemText
dotnet add package TLio.Client
```

## Quick start

```csharp
using System.Text.Json.Nodes;
using TLio.Client;
using TLio.Json.SystemText;

var data   = JsonNode.Parse("""{ "name": "Acme", "tempId": 7 }""");
var script = """
[
  { "command": "put",    "path": "$.status", "value": "active" },
  { "command": "remove", "path": "$.tempId" }
]
""";

var options = ParseOptions<JsonNode>.CreateDefault();
var engine  = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);

var result = engine.Execute(script, data, SystemTextJsonExecutionContext.CreateDefault());
```

## Path language

By default paths are read in the **Newtonsoft** dialect, so a script written for `TLio.Json` runs
unchanged:

```text
$.customer.name
$.orders[*].total
$.orders[?(@.total > 100)]
$.orders[?(@.name =~ /^A/i)]
```

Script expressions (`()` as an expression) are not supported, in either adapter.

Pick another dialect when you create the context:

```csharp
using TLio.JsonPath;

SystemTextJsonExecutionContext.CreateDefault();                              // Newtonsoft (default)
SystemTextJsonExecutionContext.CreateDefault(JsonPathDialect.Rfc9535);       // strict RFC 9535
SystemTextJsonExecutionContext.CreateDefault(JsonPathDialect.Extended);      // Newtonsoft + RFC 9535 where Newtonsoft has no answer

// …or hand over a configured engine: custom functions, regex timeout, limits.
SystemTextJsonExecutionContext.Create(new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 }));
```

| | Newtonsoft (default) | `Rfc9535` | `Extended` |
|---|---|---|---|
| `$[?(@.price < 10)]` | ✓ | ✓ | ✓ |
| `$[?@.price < 10]`, `$[-1]`, `$["a"]` | ✗ | ✓ | ✓ |
| `match()`, `search()`, `length()`, `count()`, `value()` | ✗ | ✓ | ✓ |
| `=~ /re/`, `===`, `$.a-b` | ✓ | ✗ | ✓ |
| Matches `TLio.Json` | exactly | where the standard and Newtonsoft agree | wherever Newtonsoft has an answer |

The full syntax tables, the divergence table and the configuration options are in the
[`TLio.JsonPath` README](../TLio.JsonPath/README.md).

### Behaviour worth knowing

* An invalid path **throws** (`JsonPathException`) — as Newtonsoft's `SelectTokens` does — instead of
  quietly selecting nothing.
* `SelectNode` on a path that selects several nodes throws, as `SelectToken` does; use `SelectNodes`.
* A member whose value is JSON `null` is selectable (it is a match with a null node; the adapter hands out a
  placeholder for the slot), and is told apart from a member that is absent.
* **Dates.** Newtonsoft parses date-looking strings into date values, which changes how filters compare
  them. The default dialect reproduces that for System.Text.Json documents; see *Dates* in the
  `TLio.JsonPath` README, and `JsonPathOptions.EmulateNewtonsoftDates` to turn it off.

### Coming from the JsonCons-based version

See the *Migrating* section of the `TLio.JsonPath` README: the default dialect is now Newtonsoft's, errors are
no longer swallowed, and selection runs on the live document instead of a serialized snapshot.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
