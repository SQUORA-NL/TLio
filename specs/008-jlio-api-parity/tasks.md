# Tasks: JLio API Parity

**Input**: Design documents from `/specs/008-jlio-api-parity/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Organization**: Tasks grouped by user story. US1 and US2 are P1 (blocking for migration).
US3 and US4 are P2. US5 is P3 (documentation only).

---

## Phase 1: Setup

**Purpose**: Bring feature 007 ai-ref files onto this branch (US5 depends on them).

- [x] T001 Merge branch `007-create-ai-ref-docs` into `008-jlio-api-parity`: `git merge 007-create-ai-ref-docs` — this creates the `docs/ai-ref/` tree that US5 will update

---

## Phase 2: Foundational (CommandConverter Fixes)

**Purpose**: Fix `TLio.Client/CommandConverter.cs` — these changes unblock ETL settings deserialization (US1 AC-4) and the `"decisionTable"` key mapping (US1 AC-3). All three tasks touch the same file; run sequentially.

**⚠️ CRITICAL**: ETL settings and DecisionTable key features require these before US1 is testable.

- [x] T002 Add POCO fallback in `CommandConverter.ConvertJsonValue`: after all existing type-switch cases, add `System.Text.Json.JsonSerializer.Deserialize(element.GetRawText(), targetType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })` for non-generic, non-abstract class types — this enables `FlattenSettings`, `RestoreSettings`, `CsvSettings` to deserialize from JSON in `TLio.Client/CommandConverter.cs`
- [x] T003 Handle `"decisionTable"` JSON key → `Config` property in `CommandConverter.ParseCommand` (file `TLio.Client/CommandConverter.cs`): before the general `GetProperty(propName)` lookup, check `if (propName == "DecisionTable" && command is DecisionTable<TNode>) propName = "Config";` — C# prohibits a property with the same name as its enclosing type
- [x] T004 Add `List<ResolveSetting<TNode>>` custom parsing in `CommandConverter.ConvertJsonValue` (file `TLio.Client/CommandConverter.cs`): detect `targetType` is `List<>` with element type `ResolveSetting<>` and parse using `FunctionConverter<TNode>` for `Value` fields; the generic constraint check: `targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>) && targetType.GenericTypeArguments[0].IsGenericType && targetType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(ResolveSetting<>)`

**Checkpoint**: CommandConverter can now deserialize ETL settings and handle decisionTable key — verify with `dotnet build TLio.Client`.

---

## Phase 3: User Story 1 — Property Name Alignment (Priority: P1)

**Goal**: JLio scripts using `fromPath`/`toPath` on `compare`, `merge`, `decisionTable` key, and ETL settings keys run unchanged on TLio.

**Independent Test**: Run `dotnet test TLio.UnitTests` — new fixture files for compare/merge aliases pass; DecisionTable fixture with `"decisionTable"` key passes; ETL flatten fixture with `"flattenSettings"` passes.

### Implementation

- [x] T005 [P] [US1] Add write-only alias properties to `TLio.Commands/Advanced/Compare.cs`: `public string? FromPath { set => FirstPath = value; }` and `public string? ToPath { set => SecondPath = value; }` — these let JLio `"fromPath"`/`"toPath"` keys map via ToPascalCase to `"FromPath"`/`"ToPath"`
- [x] T006 [P] [US1] Add write-only alias properties to `TLio.Commands/Advanced/Merge.cs`: `public string? FromPath { set => Path = value; }` and `public string? ToPath { set => TargetPath = value; }` — same approach for JLio merge key alignment

### Fixture Triplets (TDD — create fixture files now; they will fail until T005/T006/T002/T003 are done)

- [x] T007 [P] [US1] Create fixture `TLio.UnitTests/Fixtures/Compare/04-compare-from-to-path/fixture.json` with `{ "input": {"a":10,"b":20}, "script": [{"command":"compare","fromPath":"$.a","toPath":"$.b","resultPath":"$.result"}], "result": {"a":10,"b":20,"result":"less"} }` — verifies JLio `fromPath`/`toPath` aliases work end-to-end via ScriptEngine
- [x] T008 [P] [US1] Create fixture `TLio.UnitTests/Fixtures/Merge/03-merge-from-to-path/fixture.json` with `{ "input": {"src":{"name":"Alice"},"dst":{"id":1}}, "script": [{"command":"merge","fromPath":"$.src","toPath":"$.dst"}], "result": {"src":{"name":"Alice"},"dst":{"id":1,"name":"Alice"}} }` — verifies JLio `fromPath`/`toPath` aliases for merge
- [x] T009 [P] [US1] Create fixture directory `TLio.UnitTests/Fixtures/DecisionTable/01-decision-table-key/fixture.json` with a script using the `"decisionTable"` JSON key (not `"config"`) for the config object, and add a `DecisionTable()` test method to `TLio.UnitTests/Fixtures/FixtureTests.cs` using `[TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "DecisionTable" })]` — requires T003 to pass
- [x] T010 [P] [US1] Create `TLio.UnitTests/EtlFixtureTests.cs` that sets up `ParseOptions` with `RegisterETL<JToken>()` and runs fixtures from `TLio.UnitTests/Fixtures/Flatten/`; create fixture `TLio.UnitTests/Fixtures/Flatten/01-flatten-with-settings/fixture.json` with `flattenSettings: {"delimiter":"_"}` — verifies POCO settings deserialization (requires T002)

### Documentation

- [x] T011 [US1] Update `docs/ai-ref/commands/Compare.md`: change primary property names in Options table from `firstPath`/`secondPath` to `fromPath`/`toPath` (JLio-aligned); keep `firstPath`/`secondPath` as aliases in a note — requires T001
- [x] T012 [US1] Update `docs/ai-ref/commands/Merge.md`: change primary property names from `path`/`targetPath` to `fromPath`/`toPath` (JLio-aligned); keep `path`/`targetPath` as aliases in a note — requires T001
- [x] T013 [US1] Update `specs/002-migration-from-jlio/porting-guide.md`: add section "Property Name Aliases (008)" documenting all alias mappings from FR-001–FR-007 (constitution §VIII, FR-008)

**Checkpoint**: `dotnet test TLio.UnitTests` passes all Compare/Merge/DecisionTable/Flatten fixtures including the new ones.

---

## Phase 4: User Story 2 — Missing Core Functions (Priority: P1)

**Goal**: `=newGuid()`, `=fetch(path,default)`, `=path()`, `=promote(path,name)` work in TLio scripts using `ParseOptions.CreateDefault()`.

**Independent Test**: `dotnet test TLio.Functions.Tests` — new Fetch fixture with default value passes; new Promote fixture with explicit name passes; path alias fixture passes; NewGuid inline tests pass.

### Implementation

- [x] T014 [P] [US2] Create `TLio.Functions/NewGuid.cs` with `class NewGuid<TNode> : FunctionBase<TNode>` that returns `context.NodeAdapter.CreateString(Guid.NewGuid().ToString())` (no arguments); call `LogInfo` on success per Article X — registration in `ParseOptions.cs` happens in T016 (same file); note: `TLio.Extensions.Text` also has a `NewGuid` registered as `"newguid"` (lowercase); both coexist without conflict
- [x] T015 [P] [US2] Modify `TLio.Functions/Fetch.cs`: in the "path resolved to nothing" branch, before returning `Failed`, check `Arguments.Count >= 2`; if so, evaluate `Arguments[1]` as the default value and return it as `Successful`; existing 1-arg behaviour unchanged
- [x] T016 [US2] Register both `"newGuid"` and `"path"` in `TLio.Client/ParseOptions.cs` `CreateDefault()` in a single edit: `options.FunctionsProvider.Register("newGuid", () => new NewGuid<TNode>())` and `options.FunctionsProvider.Register("path", () => new ScriptPath<TNode>())` — runs after T014 since T014 creates the NewGuid class
- [x] T017 [P] [US2] Modify `TLio.Functions/Promote.cs`: add optional second argument; when `Arguments.Count >= 2`, evaluate `Arguments[1]` to get the explicit property name string; when absent, keep existing `GetParentPropertyName()` behaviour

### Fixture Triplets (TDD — create fixture files now; they will fail until T015/T016/T017 are done)

- [x] T018 [P] [US2] Create fixture `TLio.Functions.Tests/Fixtures/Fetch/04-fetch-with-default/fixture.json`: `{ "input": {}, "script": [{"command":"add","path":"$.name","value":"=fetch($.missing,'Unknown')"}], "result": {"name":"Unknown"} }` — tests the new default-value path when path not found
- [x] T019 [P] [US2] Create fixture `TLio.Functions.Tests/Fixtures/Promote/02-promote-with-name/fixture.json`: `{ "input": {"rawValue":42}, "script": [{"command":"add","path":"$.wrapped","value":"=promote($.rawValue,'data')"}], "result": {"rawValue":42,"wrapped":{"data":42}} }` — tests the explicit name argument
- [x] T020 [P] [US2] Create fixture `TLio.Functions.Tests/Fixtures/ScriptPath/04-path-alias/fixture.json`: `{ "input": {"items":[{"id":1},{"id":2}]}, "script": [{"command":"add","path":"$.items[*].loc","value":"=path()"}], "result": {"items":[{"id":1,"loc":"$.items[0]"},{"id":2,"loc":"$.items[1]"}]} }` — tests the `"path"` alias resolves same as `"scriptpath"` (requires T016)

### AI Reference

- [x] T021 [P] [US2] Create `docs/ai-ref/functions/NewGuid.md` (Article XI): Intent line, Syntax `=newGuid()`, Options (none), Formats (all), Example with `{"command":"add","path":"$.id","value":"=newGuid()"}` — requires T001
- [x] T022 [P] [US2] Create `docs/ai-ref/functions/Path.md` (Article XI): Intent line (alias for scriptpath), Syntax `=path()`, Options (none), Formats (all), note that `=path()` and `=scriptpath()` are identical — requires T001
- [x] T023 [P] [US2] Update `docs/ai-ref/functions/Fetch.md`: add second optional argument signature `=fetch(path, defaultValue)` to Syntax and Options table — requires T001
- [x] T024 [P] [US2] Update `docs/ai-ref/functions/Promote.md`: add second optional argument signature `=promote(path, propertyName)` to Syntax and Options table — requires T001

**Checkpoint**: `dotnet test TLio.Functions.Tests` passes all new Fetch/Promote/ScriptPath fixtures.

---

## Phase 5: User Story 3 — Fluent Builder API (Priority: P2)

**Goal**: C# code can build TLio scripts with `new TLioScript<TNode>().Add(v).OnPath(p).Set(v).OnPath(p).Copy().From(p).To(p)` etc. `TLioConvert.Parse` and `TLioConvert.Serialize` work.

**Independent Test**: Inline C# test creates a fluent script, serializes it with `TLioConvert.Serialize`, parses it back with `TLioConvert.Parse`, and the result is deeply equal to the original — verified in `TLio.UnitTests/ClientTests/`.

### Implementation

- [x] T025 [US3] Create the intermediate builder types in `TLio.Client/Fluent/` (one file is acceptable; separate for clarity): `ValueOnPathBuilder<TNode>` (for Add/Set/Put), `RemoveOnPathBuilder<TNode>`, `CopyMoveFromBuilder<TNode>`, `CopyMoveToBuilder<TNode>`, `CompareFromBuilder<TNode>`, `CompareToBuilder<TNode>`, `CompareResultBuilder<TNode>`, `MergeFromBuilder<TNode>`, `MergeToBuilder<TNode>`, `IfElseIfBuilder<TNode>`, `IfElseElseBuilder<TNode>` — all in namespace `TLio.Client`
- [x] T026 [US3] Create `TLio.Client/Fluent/TLioScriptExtensions.cs`: extension methods on `TLioScript<TNode>` for `Add(TNode)`, `Set(TNode)`, `Put(TNode)`, `Remove()`, `Copy()`, `Move()`, `Compare()`, `Merge()`, `IfElse(IFunctionSupportedValue<TNode>)` — each method wraps the value in `FixedValue<TNode>` and returns the appropriate builder from T025 — requires T025
- [x] T027 [P] [US3] Create `TLio.Client/TLioConvert.cs`: static class with `Parse<TNode>(string scriptJson, ParseOptions<TNode> options, INodeAdapter<TNode> adapter)` delegating to `CommandConverter<TNode>`; `Serialize<TNode>(TLioScript<TNode> script)` using reflection to produce a JSON array (camelCase property names, skip nulls, use `ToScript()` for `IFunctionSupportedValue<TNode>`) — independent of T025/T026; TLioConvert serializes arbitrary scripts and does not depend on builder types
- [x] T028 [US3] Create `TLio.UnitTests/ClientTests/` directory and `TLio.UnitTests/ClientTests/FluentBuilderTests.cs`: inline NUnit tests verifying (a) `new TLioScript<JToken>().Add(JValue.CreateString("hi")).OnPath("$.greeting")` executes correctly, (b) `.Copy().From("$.a").To("$.b")` round-trips through `TLioConvert.Serialize`, (c) a fluent-built script produces the same result as an equivalent JSON script parsed via `TLioConvert.Parse` — requires T025 + T026 + T027

**Checkpoint**: `dotnet test TLio.UnitTests --filter FluentBuilderTests` passes.

---

## Phase 6: User Story 4 — Text Extension Functions (Priority: P2)

**Goal**: `=toLower()`, `=toUpper()`, `=trimStart()`, `=trimEnd()`, `=toString()` work in TLio scripts. `RegisterTextPack<TNode>()` extension method is available.

**Note**: `TLio.Extensions.Text` project already exists with most functions implemented. The remaining work is: (1) add `toString`, (2) add camelCase registration aliases + `toString` registration, (3) add `RegisterTextPack` method alias, (4) create ai-ref files.

**Independent Test**: `dotnet test TLio.Functions.Tests` passes all existing Text fixtures plus the new toString fixture; `RegisterTextPack<JToken>()` compiles and registers functions.

### Implementation

- [x] T029 [P] [US4] Create `TLio.Extensions.Text/ToStringFunction.cs` (named `ToStringFunction` to avoid conflict with `object.ToString()`): `class ToStringFunction<TNode> : TextFunctionBase<TNode>` — evaluate single argument, call `context.NodeAdapter.Serialize(node, false)` for objects/arrays, `TryGetString` for primitives; **must override `public override string FunctionName => "toString";`** because `TextFunctionBase` returns `GetType().Name.ToLowerInvariant()` which would give `"tostringfunction"` — registration in `RegisterTextPack.cs` happens in T030
- [x] T030 [US4] Add camelCase aliases AND `toString` registration in `TLio.Extensions.Text/RegisterTextPack.cs` `RegisterText<TNode>()` method (single edit): add `registrar.Register("toString", () => new ToStringFunction<TNode>())`, `registrar.Register("toLower", () => new ToLower<TNode>())`, `registrar.Register("toUpper", () => new ToUpper<TNode>())`, `registrar.Register("trimStart", () => new TrimStart<TNode>())`, `registrar.Register("trimEnd", () => new TrimEnd<TNode>())` (lowercase versions remain for backwards compatibility) — requires T029
  > Superseded 2026-08-18: the four camelCase alias registrations were removed. `FunctionsProvider` matches names case-insensitively, so `=toUpper(...)` already resolves to the single `toupper` registration.
- [x] T031 [US4] Add `RegisterTextPack<TNode>()` alias method in `TLio.Extensions.Text/RegisterTextPack.cs` (FR-017): `public static IFunctionsProviderRegistrar<TNode> RegisterTextPack<TNode>(this IFunctionsProviderRegistrar<TNode> r) => r.RegisterText<TNode>();` — runs after T030 since both modify RegisterTextPack.cs

### Fixture for toString

- [x] T032 [P] [US4] Create fixture `TLio.Functions.Tests/Fixtures/Text/tostring/01-object-to-string.json`: `{ "input": {"obj":{"a":1,"b":2}}, "script": [{"command":"add","path":"$.str","value":"=toString($.obj)"}], "result": {"obj":{"a":1,"b":2},"str":"{\"a\":1,\"b\":2}"} }` — validates `=toString()` on objects; add `Text_ToString` test method to `TLio.Functions.Tests/Fixtures/ExtensionFixtureTests.cs` — requires T029 + T030

### YAML Adapter Coverage

- [x] T033 [P] [US4] Create `TLio.Yaml.Tests/TextFunctionTests.cs` with an inline NUnit test that: creates `ParseOptions<YamlNode>`, calls `RegisterText<YamlNode>()`, applies a YAML input through a script with `=toLower($.name)` and `=concat($.first,' ',$.last)`, and asserts the outputs are correct — verifies text pack works with the YAML adapter (SC-003 requires at least JSON and YAML adapter coverage)

### AI Reference (12 text function files)

- [x] T034 [P] [US4] Create `docs/ai-ref/functions/Concat.md` — Intent: concatenates all argument strings; Syntax: `=concat(a, b, ...)` (2+ args); Example with `=concat($.first,' ',$.last)` (Article XI) — requires T001
- [x] T035 [P] [US4] Create `docs/ai-ref/functions/ToString.md` — Intent: converts a node to its string representation; Syntax: `=toString(node)` (Article XI) — requires T001
- [x] T036 [P] [US4] Create `docs/ai-ref/functions/Parse.md` — Intent: parses a string into a node; Syntax: `=parse(str)` (Article XI) — requires T001
- [x] T037 [P] [US4] Create `docs/ai-ref/functions/Format.md` — Intent: replaces `{0}` in a template string; Syntax: `=format(template, value)` (Article XI) — requires T001
- [x] T038 [P] [US4] Create `docs/ai-ref/functions/Length.md` — Intent: returns character count; Syntax: `=length(str)` (Article XI) — requires T001
- [x] T039 [P] [US4] Create `docs/ai-ref/functions/Substring.md` — Intent: extracts a portion of a string; Syntax: `=substring(str, start)` or `=substring(str, start, count)` (Article XI) — requires T001
- [x] T040 [P] [US4] Create `docs/ai-ref/functions/Replace.md` — Intent: replaces all occurrences of a substring; Syntax: `=replace(str, old, new)` (Article XI) — requires T001
- [x] T041 [P] [US4] Create `docs/ai-ref/functions/ToLower.md` — Intent: converts string to lowercase; Syntax: `=toLower(str)` (Article XI) — requires T001
- [x] T042 [P] [US4] Create `docs/ai-ref/functions/ToUpper.md` — Intent: converts string to uppercase; Syntax: `=toUpper(str)` (Article XI) — requires T001
- [x] T043 [P] [US4] Create `docs/ai-ref/functions/Trim.md` — Intent: removes leading and trailing whitespace; Syntax: `=trim(str)` (Article XI) — requires T001
- [x] T044 [P] [US4] Create `docs/ai-ref/functions/TrimStart.md` — Intent: removes leading whitespace; Syntax: `=trimStart(str)` (Article XI) — requires T001
- [x] T045 [P] [US4] Create `docs/ai-ref/functions/TrimEnd.md` — Intent: removes trailing whitespace; Syntax: `=trimEnd(str)` (Article XI) — requires T001

**Checkpoint**: `dotnet test TLio.Functions.Tests` all pass. `RegisterTextPack<JToken>()` compiles. `dotnet test TLio.Yaml.Tests` TextFunctionTests pass.

---

## Phase 7: User Story 5 — AI Reference Format Alignment (Priority: P3)

**Goal**: Every `docs/ai-ref/commands/*.md` and `docs/ai-ref/functions/*.md` file includes an **Intent** line, a **Supports functions** flag (commands only), and a **C# Fluent API** section. Property names in Compare.md and Merge.md are updated.

**Independent Test**: Manual review — each command file starts with `> <one-sentence purpose>`, contains `**Supports functions**: ✅/❌`, and has a `## C# Fluent API` section. Function files contain `> <one-sentence purpose>` and a C# usage example.

**Note**: T001 (Merge 007) must be complete before these tasks begin — these tasks update files that 007 creates.

### Command ai-ref updates

Each task: add `> <Intent>` under the `#` heading; add `**Supports functions**: ✅/❌` after the Options table; add `## C# Fluent API` section with a code example at the end.

- [x] T046 [P] [US5] Update `docs/ai-ref/commands/Set.md` — Intent: sets the value at a path; Supports functions: ✅; Fluent: `new TLioScript<TNode>().Set(value).OnPath("$.path")`
- [x] T047 [P] [US5] Update `docs/ai-ref/commands/Add.md` — Intent: adds a property/element if it does not exist; Supports functions: ✅; Fluent: `new TLioScript<TNode>().Add(value).OnPath("$.path")`
- [x] T048 [P] [US5] Update `docs/ai-ref/commands/Put.md` — Intent: adds or replaces a value at a path; Supports functions: ✅; Fluent: `new TLioScript<TNode>().Put(value).OnPath("$.path")`
- [x] T049 [P] [US5] Update `docs/ai-ref/commands/Remove.md` — Intent: removes the node at a path; Supports functions: ❌; Fluent: `new TLioScript<TNode>().Remove().OnPath("$.path")`
- [x] T050 [P] [US5] Update `docs/ai-ref/commands/Copy.md` — Intent: deep-copies a node from one path to another; Supports functions: ❌; Fluent: `new TLioScript<TNode>().Copy().From("$.src").To("$.dst")`
- [x] T051 [P] [US5] Update `docs/ai-ref/commands/Move.md` — Intent: moves a node from one path to another; Supports functions: ❌; Fluent: `new TLioScript<TNode>().Move().From("$.src").To("$.dst")`
- [x] T052 [P] [US5] Update `docs/ai-ref/commands/IfElse.md` — Intent: conditionally executes one of two sub-scripts; Supports functions: ✅ (condition only); Fluent: `new TLioScript<TNode>().IfElse(condition).If(ifScript).Else(elseScript)`
- [x] T053 [P] [US5] Update `docs/ai-ref/commands/Compare.md` — Intent: compares two nodes and writes the result; Supports functions: ❌; Fluent: `new TLioScript<TNode>().Compare().From("$.a").To("$.b").Result("$.r")`; update Options table to show `fromPath`/`toPath` as primary names (aliases: `firstPath`/`secondPath`)
- [x] T054 [P] [US5] Update `docs/ai-ref/commands/Merge.md` — Intent: deep-merges one node into another; Supports functions: ❌; Fluent: `new TLioScript<TNode>().Merge().From("$.src").To("$.dst")`; update Options table to show `fromPath`/`toPath` as primary names (aliases: `path`/`targetPath`)
- [x] T055 [P] [US5] Update `docs/ai-ref/commands/DecisionTable.md` — Intent: evaluates a rule table against node inputs; Supports functions: ✅ (rule results only); Fluent: builder-style (decision table config built in code); note `"decisionTable"` key accepted as alias for `"config"`
- [x] T056 [P] [US5] Update `docs/ai-ref/commands/Flatten.md` — Intent: flattens a nested object to dot-delimited keys; Supports functions: ❌; Fluent: N/A (ETL extension pack); note `"flattenSettings"` key is the correct JSON property name
- [x] T057 [P] [US5] Update `docs/ai-ref/commands/Restore.md` — Intent: restores a flat object to its nested form; Supports functions: ❌; note `"restoreSettings"` key
- [x] T058 [P] [US5] Update `docs/ai-ref/commands/Resolve.md` — Intent: resolves a reference collection into embedded values; Supports functions: ❌; note `"resolveSettings"` key
- [x] T059 [P] [US5] Update `docs/ai-ref/commands/ToCsv.md` — Intent: converts an array of objects to CSV; Supports functions: ❌; note `"csvSettings"` key

### Function ai-ref updates

Function files do not carry a "Supports functions" flag (that concept applies to commands only). Each task: add `> <Intent>` under the `#` heading; update all signatures including new 2-arg forms where applicable; add a C# usage example at the end.

- [x] T060 [P] [US5] Update `docs/ai-ref/functions/Fetch.md` — add Intent line; update Syntax to include `=fetch(path)` and `=fetch(path, defaultValue)` signatures; add C# usage example
- [x] T061 [P] [US5] Update `docs/ai-ref/functions/Indirect.md` — add Intent line and C# usage example
- [x] T062 [P] [US5] Update `docs/ai-ref/functions/Promote.md` — add Intent line; update Syntax to include `=promote(path)` and `=promote(path, propertyName)` signatures; add C# usage example
- [x] T063 [P] [US5] Update `docs/ai-ref/functions/Partial.md` — add Intent line and C# usage example
- [x] T064 [P] [US5] Update `docs/ai-ref/functions/ScriptPath.md` — add Intent line; note `=path()` as registered alias; add C# usage example
- [x] T065 [P] [US5] Update `docs/ai-ref/functions/Datetime.md` — add Intent line and C# usage example

**Checkpoint**: All ai-ref files have Intent line. Command files have Supports-functions flag and C# Fluent API section. Function files have C# usage example. Article XI compliance check returns zero missing files.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [x] T066 Run Article I/IX compliance check: `grep -rn "Newtonsoft\|JToken\|JObject\|XElement\|YamlNode" TLio.Core/ TLio.Commands/ TLio.Functions/ TLio.Extensions.Text/` — must return zero results
- [x] T067 Run Article II compliance check: `grep -rn "new.*Adapter\|new.*Fetcher\|new.*ExecutionContext" TLio.Commands/ TLio.Functions/ TLio.Extensions.Text/` — must return zero results
- [x] T068 Run Article XI compliance check in PowerShell: check `docs/ai-ref/commands/*.md` and `docs/ai-ref/functions/*.md` exist for all `*Command.cs` and `*Function.cs` files — must return zero MISSING lines
- [x] T069 Run full test suite: `dotnet test` — all projects must pass with no regressions (SC-005)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: No dependency on Phase 1 — T002/T003/T004 are sequential (same file); can overlap with Phase 1
- **Phase 3 (US1)**: T005/T006 can start immediately (different files from Phase 2). T007/T008/T009/T010 fixture files can be created at any time (TDD: create failing fixtures first; they pass once T005/T006/T002/T003 are done). T011/T012 need T001.
- **Phase 4 (US2)**: T014/T015/T017 can start in parallel (different files). T016 depends on T014 (same file, single edit for both registrations). T018/T019/T020 fixture files can be created any time (TDD; pass once T015/T016/T017 done).
- **Phase 5 (US3)**: T025 first; T026 after T025; T027 is [P] independent of T025/T026 (no dependency on builder types); T028 needs T025 + T026 + T027.
- **Phase 6 (US4)**: T029 first (new class file); T030 after T029 (adds toString + aliases to RegisterTextPack.cs); T031 after T030 (same file); T032/T033 need T029+T030; T034–T045 all parallel (different ai-ref files, need T001).
- **Phase 7 (US5)**: All need T001 (007 merge). T046–T065 are all parallel.
- **Phase 8 (Polish)**: After all phases complete.

### Within Phase 3 (US1)

- T005/T006 can run in parallel (different files)
- T007/T008/T009/T010 can run in parallel (different files/dirs) — create fixture files any time (TDD)
- T011/T012/T013 can run in parallel (different docs)

### Within Phase 4 (US2)

- T014/T015/T017 can run in parallel (different files)
- T016 after T014 (both touch ParseOptions.cs — single edit for both "newGuid" and "path")
- T018/T019/T020 can run in parallel (different fixture files) — create any time (TDD)
- T021/T022/T023/T024 can run in parallel (different ai-ref files, need T001)

### Within Phase 5 (US3)

- T025 first (defines builder types)
- T026 after T025 (uses builder types from T025)
- T027 is [P] independent — TLioConvert serializes TLioScript directly, no dependency on builder types
- T028 after T025 + T026 + T027

### Within Phase 6 (US4)

- T029 first (new class — override FunctionName → "toString")
- T030 after T029 (adds toString + camelCase aliases to RegisterTextPack.cs)
- T031 after T030 (adds RegisterTextPack alias to same file)
- T032/T033 after T029 + T030
- T034–T045 all parallel (different ai-ref files)

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1: Setup (merge 007)
2. Complete Phase 2: Foundational (CommandConverter)
3. Complete Phase 3: US1 (property aliases)
4. Complete Phase 4: US2 (missing functions)
5. **STOP and VALIDATE**: `dotnet test` — all JLio migration scenarios work

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 (P1) → property aliases → JLio scripts run unchanged
3. US2 (P1) → missing functions → all JLio function scripts work
4. US3 (P2) → fluent API → C# script building works
5. US4 (P2) → text pack complete → all text functions available
6. US5 (P3) → ai-ref updated → AI agent documentation complete

---

## Notes

- `[P]` tasks = different files, no file-write dependencies — can run in parallel
- Fixture files are TDD — create them before implementing; they fail until the implementation tasks complete
- `TLio.Extensions.Text` already exists with most functions; US4 adds `toString` + camelCase aliases + `RegisterTextPack` alias
- Fixture format in both `TLio.UnitTests` and `TLio.Functions.Tests`: single `fixture.json` per scenario dir with `{ "input", "script", "result" }`
- `newGuid` returns a random UUID — only inline tests (not fixture triplets) are appropriate for it
- T001 (merge 007) must be done before T011/T012/T021–T024/T034–T045/T046–T065 since those update files that 007 creates
- `ToStringFunction<TNode>` MUST override `FunctionName => "toString"` — `TextFunctionBase` returns `GetType().Name.ToLowerInvariant()` which would be `"tostringfunction"`
- T016 is a single ParseOptions.cs edit that registers BOTH `"newGuid"` and `"path"` — do not split into two edits
- T027 (TLioConvert) is independent of T025/T026 (builder types) — TLioConvert serializes TLioScript<TNode> which is a plain List<ICommand<TNode>>
- Constitutional compliance checks (T066–T068) MUST pass before marking the feature done
