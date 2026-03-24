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

| Project | Purpose |
|---|---|
| `TLio.Core` | Contracts + models — zero external dependencies |
| `TLio.Commands` | Set, Add, Remove, Copy, Move, Put |
| `TLio.Functions` | Built-in value-producing functions |
| `TLio.Json` | JSON adapter (Newtonsoft.Json + JsonPath) |
| `TLio.Xml` | XML adapter (System.Xml.Linq + XPath) |
| `TLio.Yaml` | YAML adapter (YamlDotNet) |
| `TLio.Client` | ScriptEngine, command/function registries |
| `TLio.UnitTests` | NUnit 4 test suite |

## Development approach

TLio is built using **Spec-Driven Development** via [SpecKit](https://github.com/github/spec-kit).

See `.specify/README.md` for the workflow and template reference.
See `specs/constitution.md` for the immutable architectural principles.
See `specs/001-tlio-core-architecture/tasks.md` for the current implementation backlog.

## Status

The base-form scaffold is complete:
- All contracts and model stubs are in place and compile.
- The JSON adapter is partially implemented (`JsonNodeAdapter` is complete; `JsonPathItemsFetcher` needs parent navigation and relative paths).
- XML and YAML adapters have type-correct stubs.
- Phase 2–7 tasks are tracked in `specs/001-tlio-core-architecture/tasks.md`.
