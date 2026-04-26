# Tasks: API Script Slug Cache

**Input**: Design documents from `/specs/018-api-script-slug-cache/`  
**Branch**: `018-api-script-slug-cache`  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no conflicting dependencies)
- **[Story]**: User story this task belongs to (US1–US4)
- Applies to both `TLio.Sample.Api` and `TLio.Sample.DockerPlugin` unless noted

---

## Phase 1: Setup

**Purpose**: Test projects, example config files, project references

- [X] T001 Create NUnit 4.x integration test project `samples/TLio.Sample.Api.IntegrationTests/TLio.Sample.Api.IntegrationTests.csproj` referencing `TLio.Sample.Api` and `Microsoft.AspNetCore.Mvc.Testing`
- [ ] T002 [P] Create NUnit 4.x integration test project `samples/TLio.Sample.DockerPlugin.IntegrationTests/TLio.Sample.DockerPlugin.IntegrationTests.csproj` referencing `TLio.Sample.DockerPlugin` and `Microsoft.AspNetCore.Mvc.Testing`
- [X] T003 [P] Create `samples/TLio.Sample.Api/scripts-config.json` with one example entry: `[{"slug":"echo","script":"[{\"command\":\"set\",\"path\":\"$.processed\",\"value\":true}]"}]`
- [X] T004 [P] Create `samples/TLio.Sample.DockerPlugin/scripts-config.json` with the same example entry as T003

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Registry model, interface, implementation, format detection, script compilation — required by every user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 Create `samples/TLio.Sample.Api/Registry/ScriptRegistryEntry.cs` — `record` with properties: `string Slug`, `string Source`, `CompiledScript<JToken> CompiledJson`, `CompiledScript<XElement> CompiledXml`, `CompiledScript<YamlNode> CompiledYaml`, `DateTimeOffset RegisteredAt`
- [X] T00X [P] Create `samples/TLio.Sample.DockerPlugin/Registry/ScriptRegistryEntry.cs` — identical shape to T005
- [X] T00X [P] Create `samples/TLio.Sample.Api/Registry/IScriptRegistry.cs` — interface with: `void Add(ScriptRegistryEntry entry)`, `bool TryGet(string slug, out ScriptRegistryEntry entry)`, `IReadOnlyList<ScriptRegistryEntry> List()`, `bool Delete(string slug)`
- [X] T00X [P] Create `samples/TLio.Sample.DockerPlugin/Registry/IScriptRegistry.cs` — identical interface to T007
- [X] T00X Create `samples/TLio.Sample.Api/Registry/ScriptRegistry.cs` — `IScriptRegistry` backed by `ConcurrentDictionary<string, ScriptRegistryEntry>`; `Add` uses `AddOrUpdate` (atomic replace); `Delete` returns `TryRemove` result; `List` returns `Values.ToList()` snapshot
- [X] T0XX [P] Create `samples/TLio.Sample.DockerPlugin/Registry/ScriptRegistry.cs` — identical implementation to T009
- [X] T0XX [P] Create `samples/TLio.Sample.Api/Services/FormatDetector.cs` — `DetectFormat(string? contentType, string body)` returns `"json"`, `"xml"`, or `"yaml"`; maps `application/json` → json, `application/xml`/`text/xml` → xml, `application/yaml`/`text/yaml` → yaml; when header absent or unknown, tries `JsonDocument.Parse`, then `XDocument.Parse`, then YAML parse; returns `null` if all fail
- [X] T0XX [P] Create `samples/TLio.Sample.DockerPlugin/Services/FormatDetector.cs` — identical to T011
- [X] T0XX [P] Create `samples/TLio.Sample.Api/Services/ScriptCompiler.cs` — `ScriptRegistryEntry Compile(string slug, string scriptSource)`: instantiates `ScriptEngine<JToken>`, `ScriptEngine<XElement>`, `ScriptEngine<YamlNode>` with their respective adapters; calls `.Compile(scriptSource, adapter)` for each; throws `ScriptCompilationException(string detail)` if any format fails; returns a fully populated `ScriptRegistryEntry` with `RegisteredAt = DateTimeOffset.UtcNow`
- [X] T0XX [P] Create `samples/TLio.Sample.DockerPlugin/Services/ScriptCompiler.cs` — identical to T013
- [X] T0XX Register `IScriptRegistry` → `ScriptRegistry` (singleton), `FormatDetector` (singleton), `ScriptCompiler` (singleton) in `samples/TLio.Sample.Api/Program.cs`
- [X] T0XX [P] Register same three singletons in `samples/TLio.Sample.DockerPlugin/Program.cs`

