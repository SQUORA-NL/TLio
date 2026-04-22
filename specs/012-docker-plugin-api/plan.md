# Implementation Plan: Docker-Based Plugin API with NuGet Hot-Loading

**Branch**: `012-docker-plugin-api` | **Date**: 2026-04-21 | **Spec**: [spec.md](./spec.md)

## Summary

Add a new Docker-based sample (`TLio.Sample.DockerPlugin`) that demonstrates runtime NuGet package hot-loading using **NuPlane** (v0.0.1, ValenceWorks) for folder watching and assembly loading, and **CShells** (v0.0.14, Sipke Schoorstra) for the modular shell/feature host. Dropping a TLio extension `.nupkg` into a volume-mounted folder causes the running API to gain new transformation functions within 10 seconds; removing the package withdraws them. Existing samples (`TLio.Sample.Api`, `TLio.Sample.Cli`) are already correctly located under `samples/` — no relocation needed.

## Technical Context

**Language/Version**: C# / .NET 10 (existing project standard)  
**Primary Dependencies**:
- `Nuplane` (v0.0.1) + `Nuplane.Abstractions` + `Nuplane.Sources.Directory` + `Nuplane.Loading` + `Nuplane.Loading.Abstractions` + `Nuplane.Loading.Api`
- `CShells` (v0.0.14) + `CShells.Abstractions` + `CShells.AspNetCore`
- `TLio.Core`, `TLio.Commands`, `TLio.Functions`, `TLio.Client`, `TLio.Json` (via ProjectReference — local)

**Storage**: Filesystem only (`/plugins` volume mount); no database  
**Testing**: NUnit (existing convention) — integration tests via Docker Compose  
**Target Platform**: Linux Docker container (amd64), .NET 10  
**Performance Goals**: Plugin detection ≤ 10 s; container startup ≤ 30 s  
**Constraints**: Collectible `AssemblyLoadContext` required for unload; no restart on plugin change  
**Scale/Scope**: Single-container sample; single shell (no multi-tenancy needed for sample)

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing a format-specific type? | No — all changes are in the sample project `TLio.Sample.DockerPlugin`, which is an adapter-level app. `TLio.Core`/`TLio.Commands`/`TLio.Functions` are unchanged. |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? | N/A — no new commands or functions. `MutableFunctionsProvider` lives in the sample project and is injected via DI. |
| Generic-First | III | Does every new public API carry `<TNode>`? | N/A — sample project; no new Core public types. |
| Process/Execution separation | IV | Do all node reads/mutations go through `context.NodeAdapter` or `context.ItemsFetcher`? | N/A — no new commands or functions. |
| Swappable Selection | V | Are all path expressions supplied by callers? | N/A — no new commands or functions. |
| Test-First + Fixture Triplets | VI | Will every full-script-execution test use file-based fixture triplets? | N/A — this is a sample/demo project, not a library. Integration tests use Docker Compose. |
| Simplicity Gate | VII | Could this be done with fewer projects/layers? | Yes — one new project (`TLio.Sample.DockerPlugin`) suffices. CShells is justified by the user requirement; NuPlane replaces ~300 LOC of custom plumbing. |
| Backward Migration Path | VIII | Any JLio behaviour changed or dropped? | No. |
| No Leaking Internals | IX | Do `TLio.Core` public APIs expose format types? | No — `TLio.Core` is unchanged. |
| Logging as Observability | X | Does every `Execute()` path call `LogInfo`/`LogWarning`? | Plugin load/unload events will log via `ILogger<PluginLoader>`; TLio engine already conforms. |
| AI Component Reference | XI | New command/function/adapter `ai-ref.md` needed? | No new commands or functions. No new ai-ref.md required. |

**Result: All gates pass. No constitution violations.**

## Project Structure

### Documentation (this feature)

```text
specs/012-docker-plugin-api/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── api.md           ← HTTP API contract
└── tasks.md             ← Phase 2 output (from /speckit.tasks)
```

### Source Code

```text
samples/
├── TLio.Sample.Api/          ← existing, unchanged
├── TLio.Sample.Cli/          ← existing, unchanged
└── TLio.Sample.DockerPlugin/ ← NEW
    ├── TLio.Sample.DockerPlugin.csproj
    ├── Program.cs                    ← ASP.NET Core host + NuPlane + CShells wiring
    ├── PluginLoader.cs               ← subscribes to NuPlane events; calls MutableFunctionsProvider
    ├── MutableFunctionsProvider.cs   ← thread-safe dynamic IFunctionProvider wrapper
    ├── PluginCatalogService.cs       ← maintains PluginCatalog (for GET /plugins)
    ├── appsettings.json              ← CShells shell config + NuPlane plugins path
    ├── Dockerfile                    ← multi-stage build → linux/amd64 runtime image
    ├── docker-compose.yml            ← volume mount: ./plugins:/plugins
    └── README.md                     ← quickstart instructions

NuGet restore source (nuget.config):
  - https://api.nuget.org/v3/index.json   (CShells, NuPlane — both public)
```

### Key Classes

```
MutableFunctionsProvider<TNode>
  - Wraps IEnumerable<IFunctionProvider<TNode>>
  - AddProvider(string packageId, IFunctionProvider<TNode>) — thread-safe
  - RemoveProvider(string packageId) — thread-safe
  - Implements IFunctionsProvider<TNode> (same interface as existing ParseOptions uses)

PluginLoader
  - IHostedService registered in DI
  - Subscribes to Nuplane IPackageLoadedEvent / IPackageUnloadedEvent
  - On load: reflects new AssemblyLoadContext for IFunctionProvider<JToken> types, registers them
  - On unload: deregisters providers from MutableFunctionsProvider

PluginCatalogService
  - Singleton; maintains PluginCatalog (list of LoadedExtension)
  - Provides data for GET /plugins endpoint
```

### nuget.config (new, at repo root or sample folder)

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

NuPlane and CShells are both on NuGet.org — no private feed configuration required for the sample.

## Complexity Tracking

No constitution violations requiring justification.
