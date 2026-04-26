# Implementation Plan: API Script Slug Cache

**Branch**: `018-api-script-slug-cache` | **Date**: 2026-04-26 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/018-api-script-slug-cache/spec.md`

## Summary

Add pre-compiled slug-based script execution to both `TLio.Sample.Api` and `TLio.Sample.DockerPlugin`. Scripts are registered (via HTTP or startup config) and immediately compiled into `CompiledScript<TNode>` for all three supported formats (JSON, XML, YAML). At execution time, the input `Content-Type` selects the pre-compiled form, parses the request body, runs the script, and returns the result with a matching response `Content-Type`. No script parsing occurs per request.

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: ASP.NET Core Minimal API; `TLio.Client` (`ScriptEngine<TNode>`, `CompiledScript<TNode>`); `TLio.Json` (`JsonExecutionContext`, `JsonNodeAdapter`); `TLio.Xml` (`XmlExecutionContext`); `TLio.Yaml` (`YamlExecutionContext`); NuPlane + CShells (DockerPlugin host only)  
**Storage**: In-memory `ConcurrentDictionary<string, ScriptRegistryEntry>` — ephemeral, process-scoped  
**Testing**: NUnit 4.x — integration tests using `WebApplicationFactory` for HTTP round-trip; fixture triplets for script execution correctness  
**Target Platform**: .NET 10 ASP.NET Core web service (Linux container / Windows dev)  
**Project Type**: Web service samples  
**Performance Goals**: < 5ms additional latency per slug execution vs. direct in-process execution (SC-001); registration < 500ms (SC-002)  
**Constraints**: Thread-safe registry; zero script-text parsing per execution request; request body max 10 MB (configurable)  
**Scale/Scope**: No slug count limit; ≥ 200 concurrent execution requests without data corruption (SC-003)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing a format-specific type? | **No** — all new code lives in `TLio.Sample.Api` and `TLio.Sample.DockerPlugin`. Sample projects are permitted adapter consumers; no changes to Core/Commands/Functions. |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? | **Yes** — contexts are created via static factories (`JsonExecutionContext.CreateDefault()` etc.) and passed to `CompiledScript.Execute()`; no `new ConcreteAdapter()` in any non-sample code. |
| Generic-First | III | Does every new public API carry `<TNode>`? | **N/A** — new public surface is HTTP endpoints (not generic). Internal `ScriptRegistryEntry` stores three typed compiled scripts. No changes to Core/Commands/Functions APIs. |
| Process/Execution separation | IV | Do all node reads go through `context.NodeAdapter`? | **Yes** — execution delegates entirely to the existing `CompiledScript<TNode>.Execute(data, context)` chain; no direct `TNode` method calls in new code. |
| Swappable Selection | V | Are path expressions supplied by callers? | **Yes** — paths live inside the registered script source; none are hard-coded in registry or endpoint code. |
| Test-First + Fixture Triplets | VI | Will full-script-execution tests use fixture triplets? | **Yes** — slug execution correctness tests will use existing fixture triplets through the `ScriptEngine`/`CompiledScript` path. HTTP integration tests use `WebApplicationFactory` with real (not mocked) engine. |
| Simplicity Gate | VII | Could this be done with fewer projects/layers? | **Yes, kept simple** — no new projects. `ScriptRegistry` is a single service class added within each sample. Both samples share the same design pattern but are kept independent (no cross-sample shared project). |
| Backward Migration Path | VIII | Any JLio-equivalent behaviour changed or dropped? | **No** — new endpoints only; no changes to existing commands, functions, or adapters. |
| No Leaking Internals | IX | Do TLio.Core public APIs expose only `TNode`-parameterised types? | **No change** — feature is sample-layer only; Core API surface unchanged. |
| Logging as Observability | X | Does every `Execute()` path call `LogInfo` on success? | **Partial** — Article X applies to Core/Commands/Functions. At the HTTP layer, per spec clarification, successful executions are silent. The internal `IExecutionLogger` inside `CompiledScript.Execute()` still traces normally. No violation. |
| AI Component Reference | XI | Does every new command/function/adapter have an `ai-ref.md`? | **N/A** — no new commands, functions, or adapters introduced. |

## Project Structure

### Documentation (this feature)

```text
specs/018-api-script-slug-cache/
├── plan.md          ← this file
├── research.md      ✅ complete
├── data-model.md    ✅ complete
├── quickstart.md    ✅ complete
├── contracts/
│   └── http-api.md  ✅ complete
└── tasks.md         ← Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
samples/TLio.Sample.Api/
├── Registry/
│   ├── ScriptRegistryEntry.cs     ← slug, source, CompiledScript×3, RegisteredAt
│   ├── IScriptRegistry.cs         ← Add/Get/List/Delete interface
│   └── ScriptRegistry.cs          ← ConcurrentDictionary-backed implementation
├── Endpoints/
│   ├── SlugExecutionEndpoints.cs  ← POST /run/{slug}
│   └── ScriptManagementEndpoints.cs ← POST/GET/DELETE /scripts
├── Services/
│   ├── StartupScriptLoader.cs     ← reads scripts-config.json, seeds registry
│   └── FormatDetector.cs          ← Content-Type → adapter selection
├── scripts-config.json            ← sample startup seed (committed as example)
└── Program.cs                     ← register services + map endpoints

samples/TLio.Sample.DockerPlugin/
├── Registry/
│   ├── ScriptRegistryEntry.cs     ← same shape as Api sample
│   ├── IScriptRegistry.cs
│   └── ScriptRegistry.cs
├── Endpoints/
│   ├── SlugExecutionEndpoints.cs
│   └── ScriptManagementEndpoints.cs
├── Services/
│   ├── StartupScriptLoader.cs
│   └── FormatDetector.cs
├── scripts-config.json
└── Program.cs
```

**Structure Decision**: No new projects. Each sample is extended independently with the same set of classes to avoid coupling between sample projects. Shared logic (FormatDetector, registry shape) is duplicated by design — samples are demonstrations, not a shared library.

## Complexity Tracking

No Constitution Check violations requiring justification.

## Phase 0: Research

**Status**: Complete — see [research.md](research.md)

Key resolved decisions:
- Pre-compile for all three formats at registration using `ScriptEngine<TNode>.Compile()`
- `ConcurrentDictionary<string, ScriptRegistryEntry>` for thread-safe storage
- Format detection via `Content-Type` header with JSON-first fallback
- Response `Content-Type` set from the adapter used at execution
- Startup config: JSON array with inline script source text
- Article X logging applies at Core layer only; HTTP layer follows spec's error-only rule

## Phase 1: Design

**Status**: Complete

### Data Model

See [data-model.md](data-model.md).

Core entities:
- `ScriptRegistryEntry` — slug + source + 3 typed `CompiledScript<TNode>` fields
- `IScriptRegistry` / `ScriptRegistry` — `ConcurrentDictionary`-backed, thread-safe
- `SlugRegistrationPayload` — parsed from any content type
- `StartupScriptConfig` — JSON array read at startup

### Contracts

See [contracts/http-api.md](contracts/http-api.md).

Endpoints:
- `POST /run/{slug}` — execute registered script with request body as input
- `POST /scripts` — register/replace script (compile-on-register, any content type)
- `GET /scripts` — list all slugs with source and timestamp
- `DELETE /scripts/{slug}` — remove from registry

### Quickstart

See [quickstart.md](quickstart.md).

### Constitution Check (post-design)

All gates remain green. No new violations introduced during design.