**Checkpoint**: Registry, compiler, and format detector ready — user story work can begin

---

## Phase 3: User Story 1 — Execute a Pre-Registered Script via Slug (Priority: P1) 🎯 MVP

**Goal**: `POST /run/{slug}` looks up the pre-compiled script, executes it with the request body as input, returns the result with the correct `Content-Type`

**Independent Test**: Seed one entry directly into `IScriptRegistry` via test setup; send `POST /run/{slug}` with a JSON body; assert 200 with transformed output

- [X] T0XX [US1] Create `samples/TLio.Sample.Api/Endpoints/SlugExecutionEndpoints.cs` — `POST /run/{slug}`: (1) validate slug regex `^[a-z0-9\-_]+$` → 400; (2) `registry.TryGet(slug)` → 404 + `ILogger.LogWarning` if absent; (3) read body as string, `formatDetector.DetectFormat(contentType, body)` → 400 + LogWarning if null; (4) select pre-compiled form (`entry.CompiledJson/Xml/Yaml`), parse body via adapter, call `compiled.Execute(parsedNode, context)`; (5) on `result.Success` → 200 + serialised output + matching `Content-Type`; (6) on failure → 422 + `{"error": result.ErrorMessage}` + `ILogger.LogError`
- [X] T0XX [P] [US1] Create `samples/TLio.Sample.DockerPlugin/Endpoints/SlugExecutionEndpoints.cs` — identical handler to T017
- [X] T0XX [US1] Map `app.MapPost("/run/{slug}", ...)` in `samples/TLio.Sample.Api/Program.cs`; add `builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = config.GetValue<long>("SlugCache:MaxBodySizeBytes", 10_485_760))` and add `SlugCache:MaxBodySizeBytes` key to `samples/TLio.Sample.Api/appsettings.json`
- [X] T0XX [P] [US1] Map `POST /run/{slug}` and configure body size in `samples/TLio.Sample.DockerPlugin/Program.cs` and `appsettings.json`
- [X] T0XX [P] [US1] Write integration tests in `samples/TLio.Sample.Api.IntegrationTests/SlugExecutionTests.cs` covering: (a) 200 + correct JSON output for pre-seeded slug with JSON input; (b) 200 + XML response for XML input; (c) 404 for unknown slug; (d) 400 for slug containing `%20`; (e) 422 when script fails at runtime (use a script that references a missing path)

**Checkpoint**: US1 fully functional — execute any pre-seeded slug with any supported format input

---

## Phase 4: User Story 2 — Register a Script at Runtime (Priority: P2)

**Goal**: `POST /scripts` accepts any content-type payload, compiles the script immediately, stores it; returns 201/200/400 appropriately

**Independent Test**: POST /scripts with a valid JSON payload → 201; immediately POST /run/{slug} → 200 with correct output

- [X] T0XX [US2] Create `samples/TLio.Sample.Api/Services/RegistrationPayloadParser.cs` — `(string slug, string script)? Parse(string body, string? contentType)`: tries JSON deserialization to `{slug, script}` first; if fails tries XML (`<registration><slug/><script/></registration>` shape); if fails tries YAML; returns null if all attempts fail or required fields missing
- [X] T0XX [P] [US2] Create `samples/TLio.Sample.DockerPlugin/Services/RegistrationPayloadParser.cs` — identical to T022
- [X] T0XX [US2] Create `samples/TLio.Sample.Api/Endpoints/ScriptManagementEndpoints.cs` — POST /scripts handler: (1) read body; (2) `parser.Parse(body, contentType)` → 400 + `{"error":"Cannot parse..."}` if null; (3) validate slug regex → 400; (4) `compiler.Compile(slug, script)` → 400 + `{"error":"Script compilation failed: ..."}` on `ScriptCompilationException`; (5) `bool replaced = registry.TryGet(slug, out _); registry.Add(entry)`; (6) log Info `"Slug '{slug}' {registered|replaced}"`; (7) return 201 (new) or 200 (replaced) with `{"slug":slug,"status":"registered"|"replaced"}`
- [X] T0XX [P] [US2] Create `samples/TLio.Sample.DockerPlugin/Endpoints/ScriptManagementEndpoints.cs` — identical handler to T024
- [X] T0XX [US2] Map `app.MapPost("/scripts", ...)` in `samples/TLio.Sample.Api/Program.cs`; register `RegistrationPayloadParser` as singleton
- [X] T0XX [P] [US2] Map `POST /scripts` and register `RegistrationPayloadParser` as singleton in `samples/TLio.Sample.DockerPlugin/Program.cs`
- [X] T0XX [P] [US2] Write integration tests in `samples/TLio.Sample.Api.IntegrationTests/ScriptRegistrationTests.cs`: (a) new slug → 201 body contains `"registered"`; (b) re-register same slug → 200 body contains `"replaced"`; (c) invalid script syntax → 400 body contains `"compilation failed"`; (d) unrecognisable payload (binary blob) → 400 body contains `"Cannot parse"`; (e) register then immediately execute → 200 with correct output

