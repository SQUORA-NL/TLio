# Tasks: Native XPath Adapter + Adapter-Scoped Test Projects

**Plan:** [plan.md](./plan.md)
**Research:** [research.md](./research.md)
**Status:** Complete

Legend: `[P]` = parallelisable with other `[P]` tasks in the same phase

---

## Phase 1 — Setup: New test projects

**Goal**: Create the two new test project scaffolds and wire them into the solution.

- [x] T001 Create `TLio.Xml.Tests/TLio.Xml.Tests.csproj` — NUnit, refs `TLio.Xml` + `TLio.Client`; add to `TLio.sln`
- [x] T002 Create `TLio.Yaml.Tests/TLio.Yaml.Tests.csproj` — NUnit, refs `TLio.Yaml` + `TLio.Client`; add to `TLio.sln`
- [x] T003 Verify `dotnet build TLio.sln` succeeds with both new (empty) projects

---

## Phase 2 — Foundational: Rename existing fetcher + factory methods

**Goal**: Make the existing slash-path implementation self-documenting and expose named factory
methods so callers can choose their fetcher without breaking current consumers.

- [x] T004 Rename `TLio.Xml/XPathItemsFetcher.cs` → `SlashPathItemsFetcher.cs`; update class name, namespace, and XML doc comment to reflect slash-path convention
- [x] T005 Add `CreateWithSlashPaths()` factory method to `TLio.Xml/XmlExecutionContext.cs`; keep `CreateDefault()` as a call-through alias (no behaviour change)
- [x] T006 Update all references inside `TLio.Xml` (e.g. `XmlScriptParser` if it mentions the old class name) and `TLio.UnitTests` fixture tests to use `SlashPathItemsFetcher` or `CreateWithSlashPaths()`
- [x] T007 Run `dotnet test` — all 724 existing tests must still pass

---

## Phase 3 — US1: `NativeXPathItemsFetcher`

**Goal**: A second `IItemsFetcher<XElement>` that accepts genuine XPath expressions — relative
paths, recursive descent (`//`), predicates (`[@id='x']`), positional (`[1]`) — with no
stripping or rewriting.

**Path conventions**:
- Root indicator: `.` (current context node / self)
- No leading `/` — paths are relative from the root element
- Examples: `name`, `address/city`, `//name`, `items/item[1]`, `items/item[@id='x']`

### Implementation

- [x] T008 [P] [US1] Implement `TLio.Xml/NativeXPathItemsFetcher.cs`:
  - `RootPathIndicator` = `"."`; `PathDelimiter` = `"/"`
  - `SelectNodes(path, data)`: path `"."` returns `[data]`; all other paths forwarded directly to `data.XPathSelectElements(path)` — **no stripping**
  - `GetPath(node)`: walk `node.Parent` chain building `address/city` style (no leading `/`)
  - `SplitParentAndLeaf(path)`: last `/` not inside brackets → `("address", "city")`
  - `EnsurePath(path, root, adapter)`: only handles simple `a/b/c` — creates missing elements; logs warning and no-ops on predicate/axis paths
  - `ResolveRelativePath`: `.` = current node path; `..` = parent path
  - `ProcessIndirectPath`, `GetIntellisense`: same stubs as slash-path variant
- [x] T009 [P] [US1] Add `CreateWithNativeXPath()` factory to `TLio.Xml/XmlExecutionContext.cs`
- [x] T010 [P] [US1] Add `NativeXPathItemsFetcherTests.cs` to `TLio.Xml.Tests/NativeXPath/` — unit tests for `SelectNodes`, `GetPath`, `SplitParentAndLeaf`, `EnsurePath` in isolation (no script execution)

### Fixtures — NativeXPath script format (relative paths, no leading `/`)

