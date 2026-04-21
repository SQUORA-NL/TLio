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

## Projects

### Core libraries

| Project | Purpose |
|---|---|
| `TLio.Core` | Contracts + models — zero external dependencies |
| `TLio.Commands` | Set, Add, Remove, Copy, Move, Put, Compare, Merge, IfElse, DecisionTable, Flatten, Restore, ToCsv |
| `TLio.Functions` | Built-in value-producing functions (fetch, path, indirect, newGuid, …) |
| `TLio.Client` | ScriptEngine, command/function registries, fluent `TLioConvert` API |

### Format adapters

| Project | Purpose |
|---|---|
| `TLio.Json` | JSON adapter (Newtonsoft.Json + JsonPath) |
| `TLio.Json.SystemText` | JSON adapter (System.Text.Json + JsonPath.Net / RFC 9535) |
| `TLio.Xml` | XML adapter — slash-path (`SlashPathItemsFetcher`) and native XPath (`NativeXPathItemsFetcher`) |
| `TLio.Yaml` | YAML adapter (YamlDotNet, dot-notation) |

### Extension packs

| Project | Purpose |
|---|---|
| `TLio.Extensions.ETL` | ETL functions: flatten, restore, CSV round-trip |
| `TLio.Extensions.Math` | Math functions |
| `TLio.Extensions.Text` | Text functions: concat, toString, parse, format, length, substring, replace, toLower, toUpper, trim |
| `TLio.Extensions.TimeDate` | Date/time functions |

### Tests & samples

| Project | Purpose |
|---|---|
| `TLio.UnitTests` | Core / Commands / Engine tests |
| `TLio.Json.Tests` | Newtonsoft JSON adapter tests |
| `TLio.Json.SystemText.Tests` | System.Text.Json adapter tests |
| `TLio.Functions.Tests` | Built-in + extension-pack function tests |
| `TLio.Xml.Tests` | XML adapter tests (slash-path + XPath fixtures) |
| `TLio.Yaml.Tests` | YAML adapter tests |
| `samples/TLio.Sample.Api` | Minimal API sample (JSON/XML/YAML endpoints) |
| `samples/TLio.Sample.Cli` | CLI sample (file-in / transformed-out) |

## Development approach

TLio is built using **Spec-Driven Development** via [SpecKit](https://github.com/github/spec-kit).

See `.specify/README.md` for the workflow and template reference.
See `specs/constitution.md` for the immutable architectural principles.

## Status

The framework is fully implemented and in active development (features 001–011 complete or in progress):

- All adapters ship and are tested: Newtonsoft JSON, System.Text.Json, XML (slash-path + XPath), YAML.
- All core commands and built-in functions are implemented and covered by fixture-based NUnit tests.
- Four extension packs are available: ETL, Math, Text, TimeDate.
- A fluent `TLioConvert` API and `ScriptEngine` are provided in `TLio.Client`.
- Unified script notation is documented in `docs/ai-ref/notation-reference.md`.
- All 12 library packages are NuGet-packable; CI/CD pipelines publish preview and release builds.
