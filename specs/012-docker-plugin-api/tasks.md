# Tasks: Docker-Based Plugin API with NuGet Hot-Loading

**Input**: Design documents from `/specs/012-docker-plugin-api/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/api.md ✓, quickstart.md ✓

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1/US2/US3 as defined in spec.md
- All paths relative to repo root unless noted

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new project and wire it into the solution

- [x] T001 Create `samples/TLio.Sample.DockerPlugin/TLio.Sample.DockerPlugin.csproj` referencing Nuplane, Nuplane.Abstractions, Nuplane.Sources.Directory, Nuplane.Loading, Nuplane.Loading.Abstractions, Nuplane.Loading.Api (all v0.0.1), CShells, CShells.Abstractions, CShells.AspNetCore (all v0.0.14), and local ProjectReferences to TLio.Core, TLio.Commands, TLio.Functions, TLio.Client, TLio.Json
- [x] T002 Create `nuget.config` at repo root (or inside the sample folder) with a single `nuget.org` package source (`https://api.nuget.org/v3/index.json`)
- [x] T003 [P] Add `TLio.Sample.DockerPlugin` project reference to `TLio.sln` so it is included in the full solution build

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model types and the MutableFunctionsProvider that all user story phases depend on

**⚠️ CRITICAL**: Phases 3–5 all depend on this phase completing first

- [x] T004 [P] Create `samples/TLio.Sample.DockerPlugin/PluginStatus.cs` — enum with values Detected, Loading, Loaded, Failed, Unloading, Unloaded (as per data-model.md)
- [x] T005 [P] Create `samples/TLio.Sample.DockerPlugin/PluginPackage.cs` — record with fields PackageId (string), Version (string), FilePath (string), Status (PluginStatus), LoadedAt (DateTimeOffset?), UnloadedAt (DateTimeOffset?), ErrorMessage (string?) (as per data-model.md)
- [x] T006 [P] Create `samples/TLio.Sample.DockerPlugin/LoadedExtension.cs` — record with fields PackageId (string), AssemblyName (string), Functions (string[]), LoadContextId (Guid) (as per data-model.md)
- [x] T007 [P] Create `samples/TLio.Sample.DockerPlugin/PluginCatalog.cs` — record with Entries (IReadOnlyList\<LoadedExtension\>) and LastUpdated (DateTimeOffset) (as per data-model.md)
- [x] T008 Create `samples/TLio.Sample.DockerPlugin/MutableFunctionsProvider.cs` — thread-safe `MutableFunctionsProvider<TNode>` class implementing `IFunctionsProvider<TNode>`; wraps a list of `IFunctionProvider<TNode>` with `AddProvider(string packageId, IFunctionProvider<TNode>)` and `RemoveProvider(string packageId)` using a `ReaderWriterLockSlim`; delegates `GetFunction(string name)` across registered providers (last-registered wins); logs duplicate-name conflicts via injected `ILogger`
- [x] T009 [P] Create `samples/TLio.Sample.DockerPlugin/appsettings.json` with NuPlane plugins path defaulting to `/plugins` and any CShells shell configuration required for `IShellHost` registration

**Checkpoint**: Data model and MutableFunctionsProvider complete — user story implementation can begin

---

## Phase 3: User Story 1 — Consolidate Samples (Priority: P1) 🎯 MVP

**Goal**: Verify existing samples are already in `samples/` and the solution builds cleanly from the repo root, confirming the new project fits alongside them

**Independent Test**: Run `dotnet build` from repo root — `TLio.Sample.Api`, `TLio.Sample.Cli`, and `TLio.Sample.DockerPlugin` all compile without errors

**Note**: Research confirmed `samples/TLio.Sample.Api` and `samples/TLio.Sample.Cli` are already in the correct location — no file moves required. This phase verifies correctness and integrates the new project.

