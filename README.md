# TLio

**TLio** is a type-agnostic, format-neutral scripting framework for structured data transformation.
It is the successor to [JLio](https://jlio.online/) and extends the same command/function scripting
model to JSON, XML, YAML, and any future structured data format.

## Design principles

- **Format neutrality** — commands and functions have zero dependency on any specific data library.
- **Swappable selection** — the path language (JsonPath, XPath, dot-notation) is an injected
  `IItemsFetcher<TNode>` implementation.
- **Swappable node operations** — all create/read/update/delete operations on nodes go through
  `INodeAdapter<TNode>`.
- **Generic-first** — `TNode` travels from `ICommand<TNode>` through to `TLioScript<TNode>`;
  no boxing, no casting.
- **Injection everywhere** — `IExecutionContext<TNode>` carries the fetcher, adapter, and logger;
  nothing is hard-coded.
- **Continue where you can** — a path that matches nothing, or a step that changes nothing, logs a
  warning and lets the rest of the script run. Only a validation error or an exception fails a step.

## Commands and functions

### Commands

| Command | Purpose |
|---|---|
| `add` | Create a property — no-op if it already exists |
| `set` | Update an existing property — no-op if absent; never creates the path |
| `put` | Upsert — creates the path if needed, always writes |
| `remove` | Delete the matched nodes |
| `rename` | Change a node's **name**, keeping value, children, position and XML attributes |
| `copy` / `move` | Relocate nodes; `move` deletes the source |
| `merge` | Deep-merge one sub-tree into another, with array key-matching strategies |
| `compare` | Classify two values, or produce a full structural diff |
| `ifElse` / `decisionTable` | Conditional execution — two branches, or a rule table |

`flatten`, `restore`, `resolve` and `tocsv` come from the ETL pack (`RegisterETL`).

Any command also takes an optional `title` and `description` — free text saying what the step is
for. Nothing reads them at run time; they are there so a script is readable months later.

### Functions in values and in paths

Both a command's **value** and its **path** can carry function expressions, and the two are
independent:

- **Value** — `add`, `set`, `put` take any `=function()` expression; `ifElse` takes one as its
  condition; `decisionTable` takes them as rule results. `remove`, `copy`, `move`, `merge` and
  `compare` have no value field, and `rename`'s `name` is a literal.
- **Path** — every core command resolves `=indirect(...)` in **every** one of its paths, so the
  path itself can be read out of the document: `{"command":"remove","path":"=indirect($.target).field"}`.
  An expression that cannot be resolved warns and no-ops rather than failing the script. The ETL
  pack commands do not resolve `=indirect()`; compute the path in a preceding step.

Per-command detail is in [`docs/ai-ref/commands/`](docs/ai-ref/commands/); notation rules are in
[`docs/ai-ref/notation-reference.md`](docs/ai-ref/notation-reference.md); throughput,
thread-safety and precompilation guidance is in
[`docs/ai-ref/performance.md`](docs/ai-ref/performance.md).

## Projects

### Core libraries

| Project | Purpose |
|---|---|
| `TLio.Core` | Contracts + models — zero external dependencies |
| `TLio.Commands` | add, set, put, remove, rename, copy, move, merge, compare, ifElse, decisionTable, setProperties |
| `TLio.Functions` | Built-in value-producing functions (fetch, path/scriptpath, indirect, promote, partial, toArray, datetime, newGuid), the collection functions (distinct, sort, sortBy, last), the value-choosing functions (if, coalesce, between) and the predicate set |
| `TLio.Client` | `ScriptEngine`, `CompiledScript`, command/function registries, fluent `TLioConvert` API |

### Format adapters

| Project | Purpose |
|---|---|
| `TLio.Json` | JSON adapter (Newtonsoft.Json + JsonPath) |
| `TLio.Json.SystemText` | JSON adapter (System.Text.Json + JsonPath.Net / RFC 9535) |
| `TLio.Xml` | XML adapter — slash-path (`SlashPathItemsFetcher`) and native XPath (`NativeXPathItemsFetcher`), both anchored on the document node |
| `TLio.Yaml` | YAML adapter (YamlDotNet, dot-notation) |

### Extension packs

| Project | Purpose |
|---|---|
| `TLio.Extensions.ETL` | ETL commands: flatten, restore, resolve, tocsv |
| `TLio.Extensions.Math` | 27 math functions: abs, avg, calculate, ceiling, clamp, count, divide, floor, max, median, min, modulo, multiply, pow, round, sign, sqrt, subtract, sum and the conditional aggregates (sumIf(s), countIf(s), averageIf(s), maxIfs, minIfs) |
| `TLio.Extensions.Text` | 26 text functions: concat, contains, endsWith, format, indexOf, isEmpty, join, length, newGuid, padLeft/Right, parse, regexExtract, regexReplace, replace, right, split, startsWith, substring, toFixed, toLower, toString, toUpper, trim, trimStart/End |
| `TLio.Extensions.TimeDate` | 12 date functions: avgDate, dateAdd, dateCompare, dateDiff, datePart, endOfMonth, formatDate, isDateBetween, maxDate, minDate, parseDate, startOfMonth (`datetime` is core, not in this pack) |

`JLio.Extensions.JSchema` has no TLio counterpart — deliberately deferred
(`specs/002-migration-from-jlio/spec.md`), since the JSON Schema library JLio uses is AGPL /
paid-licence and Newtonsoft-bound.

### Format conversion

| Project | Package | Purpose |
|---|---|---|
| `TLio.FormatConverter` | `TLio.FormatConverter` | The `convert` and `convertValue` commands, `MultiFormatScriptRunner`, and the JSON / XML / YAML converters they work through. One assembly: `Core/` holds the intermediate model and `ConversionSettings`, `Json/` `Xml/` `Yaml/` the adapters, `Commands/` the TLio side |

### Tooling

| Project | Purpose |
|---|---|
| `TLio.Mcp` | MCP stdio server: `tlio_execute`, `tlio_analyze`, `tlio_list_commands`, `tlio_list_functions`, `tlio_describe`, `tlio_guide` |

### Tests & samples

| Project | Purpose |
|---|---|
| `TLio.UnitTests` | Core / Commands / Engine / Client tests |
| `TLio.Json.Tests` | Newtonsoft JSON adapter tests |
| `TLio.Json.SystemText.Tests` | System.Text.Json adapter tests |
| `TLio.Functions.Tests` | Built-in + extension-pack function tests |
| `TLio.Xml.Tests` | XML adapter tests (slash-path + XPath fixtures) |
| `TLio.Yaml.Tests` | YAML adapter tests |
| `TLio.FormatConverter.Tests` | Format conversion tests — adapters, round trips, mid-script `convert` |
| `TLio.Mcp.Tests` | MCP server tests, including two-iteration agent workflows |
| `samples/TLio.Sample.Api` | Minimal API sample (JSON/XML/YAML endpoints, slug script cache) |
| `samples/TLio.Sample.Api.IntegrationTests` | End-to-end API tests |
| `samples/TLio.Sample.Cli` | CLI sample (file-in / transformed-out) |
| `samples/TLio.Sample.DockerPlugin` | Docker API with NuPlane hot-loading of `.nupkg` plugins |
| `samples/TLio.Sample.AfdApi` | SIVI AFD 1.0 / AFD Short / AFD 2.0 conversion demo API |

### Running the samples

```sh
cd samples/TLio.Sample.Api && dotnet run
cd samples/TLio.Sample.Cli && dotnet run -- --input <file> --script <file> --output <file>
cd samples/TLio.Sample.AfdApi && dotnet run
cd samples/TLio.Sample.DockerPlugin && docker compose up
```

## Commands

```sh
dotnet build
dotnet test
```

## Versioning and releases

The git tag is the version — no version number is written down in this repository. MinVer reads
the nearest `v*` tag at build time, so `dotnet pack` on a laptop produces exactly what the
pipeline publishes to NuGet.org.

| | |
|---|---|
| commit on `main` | next minor of the last tag, e.g. `0.9.0-preview.3` |
| tag `v0.9.0` | `0.9.0` |

Cut a release with the **Release** workflow (choose patch / minor / major), or by hand with
`git tag v0.9.0 && git push origin v0.9.0`. Steering, pre-releases and the rules the pipeline
enforces: [`docs/versioning.md`](docs/versioning.md).

## Development approach

TLio is built using **Spec-Driven Development** via [SpecKit](https://github.com/github/spec-kit).

See `.specify/README.md` for the workflow and template reference.
See `specs/constitution.md` for the immutable architectural principles.

## Status

The framework is fully implemented and in active development (specs 001–019).

- All adapters ship and are tested: Newtonsoft JSON, System.Text.Json, XML (slash-path + XPath), YAML.
- All commands and built-in functions are implemented and covered by fixture-based NUnit tests.
- Four extension packs are available: ETL, Math, Text, TimeDate.
- A fluent `TLioConvert` API, `ScriptEngine` and `CompiledScript` are provided in `TLio.Client`.
- An MCP server (`TLio.Mcp`) exposes execution, gap analysis and reference lookup to agents.
- Unified script notation is documented in `docs/ai-ref/notation-reference.md`.
- All library packages are NuGet-packable; CI/CD pipelines publish preview and release builds.
