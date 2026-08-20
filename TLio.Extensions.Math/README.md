# TLio.Extensions.Math

Math and aggregation functions for [TLio](https://github.com/SQUORA-NL/TLio). Format-agnostic —
works against JSON, XML, and YAML through `IExecutionContext<TNode>`.

```sh
dotnet add package TLio.Extensions.Math
```

## Register the pack

```csharp
using TLio.Extensions.Math;

var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterMath<JToken>();
```

## Functions

**Aggregation** — `sum` · `avg` · `count` · `min` · `max` · `median`

**Arithmetic** — `abs` · `ceiling` · `floor` · `round` · `sqrt` · `pow` · `subtract` · `modulo` ·
`calculate`

**Conditional aggregation** — `sumif` · `sumifs` · `countif` · `countifs` · `averageif` ·
`averageifs` · `minifs` · `maxifs`

## Example

```json
[
  { "command": "add", "path": "$.orderTotal",
    "value": "=sum(=fetch($.lines[*].amount))" },
  { "command": "add", "path": "$.largeOrders",
    "value": "=countif($.orders[*].total, '>100')" }
]
```

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