- [x] T010 [US1] Verify `dotnet build samples/TLio.Sample.Api` and `dotnet build samples/TLio.Sample.Cli` succeed from repo root; confirm solution file `TLio.sln` already references both projects under `samples\` and update any stale paths if found
- [x] T011 [US1] Run `dotnet build TLio.sln` (or `dotnet build` at root) and confirm all projects including `TLio.Sample.DockerPlugin` compile without errors after T001–T003 are complete

**Checkpoint**: All three sample projects build — US1 complete

---

## Phase 4: User Story 2 — Drop NuGet Package → API Gains Functionality (Priority: P2)

**Goal**: A running Docker container detects `.nupkg` files added to/removed from the mounted `/plugins` folder and makes the extension's functions available (or unavailable) within 10 seconds — without restarting

**Independent Test**: Start container, call `=round($.value)` → expect "Unknown function: round". Drop `TLio.Extensions.Math.nupkg` → wait ≤10 s → call again → expect correct result. Remove package → call again → expect "Unknown function: round"

- [x] T012 [US2] Create `samples/TLio.Sample.DockerPlugin/PluginCatalogService.cs` — singleton service that maintains the `PluginCatalog` (thread-safe); exposes `AddExtension(LoadedExtension)` and `RemoveExtension(string packageId)` methods; tracks PluginPackage status transitions; provides `GetCatalog()` returning the current snapshot
- [x] T013 [US2] Create `samples/TLio.Sample.DockerPlugin/PluginLoader.cs` — `IHostedService` that subscribes to NuPlane `IPackageLoadedEvent` and `IPackageUnloadedEvent`; on load: discovers all `IFunctionProvider<JToken>` implementations in the new `AssemblyLoadContext` via reflection, registers each with `MutableFunctionsProvider.AddProvider`, updates `PluginCatalogService`, logs success; on load failure (malformed/non-TLio package): logs warning, no crash; on unload: calls `MutableFunctionsProvider.RemoveProvider`, updates catalog, marks context for GC
- [x] T014 [US2] Create `samples/TLio.Sample.DockerPlugin/Program.cs` — ASP.NET Core minimal API host; registers `MutableFunctionsProvider<JToken>` as singleton `IFunctionsProvider<JToken>`; registers `PluginCatalogService` as singleton; registers `PluginLoader` as `IHostedService`; wires NuPlane with `Sources.Directory` watching `appsettings.json` plugins path (overridable via env var `NUPLANE_PLUGINS_PATH`); wires CShells `IShellHost`; implements `POST /transform/{format}` endpoint that builds `ScriptEngine<JToken>` from the MutableFunctionsProvider singleton and executes the TLio script against the input document, returning `{ "success": true/false, "data": ... / "error": ... }`
- [x] T015 [P] [US2] Create `samples/TLio.Sample.DockerPlugin/Dockerfile` — multi-stage: `sdk:10.0` build stage (restore + publish), `aspnet:10.0` runtime stage (linux/amd64); copies published output; sets `ASPNETCORE_URLS=http://+:8080`; exposes port 8080; sets default `NUPLANE_PLUGINS_PATH=/plugins`; includes Docker HEALTHCHECK calling `GET /health`
- [x] T016 [P] [US2] Create `samples/TLio.Sample.DockerPlugin/docker-compose.yml` — defines service `tlio-plugin-api` using `image: tlio-sample-docker-plugin:latest`; maps port `5000:8080`; mounts `./plugins:/plugins` volume; sets `ASPNETCORE_ENVIRONMENT=Development`; includes health check and container name matching quickstart.md examples

**Checkpoint**: Container starts, hot-load add/remove cycle works end-to-end — US2 complete

---

## Phase 5: User Story 3 — Inspect Loaded Plugins (Priority: P3)

**Goal**: `GET /plugins` returns the live list of loaded packages and their functions; `GET /plugins/status` returns full reconciliation state; `GET /health` provides liveness

**Independent Test**: Load a plugin, call `GET /plugins` — response includes the package name, version, loadedAt, and functions array alongside builtinFunctions list

- [x] T017 [US3] Add `GET /plugins` custom endpoint in `Program.cs` — queries `PluginCatalogService.GetCatalog()` and the `MutableFunctionsProvider` for builtin function names; returns JSON matching the contract in `contracts/api.md` (lastUpdated, plugins array, builtinFunctions array); response updates within 1 second of any plugin change
- [x] T018 [P] [US3] Wire `Nuplane.Loading.Api` endpoint extensions in `Program.cs` to expose `GET /plugins/status` returning the full PluginPackage reconciliation state (packageId, version, status, loadedAt, errorMessage) as defined in `contracts/api.md`
- [x] T019 [P] [US3] Add `GET /health` liveness probe endpoint in `Program.cs` returning `{ "status": "healthy" }` with HTTP 200; used by Docker HEALTHCHECK in T015