- [x] T011 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathSet/01-set-string/fixture.xml` — `<set path="name">new</set>`
- [x] T012 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathSet/02-set-nested/fixture.xml` — `<set path="address/city">new-city</set>`
- [x] T013 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathSet/03-set-descendant/fixture.xml` — `<set path="//city">new-city</set>` (recursive descent — only possible with NativeXPath)
- [x] T014 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathAdd/01-add-new-child/fixture.xml`
- [x] T015 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathRemove/01-remove-child/fixture.xml` — `<remove path="remove-me"/>`
- [x] T016 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathRemove/02-remove-descendant/fixture.xml` — `<remove path="//obsolete"/>` (matches any depth — only NativeXPath)
- [x] T017 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathPut/01-put-new-property/fixture.xml`
- [x] T018 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathCopy/01-copy-child/fixture.xml`
- [x] T019 [P] [US1] Create `TLio.Xml.Tests/Fixtures/XPathMove/01-move-child/fixture.xml`

### Fixture runner

- [x] T020 [US1] Add `TLio.Xml.Tests/NativeXPath/XPathFixtureLoader.cs` — loads `fixture.xml` using same `<fixture><input>…</input><script>…</script><result>…</result></fixture>` format; parses script element as string for `XmlScriptParser`
- [x] T021 [US1] Add `TLio.Xml.Tests/NativeXPath/XPathFixtureTests.cs` — fixture runner using `XmlExecutionContext.CreateWithNativeXPath()`; `TestCaseSource` for each `XPath*` fixture folder
- [x] T022 [US1] Run `dotnet test TLio.Xml.Tests` — all NativeXPath fixture tests pass

---

## Phase 4 — US2: Move XML tests to `TLio.Xml.Tests`

**Goal**: Eliminate all XML test concerns from `TLio.UnitTests`. Every XML test lives in its
own project and references only `TLio.Xml`.

- [x] T023 Move `TLio.UnitTests/Fixtures/XmlFixtureLoader.cs` → `TLio.Xml.Tests/SlashPath/XmlFixtureLoader.cs`; update namespace to `TLio.Xml.Tests.SlashPath`
- [x] T024 Move `TLio.UnitTests/Fixtures/XmlFixtureTests.cs` → `TLio.Xml.Tests/SlashPath/XmlFixtureTests.cs`; update namespace and `using` statements; change `XmlExecutionContext.CreateDefault()` call to `CreateWithSlashPaths()`
- [x] T025 [P] Move `TLio.UnitTests/Fixtures/XmlSet/` directory tree → `TLio.Xml.Tests/Fixtures/XmlSet/`
- [x] T026 [P] Move `TLio.UnitTests/Fixtures/XmlAdd/` → `TLio.Xml.Tests/Fixtures/XmlAdd/`
- [x] T027 [P] Move `TLio.UnitTests/Fixtures/XmlPut/` → `TLio.Xml.Tests/Fixtures/XmlPut/`
- [x] T028 [P] Move `TLio.UnitTests/Fixtures/XmlRemove/` → `TLio.Xml.Tests/Fixtures/XmlRemove/`
- [x] T029 [P] Move `TLio.UnitTests/Fixtures/XmlCopy/` → `TLio.Xml.Tests/Fixtures/XmlCopy/`
- [x] T030 [P] Move `TLio.UnitTests/Fixtures/XmlMove/` → `TLio.Xml.Tests/Fixtures/XmlMove/`
- [x] T031 Add `<Content Include="Fixtures/**/*.xml"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>` to `TLio.Xml.Tests/TLio.Xml.Tests.csproj`
- [x] T032 Move any XML adapter unit tests (`XmlNodeAdapterTests.cs`, `XmlPathFetcherTests.cs`, `XmlCommandIntegrationTests.cs` if they exist) from `TLio.UnitTests/` → `TLio.Xml.Tests/Adapter/`; update namespaces
- [x] T033 Remove `<ProjectReference Include="..\TLio.Xml\TLio.Xml.csproj" />` from `TLio.UnitTests/TLio.UnitTests.csproj`; remove XML `<Content>` glob
- [x] T034 Run `dotnet test TLio.Xml.Tests` — all SlashPath + NativeXPath XML tests pass
- [x] T035 Run `dotnet test TLio.UnitTests` — no XML test failures; total count reduces by number of moved tests

