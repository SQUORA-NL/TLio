# TLio.Core

Type-agnostic contracts and models for [TLio](https://github.com/SQUORA-NL/TLio), a format-neutral
scripting framework for structured data transformation. Successor to
[JLio](https://jlio.online/).

`TLio.Core` has **no dependency on any data format library** — it defines the abstractions every
other TLio package builds on.

```sh
dotnet add package TLio.Core
```

## What's in the box

| Contract | Purpose |
|---|---|
| `ICommand<TNode>` | A script step (set, add, remove, …) |
| `IFunction<TNode>` | A value-producing expression (`=concat(…)`) |
| `INodeAdapter<TNode>` | All create/read/update/delete operations on nodes |
| `IItemsFetcher<TNode>` | The path language (JSONPath, XPath, dot-notation) |
| `IExecutionContext<TNode>` | Carries the fetcher, adapter, and logger |
| `TLioScript<TNode>` | An ordered, executable list of commands |

`TNode` travels from `ICommand<TNode>` through to `TLioScript<TNode>` — no boxing, no casting.

## You probably want more than this

`TLio.Core` on its own does not execute anything. Add an engine and a format adapter:

```sh
dotnet add package TLio.Client   # ScriptEngine + registries
dotnet add package TLio.Json     # JSON adapter (Newtonsoft)
```

## Related packages

`TLio.Client` · `TLio.Commands` · `TLio.Functions` ·
`TLio.Json` · `TLio.Json.SystemText` · `TLio.Xml` · `TLio.Yaml` ·
`TLio.Extensions.ETL` · `TLio.Extensions.Math` · `TLio.Extensions.Text` · `TLio.Extensions.TimeDate`

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