**Checkpoint**: All three endpoints respond correctly — US3 complete

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and final validation

- [x] T020 [P] Create `samples/TLio.Sample.DockerPlugin/README.md` documenting prerequisites (Docker Desktop), build command (`docker build`), run command (`docker compose up`), and the full add-plugin → call endpoint → remove-plugin cycle matching `quickstart.md` scenarios 1–5; document the conflict resolution rule (last-registered wins)
- [x] T021 [P] Update `CLAUDE.md` at repo root to add `TLio.Sample.DockerPlugin` to the Project Structure section under `samples/`
- [x] T022 Run `dotnet build` from repo root and confirm zero errors across all projects including the new sample
- [x] T023 Validate `quickstart.md` scenarios against built artifacts: confirm docker-compose service name, port mapping, and `GET /plugins` response shape match the examples in `specs/012-docker-plugin-api/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on T001 (project file must exist before adding C# files) — **BLOCKS Phases 3–5**
- **US1 (Phase 3)**: Depends on Phase 2 completion; T010 can run independently of T008/T009
- **US2 (Phase 4)**: Depends on Phase 2 completion (needs MutableFunctionsProvider, data model types, appsettings.json)
- **US3 (Phase 5)**: Depends on T014 (Program.cs must exist to add endpoints) and T012 (PluginCatalogService)
- **Polish (Phase 6)**: Depends on all preceding phases

### User Story Dependencies

- **US1 (P1)**: Verifies pre-existing samples + new project builds — no runtime dependencies on US2/US3
- **US2 (P2)**: Depends on Phase 2 foundational types — independent of US3
- **US3 (P3)**: Depends on US2's Program.cs existing (T014) for endpoint registration; PluginCatalogService (T012) must be complete

### Within Each User Story

- Models/records before services
- MutableFunctionsProvider (T008) before PluginLoader (T013) before Program.cs (T014)
- Program.cs (T014) before endpoint additions (T017–T019)

### Parallel Opportunities

- T004–T007 (data model records) all touch different files — run in parallel
- T015 (Dockerfile) and T016 (docker-compose.yml) touch different files — parallel with each other and with T013 once T014's wiring plan is clear
- T018 (plugins/status) and T019 (health) are independent endpoint additions — parallel
- T020 (README) and T021 (CLAUDE.md) touch different files — parallel

---

## Parallel Example: User Story 2

```bash
# After Phase 2 is complete, launch in parallel:
Task T012: PluginCatalogService.cs
Task T013: PluginLoader.cs (depends on T012, T008)

# Then:
Task T014: Program.cs (depends on T012, T013)

# In parallel with T014:
Task T015: Dockerfile
Task T016: docker-compose.yml
```

---

## Implementation Strategy

### MVP First (User Story 2 — Core Hot-Loading)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational (T004–T009)
3. Complete Phase 3: US1 verification (T010–T011)
4. Complete Phase 4: US2 hot-loading (T012–T016)
5. **STOP and VALIDATE**: Build Docker image, run add-plugin cycle, verify via quickstart.md scenarios 1–3
6. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → project builds
2. US1 verification → existing samples confirmed
3. US2 → hot-loading works end-to-end (MVP!)
4. US3 → observability endpoints added
5. Polish → README + CLAUDE.md + final build verification

---

## Notes

- NuPlane and CShells are both on public NuGet.org — no private feedz.io feed configuration is required (confirmed in research.md Decision 1)
- `MutableFunctionsProvider` lives in the sample project only — no changes to `TLio.Core`, `TLio.Commands`, or `TLio.Functions` (Article I/IX compliant)
- No new TLio commands or functions are introduced — no `ai-ref.md` files needed (Article XI)
- Plugin unload uses collectible `AssemblyLoadContext` (provided by Nuplane.Loading) — plugins.cs must not hold static references to loaded types after unload
- Duplicate function names: last-registered wins; warn via `ILogger` (research.md Decision 7)
- Partial-copy / malformed nupkg: PluginLoader must catch exceptions and log a warning without crashing (spec edge case)
- The `POST /transform/{format}` endpoint should return HTTP 200 with `"success": false` for unknown functions — not HTTP 4xx — to match the contract in `contracts/api.md`
