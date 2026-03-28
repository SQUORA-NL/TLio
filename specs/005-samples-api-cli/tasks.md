# Tasks: Sample Projects — API and CLI

**Input**: Design documents from `specs/005-samples-api-cli/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Organization**: Tasks grouped by user story for independent delivery.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 = API Sample, US2 = CLI Sample

---

## Phase 1: Setup (Solution Scaffolding)

**Purpose**: Create both sample projects and register them in the solution. Nothing compiles until T004 passes.

- [x] T001 [P] Create `samples/TLio.Sample.Api/TLio.Sample.Api.csproj` targeting net10.0 with `<OutputType>Exe</OutputType>`; add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`; add project references to TLio.Json, TLio.Xml, TLio.Yaml, TLio.Client; add `<Content Include="Scripts/**/*.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>` and same for `SampleInput/**/*.json`, `SampleInput/**/*.xml`, `SampleInput/**/*.yaml`
- [x] T002 [P] Create `samples/TLio.Sample.Cli/TLio.Sample.Cli.csproj` targeting net10.0 with `<OutputType>Exe</OutputType>`; add project references to TLio.Json, TLio.Xml, TLio.Yaml, TLio.Client; add content items for `Scripts/**/*.json` and `SampleInput/**` (all extensions) with `CopyToOutputDirectory=PreserveNewest`
- [x] T003 Add both `samples/TLio.Sample.Api/TLio.Sample.Api.csproj` and `samples/TLio.Sample.Cli/TLio.Sample.Cli.csproj` to `TLio.sln` (two new `Project(...)` entries and corresponding `GlobalSection` build-configuration entries)
- [x] T004 Run `dotnet build TLio.sln` and confirm zero errors (both new projects must compile alongside existing projects)

**Checkpoint**: Solution builds with both sample project stubs present — user story implementation can now begin.

---

## Phase 2: Foundational

No blocking prerequisites beyond Phase 1. Both user stories can proceed in parallel after T004 passes.

---

## Phase 3: User Story 1 — API Sample (Priority: P1) 🎯 MVP

**Goal**: A running ASP.NET Core Minimal API with three POST endpoints (`/transform/json`, `/transform/xml`, `/transform/yaml`), each executing a bundled TLio script and returning the correct HTTP status code and `Content-Type` header.

**Independent Test**: `dotnet run --project samples/TLio.Sample.Api` starts without errors; `curl -X POST http://localhost:5100/transform/json -H "Content-Type: application/json" -d '{"name":"Alice"}'` returns HTTP 200 with `Content-Type: application/json` and a JSON body containing a `greeting` field.

### Bundled scripts (all [P] — independent files)

- [x] T005 [P] [US1] Create `samples/TLio.Sample.Api/Scripts/transform-json.json` with content: `[{"command":"add","path":"$.greeting","value":"Hello from TLio!"}]` — adds a `greeting` property to any JSON object
- [x] T006 [P] [US1] Create `samples/TLio.Sample.Api/Scripts/transform-xml.json` with content: `[{"command":"add","path":"person/greeting","value":"Hello from TLio!"}]` — adds a `<greeting>` element under the root `<person>` element (NativeXPath, root = `.`)
- [x] T007 [P] [US1] Create `samples/TLio.Sample.Api/Scripts/transform-yaml.json` with content: `[{"command":"add","path":"$.greeting","value":"Hello from TLio!"}]` — adds a `greeting` key to any YAML mapping

### Sample input files (all [P] — independent files)

- [x] T008 [P] [US1] Create `samples/TLio.Sample.Api/SampleInput/sample.json` with content: `{"name":"Alice","age":30}` (used in quickstart.md examples and README)
- [x] T009 [P] [US1] Create `samples/TLio.Sample.Api/SampleInput/sample.xml` with content: `<person><name>Alice</name><age>30</age></person>` (used in README examples)
- [x] T010 [P] [US1] Create `samples/TLio.Sample.Api/SampleInput/sample.yaml` with content: `name: Alice\nage: 30` (used in README examples)

### Implementation (depends on T005-T010)