**Checkpoint**: Runtime registration fully functional — operators can register new slugs without restart

---

## Phase 5: User Story 4 — Seed Scripts at Startup via Configuration (Priority: P2)

**Goal**: `scripts-config.json` (or `TLIO_SCRIPTS_CONFIG` env var path) is read at startup; valid entries are compiled and registered; invalid entries are logged and skipped

**Independent Test**: Provide a `scripts-config.json` with one valid entry in `WebApplicationFactory`; assert slug is immediately executable after server start with no registration call

- [X] T0XX [US4] Create `samples/TLio.Sample.Api/Services/StartupScriptLoader.cs` — `Task LoadAsync(IScriptRegistry registry, ScriptCompiler compiler, ILogger logger, IConfiguration config)`: reads path from `config["TLIO_SCRIPTS_CONFIG"]` or defaults to `Path.Combine(AppContext.BaseDirectory, "scripts-config.json")`; if file absent, returns (no error); deserialises JSON array of `{slug, script}`; for each entry: calls `compiler.Compile(slug, script)`, calls `registry.Add(entry)`, logs Info; on `ScriptCompilationException`: logs Warning with slug and detail, continues
- [X] T0XX [P] [US4] Create `samples/TLio.Sample.DockerPlugin/Services/StartupScriptLoader.cs` — identical to T029
- [X] T0XX [US4] In `samples/TLio.Sample.Api/Program.cs`: resolve `StartupScriptLoader`, `IScriptRegistry`, `ScriptCompiler`, `ILogger<StartupScriptLoader>`, `IConfiguration` from the built app's service provider; call `loader.LoadAsync(...)` before `app.Run()`
- [X] T0XX [P] [US4] Wire `StartupScriptLoader.LoadAsync()` before `app.Run()` in `samples/TLio.Sample.DockerPlugin/Program.cs`
- [X] T0XX [P] [US4] Write integration tests in `samples/TLio.Sample.Api.IntegrationTests/StartupScriptTests.cs`: (a) factory configured with valid `scripts-config.json` → POST /run/{slug} returns 200 immediately; (b) config file with one invalid + one valid entry → invalid slug returns 404, valid slug returns 200; (c) no config file present → server starts normally, no errors

**Checkpoint**: Startup seeding fully functional — Docker deployments can pre-load scripts via mounted config file

---

## Phase 6: User Story 3 — List and Delete Registered Scripts (Priority: P3)

**Goal**: `GET /scripts` returns all registered slugs with source and timestamp; `DELETE /scripts/{slug}` removes a slug and subsequent execution returns 404

**Independent Test**: Register two slugs; GET /scripts returns both; DELETE one; GET /scripts returns one; POST /run on deleted slug returns 404

- [X] T0XX [US3] Add `GET /scripts` handler to `samples/TLio.Sample.Api/Endpoints/ScriptManagementEndpoints.cs` — returns `registry.List()` serialised as JSON array: `[{"slug":"...","source":"...","registeredAt":"..."}]`; returns empty array when none registered
- [X] T0XX [P] [US3] Add `GET /scripts` handler to `samples/TLio.Sample.DockerPlugin/Endpoints/ScriptManagementEndpoints.cs`
- [X] T0XX [US3] Add `DELETE /scripts/{slug}` handler to `samples/TLio.Sample.Api/Endpoints/ScriptManagementEndpoints.cs` — validate slug regex (400); `registry.Delete(slug)`: if true → 204 + log Info `"Slug '{slug}' deleted"`; if false → 404 + log Warning
- [X] T0XX [P] [US3] Add `DELETE /scripts/{slug}` handler to `samples/TLio.Sample.DockerPlugin/Endpoints/ScriptManagementEndpoints.cs`
- [X] T0XX [US3] Map `app.MapGet("/scripts", ...)` and `app.MapDelete("/scripts/{slug}", ...)` in `samples/TLio.Sample.Api/Program.cs`
- [X] T0XX [P] [US3] Map `GET /scripts` and `DELETE /scripts/{slug}` routes in `samples/TLio.Sample.DockerPlugin/Program.cs`
- [X] T0XX [P] [US3] Write integration tests in `samples/TLio.Sample.Api.IntegrationTests/ScriptManagementTests.cs`: (a) GET /scripts with no entries → 200 empty array; (b) GET /scripts after registering two slugs → both present; (c) DELETE known slug → 204; (d) DELETE same slug again → 404; (e) POST /run on deleted slug → 404