---

## Phase 5 — US3: Move YAML tests to `TLio.Yaml.Tests`

**Goal**: Mirror Phase 4 for YAML. `TLio.UnitTests` no longer references `YamlDotNet` or
`TLio.Yaml`.

- [x] T036 Move `TLio.UnitTests/Fixtures/YamlFixtureLoader.cs` → `TLio.Yaml.Tests/Yaml/YamlFixtureLoader.cs`; update namespace to `TLio.Yaml.Tests.Yaml`
- [x] T037 Move `TLio.UnitTests/Fixtures/YamlFixtureTests.cs` → `TLio.Yaml.Tests/Yaml/YamlFixtureTests.cs`; update namespace and `using` statements
- [x] T038 [P] Move `TLio.UnitTests/Fixtures/YamlSet/` → `TLio.Yaml.Tests/Fixtures/YamlSet/`
- [x] T039 [P] Move `TLio.UnitTests/Fixtures/YamlAdd/` → `TLio.Yaml.Tests/Fixtures/YamlAdd/`
- [x] T040 [P] Move `TLio.UnitTests/Fixtures/YamlPut/` → `TLio.Yaml.Tests/Fixtures/YamlPut/`
- [x] T041 [P] Move `TLio.UnitTests/Fixtures/YamlRemove/` → `TLio.Yaml.Tests/Fixtures/YamlRemove/`
- [x] T042 [P] Move `TLio.UnitTests/Fixtures/YamlCopy/` → `TLio.Yaml.Tests/Fixtures/YamlCopy/`
- [x] T043 [P] Move `TLio.UnitTests/Fixtures/YamlMove/` → `TLio.Yaml.Tests/Fixtures/YamlMove/`
- [x] T044 Add `<Content Include="Fixtures/**/*.yaml"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>` to `TLio.Yaml.Tests/TLio.Yaml.Tests.csproj`
- [x] T045 Move any YAML adapter unit tests (`YamlNodeAdapterTests.cs`, `YamlPathFetcherTests.cs`, `YamlCommandIntegrationTests.cs` if they exist) from `TLio.UnitTests/` → `TLio.Yaml.Tests/Adapter/`; update namespaces
- [x] T046 Remove `<ProjectReference Include="..\TLio.Yaml\TLio.Yaml.csproj" />` from `TLio.UnitTests/TLio.UnitTests.csproj`; remove YAML `<Content>` glob
- [x] T047 Run `dotnet test TLio.Yaml.Tests` — all YAML tests pass
- [x] T048 Run `dotnet test TLio.UnitTests` — no YAML failures; project no longer references `YamlDotNet`

---

## Phase 6 — Polish

- [x] T049 Run `dotnet test` (full solution) — all projects pass; confirm total count = sum of individual projects
- [x] T050 Verify constitution compliance: `grep -rn "System\.Xml\|YamlDotNet" TLio.UnitTests/` returns **zero** results
- [x] T051 Update `specs/003-xml-xpath-implementation/tasks.md` — mark all tasks complete
- [x] T052 Update `specs/001-tlio-core-architecture/tasks.md` — annotate Phase 6/7 entries as superseded by spec 003 for the test-project separation

---

## Dependency Graph

```
Phase 1 (T001–T003)
    ↓
Phase 2 (T004–T007)          ← must precede all phases below (rename bakes in)
    ├──────────────────────────────────────────────┐
    ↓                                              ↓
Phase 3 (T008–T022)          Phase 4 (T023–T035) + Phase 5 (T036–T048)
[NativeXPath impl]           [move existing tests — independent of Phase 3]
    └──────────────────────────────────────────────┘
                              ↓
                          Phase 6 (T049–T052)
```

Phases 3, 4, and 5 are **independent** of each other after Phase 2 completes.