- [x] T011 [US1] Create `samples/TLio.Sample.Api/TransformService.cs` — static class with `Execute(string format, string payload)` method that: (1) maps `format` string to `SupportedFormat` enum, returns null for unknown format; (2) selects the appropriate execution context (`JsonExecutionContext.CreateDefault()` / `XmlExecutionContext.CreateWithNativeXPath()` / `YamlExecutionContext.CreateDefault()`); (3) loads the corresponding script file from `Scripts/transform-{format}.json` relative to `AppContext.BaseDirectory`; (4) parses payload via `adapter.Parse(payload)`; (5) runs `ScriptEngine<TNode>.Execute(script, input, context)`; (6) serializes result via `adapter.Serialize(result.Data)`; (7) returns `(bool success, string output, IReadOnlyList<LogEntry> log)`
- [x] T012 [US1] Create `samples/TLio.Sample.Api/Program.cs` — ASP.NET Core Minimal API targeting port 5100: (1) `app.MapPost("/transform/json", ...)` reads body as string, calls `TransformService`, returns 200+`application/json` on success, 400 for empty/null body, 422 for engine failure with `{ "error": "...", "log": [...] }`; (2) identical handlers for `/transform/xml` (content-type `application/xml`) and `/transform/yaml` (content-type `text/yaml`); (3) 400 on empty body for all formats; note: `ScriptEngine<TNode>` must be constructed with `ParseOptions<TNode>.CreateDefault().CommandsProvider` and `.FunctionsProvider` per adapter type
- [x] T013 [US1] Create `samples/TLio.Sample.Api/README.md` — documents: (1) prerequisites (`dotnet run`), (2) `curl` examples for all 3 formats matching quickstart.md, (3) what the bundled scripts do, (4) how to supply your own script by replacing the Scripts/ files

**Checkpoint**: `dotnet run --project samples/TLio.Sample.Api` starts; all three curl examples in quickstart.md return 200 with correct content-type and a `greeting` field in the output.

---

## Phase 4: User Story 2 — CLI Sample (Priority: P2)

**Goal**: A runnable console application that accepts `--input <file>` and `--script <file>` arguments, detects format from file extension, executes the transformation, writes output to stdout (or `--output <file>`), and exits with the correct code per `contracts/cli.md`.

**Independent Test**: `dotnet run --project samples/TLio.Sample.Cli -- --input samples/TLio.Sample.Cli/SampleInput/sample.json --script samples/TLio.Sample.Cli/Scripts/transform-json.json` prints the transformed JSON to stdout and exits 0. Running with a missing input file prints an error to stderr and exits 1.

### Bundled scripts (all [P] — independent files, same content as Phase 3)

- [x] T014 [P] [US2] Create `samples/TLio.Sample.Cli/Scripts/transform-json.json` with content: `[{"command":"add","path":"$.greeting","value":"Hello from TLio!"}]`
- [x] T015 [P] [US2] Create `samples/TLio.Sample.Cli/Scripts/transform-xml.json` with content: `[{"command":"add","path":"person/greeting","value":"Hello from TLio!"}]`
- [x] T016 [P] [US2] Create `samples/TLio.Sample.Cli/Scripts/transform-yaml.json` with content: `[{"command":"add","path":"$.greeting","value":"Hello from TLio!"}]`

### Sample input files (all [P] — independent files)

- [x] T017 [P] [US2] Create `samples/TLio.Sample.Cli/SampleInput/sample.json` with content: `{"name":"Alice","age":30}`
- [x] T018 [P] [US2] Create `samples/TLio.Sample.Cli/SampleInput/sample.xml` with content: `<person><name>Alice</name><age>30</age></person>`
- [x] T019 [P] [US2] Create `samples/TLio.Sample.Cli/SampleInput/sample.yaml` with content: `name: Alice\nage: 30`

### Implementation

- [x] T020 [US2] Create `samples/TLio.Sample.Cli/Program.cs` — top-level statements: (1) parse `--input`, `--script`, `--output`, `--help` from `args` (simple loop, no library needed); (2) show help and exit 0 if `--help`; (3) return exit code 1 if `--input` file not found, exit code 2 if `--script` file not found; (4) detect format from `--input` extension (`.json`→Json, `.xml`→Xml, `.yaml`/`.yml`→Yaml), exit code 3 for unknown extension; (5) parse input file via appropriate adapter, exit code 3 on parse exception; (6) parse script file via `System.Text.Json.JsonDocument`, exit code 4 on parse error; (7) build `ScriptEngine<TNode>` using `ParseOptions<TNode>.CreateDefault()`; (8) run engine, serialize result, write to stdout or `--output` file, exit 0; (9) on engine failure, write `result.Success == false` message + log to stderr, exit 5; (10) wrap all unexpected exceptions, write to stderr, exit 10
- [x] T021 [US2] Create `samples/TLio.Sample.Cli/README.md` — documents: (1) all argument options matching `contracts/cli.md`, (2) exit code table, (3) invocation examples from quickstart.md for all 3 formats, (4) format auto-detection rules, (5) how to use your own input files and scripts

