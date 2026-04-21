# Research: Docker-Based Plugin API with NuGet Hot-Loading

## Decision 1: NuPlane package selection

**Decision**: Use the full NuPlane ecosystem from NuGet.org (v0.0.1, by ValenceWorks, released 2026-04-17).

**Packages required**:
| Package | Role |
|---------|------|
| `Nuplane` | Primary DI/config entrypoint for runtime services |
| `Nuplane.Abstractions` | Contracts for package reconciliation and observer callbacks |
| `Nuplane.Sources.Directory` | Watches a local folder for `.nupkg` additions/removals |
| `Nuplane.Loading` | Collectible `AssemblyLoadContext` + package loading coordination |
| `Nuplane.Loading.Abstractions` | Contracts for load events and shared assembly policies |
| `Nuplane.Loading.Api` | ASP.NET Core endpoints that expose the loading catalog (`GET /plugins`) |

**Rationale**: NuPlane covers the full lifecycle: folder watching (`Sources.Directory`), package reconciliation (`Abstractions`), assembly loading (`Loading`), and HTTP observability (`Loading.Api`). This eliminates the need for a custom `FileSystemWatcher` + `NuGet.Packaging` + `AssemblyLoadContext` implementation.

**Alternatives considered**:
- Custom `FileSystemWatcher` + `NuGet.Packaging` + `AssemblyLoadContext`: would require ~300 LOC of plumbing that NuPlane provides out of the box.
- McMaster.NETCore.Plugins: mature, but does not handle `.nupkg` watching or reconciliation — only raw DLL loading.

---

## Decision 2: CShells package selection

**Decision**: Use CShells v0.0.14 with `CShells.AspNetCore` for the ASP.NET Core host.

**Packages required**:
| Package | Role |
|---------|------|
| `CShells` | Core shell/feature runtime with isolated DI containers |
| `CShells.Abstractions` | `IShellFeature` / `IWebShellFeature` interfaces (referenced by plugin assemblies) |
| `CShells.AspNetCore` | ASP.NET Core middleware, endpoint routing |

**Rationale**: CShells provides the modular feature model that maps cleanly onto TLio extension packages. Each loaded plugin package becomes a `IWebShellFeature` that registers the extension's `IFunctionProvider` with TLio and optionally maps diagnostic HTTP endpoints.

**Key architectural note**: CShells does not natively hot-load assemblies post-startup. The integration pattern is: NuPlane fires `IPackageLoadedEvent` → app registers the new feature's services into the shell's DI container → shell context is re-initialized. This pattern requires calling `IShellHost.UpdateShellSettingsAsync()` after assembly load, which CShells v0.0.14 supports.

**Alternatives considered**: Plain ASP.NET Core DI without CShells — simpler, but loses the per-shell isolation that makes the plugin model clean.

---

## Decision 3: Samples folder

**Decision**: The `samples/` directory already exists at the repo root with `TLio.Sample.Api` and `TLio.Sample.Cli`. US1 (consolidation) is already complete; US2 adds `TLio.Sample.DockerPlugin` as the third sample.

**Rationale**: No relocation work needed. Solution file already references projects under `samples\`.

---

## Decision 4: TLio function provider dynamic registration

**Decision**: Introduce a `MutableFunctionsProvider<TNode>` wrapper in `TLio.Client` that allows adding/removing `IFunctionProvider<TNode>` instances at runtime. The `DockerPlugin` sample uses this to register extension functions after NuPlane loads a package.

**Rationale**: `ParseOptions<TNode>.CreateDefault()` builds an immutable `FunctionsProvider`. To support dynamic add/remove without restarting the host, the sample wraps it with a `MutableFunctionsProvider` registered as a singleton in DI. The `ScriptEngine` resolves it from DI, so all subsequent script executions see the updated function set.

**Alternatives considered**:
- Rebuild `ScriptEngine` on each plugin change: correct but expensive; causes a brief unavailability window.
- Expose `ParseOptions` mutation: would require changing TLio.Core's public API surface (undesirable for this sample feature).

**Constitution check**: `MutableFunctionsProvider` lives in the sample project (`TLio.Sample.DockerPlugin`), not in `TLio.Core` — no constitution violations.

---

## Decision 5: Docker base image and volume mount

**Decision**: Use `mcr.microsoft.com/dotnet/aspnet:10.0` as the runtime image. The plugins folder is mounted at `/plugins` via a Docker volume (`-v ./plugins:/plugins`). The container reads `NUPLANE_PLUGINS_PATH=/plugins` environment variable (or `appsettings.json` default).

**Rationale**: Standard Microsoft .NET runtime image; minimal size for a sample. The folder path is configurable so users can adapt it.

**Alternatives considered**: `mcr.microsoft.com/dotnet/sdk:10.0` — too large for a runtime-only container.

---

## Decision 6: Plugin unload strategy

**Decision**: Use collectible `AssemblyLoadContext` (provided by `Nuplane.Loading`) so assemblies can be garbage-collected after unload. Plugin functions are deregistered from `MutableFunctionsProvider` on the `IPackageUnloadedEvent` callback.

**Rationale**: Collectible contexts are the only .NET mechanism for unloading assemblies from a running process. NuPlane.Loading already uses them internally.

**Alternatives considered**: Non-collectible contexts — assemblies stay loaded in memory permanently, violating the "remove package → functionality stops working" requirement.

---

## Decision 7: Conflict resolution for duplicate function names

**Decision**: Last-registered wins. If two plugins register a function with the same name, the most recently loaded package's implementation is used. A warning is logged via `ILogger`.

**Rationale**: Simple deterministic rule; easy to document. First-loaded-wins would require unloading all plugins when a new one arrives.

**Alternatives considered**: Namespace-qualified function names (e.g., `math:round`) — more correct but changes TLio script syntax, which is outside this feature's scope.
