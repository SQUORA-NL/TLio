# Tasks: NuGet Package Deployment

**Input**: Design documents from `specs/011-nuget-packaging/`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ quickstart.md ✅

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to
- Exact file paths included in every description

---

## Phase 1: Foundational (Blocking Prerequisite)

**Purpose**: Extend `Directory.Build.props` with shared NuGet metadata and set `IsPackable=false` as the default for all projects. This must be in place before any project-level packaging tasks or workflow files can be written.

**⚠️ CRITICAL**: No user story work can begin until T001–T002 are complete.

- [ ] T001 Extend `Directory.Build.props` to add shared NuGet metadata: `<Authors>Frans van Ek</Authors>`, `<PackageLicenseExpression>MIT</PackageLicenseExpression>`, `<PackageProjectUrl>https://github.com/FransVanEk/TLio</PackageProjectUrl>`, `<RepositoryUrl>https://github.com/FransVanEk/TLio</RepositoryUrl>`, `<RepositoryType>git</RepositoryType>`, `<IsPackable>false</IsPackable>`, `<VersionPrefix>0.1.0</VersionPrefix>`
- [ ] T002 Create `.github/workflows/` directory (empty placeholder to unblock workflow tasks)

**Checkpoint**: `Directory.Build.props` has NuGet metadata + `IsPackable=false` default. All user story work can begin.

---

## Phase 2: User Story 1 — Stable Release Packages (Priority: P1) 🎯 MVP

**Goal**: All 12 library projects produce `.nupkg` files when `dotnet pack` is run. Test and sample projects produce nothing.

**Independent Test**: Running `dotnet pack --no-build -o ./out` from the repo root produces exactly 12 `.nupkg` files — one per library project — and no `.nupkg` for any test or sample project.

### Implementation for User Story 1