**Checkpoint**: All three quickstart.md CLI examples execute successfully; exit code is 0 for success and non-zero for missing file.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Verify full solution integrity and smoke-test both samples end-to-end.

- [x] T022 Run `dotnet build TLio.sln` — all projects (including both samples) must compile with zero errors and zero warnings
- [x] T023 [P] Run `dotnet test TLio.sln` — confirm no regressions in the existing 838-test suite (TLio.UnitTests, TLio.Json.Tests, TLio.Json.SystemText.Tests, TLio.Functions.Tests, TLio.Xml.Tests, TLio.Yaml.Tests)
- [x] T024 [P] Smoke-test API sample: follow all curl examples in `specs/005-samples-api-cli/quickstart.md` and confirm correct status codes, content-types, and output shape for all 3 formats
- [x] T025 [P] Smoke-test CLI sample: follow all `dotnet run` examples in `specs/005-samples-api-cli/quickstart.md` and confirm correct stdout output and exit codes for success and error cases

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — T001 and T002 can run in parallel immediately
- **Phase 2 (Foundational)**: No blocking tasks
- **Phase 3 (US1 — API)**: Requires T004 (build passes); T005–T010 parallel; T011–T013 sequential after T005–T010
- **Phase 4 (US2 — CLI)**: Requires T004 (build passes); T014–T019 parallel; T020–T021 sequential after T014–T019
- **Phase 5 (Polish)**: Requires both Phase 3 and Phase 4 complete; T023–T025 parallel after T022

### User Story Dependencies

- **US1 (P1)**: Independent — only depends on T004 (build). No dependency on US2.
- **US2 (P2)**: Independent — only depends on T004 (build). No dependency on US1.

Both stories can be implemented in parallel once Phase 1 is complete.

### Within Each User Story

- Script files (T005-T007, T014-T016) and sample inputs (T008-T010, T017-T019) are all parallel
- `TransformService.cs` / `Program.cs` implementation depends on script and input files being in place
- README depends on knowing the final curl/CLI invocations work

---

## Parallel Execution Examples

### User Story 1 (API Sample) — scripts and inputs in parallel

```
T005  create Scripts/transform-json.json
T006  create Scripts/transform-xml.json
T007  create Scripts/transform-yaml.json
T008  create SampleInput/sample.json
T009  create SampleInput/sample.xml
T010  create SampleInput/sample.yaml
         ↓ all done
T011  TransformService.cs
T012  Program.cs
T013  README.md
```

### User Story 2 (CLI Sample) — can run fully in parallel with US1 after T004

```
T014  create Scripts/transform-json.json
T015  create Scripts/transform-xml.json
T016  create Scripts/transform-yaml.json
T017  create SampleInput/sample.json
T018  create SampleInput/sample.xml
T019  create SampleInput/sample.yaml
         ↓ all done
T020  Program.cs
T021  README.md
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T004)
2. Complete Phase 3: API Sample (T005–T013)
3. **STOP and VALIDATE**: `dotnet run --project samples/TLio.Sample.Api`, test all 3 curl examples
4. US1 done — ship or continue to US2

### Incremental Delivery

1. Setup → Phase 1
2. API Sample → Phase 3 (most visible, demonstrates HTTP integration)
3. CLI Sample → Phase 4 (automation/pipeline pattern)
4. Polish → Phase 5

---

## Notes

- `[P]` tasks = different files, no dependencies — can run in parallel
- `[Story]` label maps each task to its user story for traceability
- Script files have identical content in both sample projects (deliberate duplication — each project is self-contained)
- The `TransformService` in the API is an internal helper class, not a shared library — no new project is needed (Article VII)
- Constitutional compliance (Articles I–X) is not required for sample projects — they are consumer entry-points, not Core/Commands/Functions code
- Both projects use `ParseOptions<TNode>.CreateDefault()` from `TLio.Client` — this is the correct consumer pattern