**Checkpoint**: All four user stories complete — full slug lifecycle (seed/register → execute → list → delete)

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T0XX Add `SlugCache:MaxBodySizeBytes` (default `10485760`) to `samples/TLio.Sample.Api/appsettings.json` and `appsettings.Development.json`; verify Kestrel reads it (set in T019)
- [X] T0XX [P] Same config key in `samples/TLio.Sample.DockerPlugin/appsettings.json`
- [X] T0XX [P] Update `samples/TLio.Sample.DockerPlugin/docker-compose.yml` — add volume mount `./scripts-config.json:/app/scripts-config.json:ro` and env var `TLIO_SCRIPTS_CONFIG=/app/scripts-config.json`
- [X] T0XX [P] Run `dotnet build` across all four projects (both samples + both integration test projects) and `dotnet test samples/` — confirm zero build errors and all integration tests pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; all four tasks can run in parallel
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories; T005–T012 can all run in parallel; T009/T010 depend on T005/T006; T013/T014 depend on T007–T012; T015/T016 depend on T013/T014
- **US1 (Phase 3)**: Depends on Phase 2 — T017/T018 can run in parallel; T019/T020 depend on T017/T018
- **US2 (Phase 4)**: Depends on Phase 2 — T022–T025 can run in parallel with US4 (Phase 5) since different files
- **US4 (Phase 5)**: Depends on Phase 2 (and `ScriptCompiler` from T013/T014) — can be implemented in parallel with US2
- **US3 (Phase 6)**: Depends on Phase 4 (ScriptManagementEndpoints file created in US2) — T034–T039 add to existing endpoints file
- **Polish (Phase 7)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: No dependency on US2/US3/US4 — uses registry directly seeded in tests
- **US2 (P2)**: No dependency on US1 beyond shared registry — independently testable
- **US4 (P2)**: Depends on `ScriptCompiler` (Phase 2) — does NOT depend on US2's `RegistrationPayloadParser`
- **US3 (P3)**: Depends on US2 (`ScriptManagementEndpoints.cs` file created in T024) — adds GET/DELETE handlers to existing file

### Parallel Opportunities per Phase

**Phase 2**: T005–T008 all parallel; T009/T010 parallel after T005/T006; T011–T014 all parallel; T015/T016 parallel
**Phase 3**: T017/T018 parallel; T019/T020 parallel after T017/T018; T021 parallel with T019/T020
**Phase 4**: T022/T023 parallel; T024/T025 parallel after T022/T023; T026/T027 parallel; T028 parallel with T026/T027
**Phase 5**: T029/T030 parallel; T031/T032 parallel; T033 parallel with T031/T032
**Phase 6**: T034/T035 parallel; T036/T037 parallel; T038/T039 parallel; T040 after T038/T039

---

## Implementation Strategy

### MVP (US1 only — Phases 1–3)

1. Phase 1: Setup test projects + config files
2. Phase 2: Foundational registry + compiler + format detection
3. Phase 3: `POST /run/{slug}` execution endpoint
4. **STOP and VALIDATE**: Seed a script directly in test setup; confirm execution via integration test

### Incremental Delivery

1. Phase 1 + 2 → Foundation ready (both samples)
2. Phase 3 → POST /run/{slug} works (MVP demo via curl)
3. Phase 4 → Runtime registration → register scripts without restart
4. Phase 5 → Startup seeding → Docker deployments pre-load scripts
5. Phase 6 → List + Delete → full lifecycle management
6. Phase 7 → Polish + Docker compose + full test run

### Parallel Team Strategy (2 developers)

- **Dev A**: All TLio.Sample.Api tasks
- **Dev B**: All TLio.Sample.DockerPlugin tasks (all `[P]` tasks in each phase)
- Both proceed through the same phases simultaneously

---

## Notes

- `[P]` tasks operate on different files and have no shared dependencies — safe to run in parallel
- `[Story]` label maps each task to its user story for traceability
- No new commands, functions, or adapters introduced → Article XI (ai-ref.md) does NOT apply
- All execution goes through existing `CompiledScript<TNode>.Execute()` → Article IV satisfied
- No format types in Core/Commands/Functions → Article I/IX satisfied
- Constitution compliance greps not required (no Core/Commands/Functions changes)
- Integration tests use `WebApplicationFactory<Program>` — no mock of registry or engine
- Fixture triplets (Article VI) apply to Core layer tests only; HTTP integration tests use WebApplicationFactory