- [ ] T003 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting json transformation</PackageTags>` to `TLio.Core/TLio.Core.csproj`
- [ ] T004 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting commands</PackageTags>` to `TLio.Commands/TLio.Commands.csproj`
- [ ] T005 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting functions</PackageTags>` to `TLio.Functions/TLio.Functions.csproj`
- [ ] T006 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting engine client</PackageTags>` to `TLio.Client/TLio.Client.csproj`
- [ ] T007 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting json newtonsoft</PackageTags>` to `TLio.Json/TLio.Json.csproj`
- [ ] T008 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting json systemtext rfc9535</PackageTags>` to `TLio.Json.SystemText/TLio.Json.SystemText.csproj`
- [ ] T009 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting xml xpath</PackageTags>` to `TLio.Xml/TLio.Xml.csproj`
- [ ] T010 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting yaml</PackageTags>` to `TLio.Yaml/TLio.Yaml.csproj`
- [ ] T011 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting etl flatten restore csv</PackageTags>` to `TLio.Extensions.ETL/TLio.Extensions.ETL.csproj`
- [ ] T012 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting math functions</PackageTags>` to `TLio.Extensions.Math/TLio.Extensions.Math.csproj`
- [ ] T013 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting text string functions</PackageTags>` to `TLio.Extensions.Text/TLio.Extensions.Text.csproj`
- [ ] T014 [P] [US1] Add `<IsPackable>true</IsPackable>` and `<PackageTags>tlio scripting datetime functions</PackageTags>` to `TLio.Extensions.TimeDate/TLio.Extensions.TimeDate.csproj`
- [ ] T015 [US1] Run `dotnet build` then `dotnet pack --no-build -o ./nupkg-out` and verify: exactly 12 `.nupkg` files exist in `./nupkg-out/`, none named `TLio.*.Tests` or `TLio.Sample.*`, then delete `./nupkg-out/`

**Checkpoint**: US1 independently verifiable — `dotnet pack` produces exactly 12 library packages.

---

## Phase 3: User Story 2 — Preview and Release CI/CD Pipelines (Priority: P1)

**Goal**: GitHub Actions publishes preview packages on every `main` push and stable packages on every `v*` tag. All 12 packages share the same version per run.

**Independent Test**: After merging to `main`, all 12 packages appear on NuGet.org with a `-preview.{N}` version within 10 minutes. After pushing `v0.1.0`, all 12 appear without a pre-release suffix.

### Implementation for User Story 2

- [ ] T016 [US2] Create `.github/workflows/ci.yml`: triggers on `push` to all branches and `pull_request` targeting `main`; single `test` job running `dotnet test` on `ubuntu-latest` with .NET 10
- [ ] T017 [US2] Create `.github/workflows/publish.yml`: triggers on `push` to `main` (preview) and `push` of tags matching `v*` (release); two jobs — `test` (same as ci.yml) and `publish` (depends on `test`); `publish` job computes version (`0.1.0-preview.${{ github.run_number }}` for main, tag-derived `X.Y.Z` for release tag), runs `dotnet pack /p:Version={VERSION}`, then `dotnet nuget push **/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate`

**Checkpoint**: US2 independently verifiable — pipelines exist and are syntactically valid YAML.

---

## Phase 4: User Story 3 — Correct Transitive Dependencies (Priority: P2)

**Goal**: Installing any top-level TLio package automatically restores all required TLio transitive dependencies. No consumer needs to manually install TLio sub-packages.

**Independent Test**: Inspect the `.nupkg` for `TLio.Json` (unzip and read `.nuspec`) and confirm it lists `TLio.Core` as a dependency. Inspect `TLio.Client.nupkg` and confirm it lists `TLio.Core`, `TLio.Commands`, and `TLio.Functions`.

### Implementation for User Story 3

- [ ] T018 [US3] Verify `TLio.Json` package dependencies: build and pack, unzip `TLio.Json.*.nupkg`, read the `.nuspec` file and confirm `<dependencies>` contains `TLio.Core` — no manual fix needed if `ProjectReference` is present (it is automatic), but document the finding
- [ ] T019 [US3] Verify `TLio.Client` package dependencies: same process — confirm `.nuspec` lists `TLio.Core`, `TLio.Commands`, and `TLio.Functions` as dependencies
- [ ] T020 [US3] Verify `TLio.Xml` and `TLio.Yaml` package dependencies: confirm each `.nuspec` lists both `TLio.Core` and `TLio.Client` as dependencies
- [ ] T021 [US3] Verify `TLio.Extensions.ETL` lists `TLio.Core` and `TLio.Commands`; verify all four extension packages (`ETL`, `Math`, `Text`, `TimeDate`) list at minimum `TLio.Core`

**Checkpoint**: US3 independently verifiable — all `.nuspec` dependency declarations are correct.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Verification, documentation, and cleanup.

- [ ] T022 Run `dotnet build` and `dotnet test` from repository root and confirm zero failures (no regressions from metadata changes)
- [ ] T023 [P] Validate preview version suffix: run `dotnet pack /p:VersionSuffix=preview.999 -o ./test-out` and confirm all 12 packages are named `*0.1.0-preview.999.nupkg`, then delete `./test-out/`
- [ ] T024 [P] Validate release version override: run `dotnet pack /p:Version=1.0.0 -o ./test-out` and confirm all 12 packages are named `*1.0.0.nupkg`, then delete `./test-out/`
- [ ] T025 Update `specs/011-nuget-packaging/checklists/requirements.md` to mark all items verified after T022–T024 pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — start immediately
- **US1 (Phase 2)**: Depends on T001 (shared metadata must be in Directory.Build.props first)
- **US2 (Phase 3)**: Depends on T001–T002; T016 and T017 can be written in parallel with US1 tasks
- **US3 (Phase 4)**: Depends on T003–T015 (packages must be buildable before inspecting .nuspec)
- **Polish (Phase 5)**: Depends on all phases complete

### User Story Dependencies

- **US1 (P1)**: Start after T001. T003–T014 are all [P] and can run simultaneously.
- **US2 (P1)**: Start after T001–T002. T016 and T017 are independent workflow files.
- **US3 (P2)**: Start after US1 is complete (needs packable projects).

### Within Each User Story

- T003–T014 are fully independent (different `.csproj` files) — run all in parallel
- T015 depends on T003–T014 all being complete
- T016 and T017 are independent workflow files — can be written in parallel
- T018–T021 are independent inspection tasks — can run in parallel after T015

### Parallel Opportunities

- T003–T014: all 12 csproj updates run simultaneously (different files)
- T016 and T017: two workflow files, fully independent
- T018–T021: four package inspection tasks, fully independent
- T023 and T024: two validation runs, independent

---

## Parallel Example: User Story 1 (csproj updates)

```text
All 12 IsPackable tasks run in parallel (different files):
T003: TLio.Core/TLio.Core.csproj
T004: TLio.Commands/TLio.Commands.csproj
T005: TLio.Functions/TLio.Functions.csproj
T006: TLio.Client/TLio.Client.csproj
T007: TLio.Json/TLio.Json.csproj
T008: TLio.Json.SystemText/TLio.Json.SystemText.csproj
T009: TLio.Xml/TLio.Xml.csproj
T010: TLio.Yaml/TLio.Yaml.csproj
T011: TLio.Extensions.ETL/TLio.Extensions.ETL.csproj
T012: TLio.Extensions.Math/TLio.Extensions.Math.csproj
T013: TLio.Extensions.Text/TLio.Extensions.Text.csproj
T014: TLio.Extensions.TimeDate/TLio.Extensions.TimeDate.csproj
→ Then T015 (verification) runs once all are done
```

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1 (Foundational): T001–T002
2. Complete Phase 2 (US1): T003–T015
3. Complete Phase 3 (US2): T016–T017
4. **STOP and VALIDATE**: Packages build locally, pipelines are in place.
5. Proceed to US3 and Polish as time allows.

### Incremental Delivery

1. T001–T002 → Shared metadata + workflow directory ready
2. T003–T015 → `dotnet pack` works locally (US1 ✅)
3. T016–T017 → CI/CD pipelines live (US2 ✅)
4. T018–T021 → Transitive dependencies verified (US3 ✅)
5. T022–T025 → Verified and clean

---

## Notes

- `[P]` tasks = different files, no dependencies — can run in parallel
- `[Story]` label maps task to specific user story for traceability
- No new C# library code is introduced — only `.csproj` metadata and GitHub Actions YAML
- Constitution Articles I–XI are not applicable (no commands, functions, or adapters added)
- The `NUGET_API_KEY` secret must be added in GitHub repository settings before the first publish run
- Commit after Phase 1 checkpoint, after Phase 2 checkpoint, after Phase 3 workflow files
