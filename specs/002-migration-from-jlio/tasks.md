# Tasks 002 — Migration from JLio to TLio

**Plan:** [plan.md](./plan.md)
**Status:** In Progress

Legend: `[P]` = parallelisable · `[!]` = hard requirement (must not be weakened)

---

## ⚠ Hard Requirements — must be checked before any task is marked done

- `[!]` Every ported test uses identical assertions to the JLio original.
- `[!]` No format-specific type in TLio.Core or TLio.Commands.
- `[!]` Newtonsoft and System.Text.Json adapters produce identical results for the same input.

---

## Phase 2A — Complete Newtonsoft JsonNodeAdapter (`TLio.Json`)

> ⚠ **Article VI Correction**: Implementation tasks below were completed before unit tests
> were written (constitution violation). Tests must be written as the **immediate next step**.
> Tests are expected to pass; any failures must be fixed before Phase 3 proceeds.

- [x] [P] Implement `JsonNodeAdapter.RemoveFromParent` — port `JsonMethods.RemoveItemFromTarget` (handles JProperty and JArray cases)
- [x] [P] Implement `JsonNodeAdapter.DeepMergeInto` — port deep-merge from JLio's `CopyMove` root-merge and `Merge` command logic
- [x] [P] Implement `JsonNodeAdapter.GetParentNode` — port semantic-parent logic from `JsonPathItemsFetcher.NavigateToParent` (skips JProperty, JArray intermediaries)
- [x] [P] Implement `JsonNodeAdapter.GetParentPropertyName` — return property name under which node lives in parent JObject
- [x] [P] Implement `JsonNodeAdapter.TryGetBoolean` — coerce JToken to bool? (handles JValue only)
- [x] [P] Implement `JsonNodeAdapter.TryGetDouble` — coerce JToken to double? (handles numeric JValue types)
- [x] [P] Implement `JsonNodeAdapter.TryGetString` — coerce JToken to string? (handles all JValue types via JToken.ToString)
- [x] [P] Implement `JsonNodeAdapter.DeepEquals` — use `JToken.DeepEquals()`
- [x] Write `JsonNodeAdapterTests` — cover all INodeAdapter members for object, array, primitive, null nodes
- [x] `[!]` All `JsonNodeAdapterTests` pass

---

## Phase 2B — Complete Newtonsoft JsonPathItemsFetcher (`TLio.Json`)

> ⚠ **Article VI Correction**: Implementation tasks below were completed before unit tests
> were written (constitution violation). Tests must be written as the **immediate next step**.
> Tests are expected to pass; any failures must be fixed before Phase 3 proceeds.

- [x] [P] Implement `JsonPathItemsFetcher.GetParent` — port `NavigateToParent` including semantic-parent level counting (skips JProperty/JArray wrappers)
- [x] [P] Implement `JsonPathItemsFetcher.ResolveRelativePath` — port `@` and `<--` resolution from JLio
- [x] [P] Implement `JsonPathItemsFetcher.EnsurePath` — port `JsonMethods.CheckOrCreateParentPath` (walk path, create missing JObject nodes)
- [x] [P] Implement `JsonPathItemsFetcher.SplitParentAndLeaf` — port `JsonSplittedPath` / `JsonPathMethods.SplitPath` (respects bracket nesting, handles `[*]` etc.)
- [x] [P] Implement `JsonPathItemsFetcher.ProcessIndirectPath` — port regex-based `=indirect(path)` substitution from JLio
- [x] [P] Implement `JsonPathItemsFetcher.GetIntellisense` — port property suggestion logic from JLio
- [x] Write `JsonPathFetcherTests` — cover root, nested, array-index, recursive-descent, wildcard, filter, parent-navigation, indirect paths
- [x] `[!]` All `JsonPathFetcherTests` pass; results match JLio's existing `JsonPathMethodsTests`

---

## Phase 3 — Commands implementation (Newtonsoft)

All command tasks below have this invariant: **no `JToken`, `JObject`, or `JArray`
reference in the command body — all node operations via INodeAdapter/IItemsFetcher.**

### PropertyChangeCommand<TNode> (base for Add/Set/Put)

- [x] Complete `PropertyChangeCommand.ExecuteNewSyntax` — new-syntax loop using INodeAdapter
- [x] Complete `PropertyChangeCommand.ExecuteLegacySyntax` — legacy path-split + EnsurePath + loop
- [x] Complete `PropertyChangeCommand.ReplaceProperty` — full implementation using INodeAdapter
- [x] Write `PropertyChangeCommandBaseTests` — legacy vs new syntax, missing path, type mismatches

### Add<TNode>

- [x] `[!]` Port all `JLio.UnitTests.CommandsTests.AddTests` → `TLio.UnitTests.CommandsTests.AddTests` with identical assertions
- [ ] `[!]` Port `PropertyFieldBackwardsCompatibilityTests` for Add
- [x] Run ported Add tests; all pass

### Set<TNode>

- [x] `[!]` Port all `SetTests` → `TLio.UnitTests.CommandsTests.SetTests` with identical assertions
- [x] Run ported Set tests; all pass

### Put<TNode>

- [x] `[!]` Port all `PutTests` → `TLio.UnitTests.CommandsTests.PutTests` with identical assertions
- [x] Run ported Put tests; all pass

### Remove<TNode>

- [x] `[!]` Port all `RemoveTests` → `TLio.UnitTests.CommandsTests.RemoveTests` with identical assertions
- [x] Run ported Remove tests; all pass

### CopyMoveBase<TNode> (base for Copy/Move)

- [x] Complete `CopyMoveBase` array-index alignment logic — port `GetInnerArrayIndex` from JLio
- [x] Complete `CopyMoveBase` many-to-many vs one-to-one dispatch
- [ ] Write `CopyMoveBaseTests` — alignment, root merge, indirect path, DestinationAsArray

### Copy<TNode>

- [x] `[!]` Port `CopyMoveTests` (copy half) with identical assertions
- [x] `[!]` Port `CopyMoveDestinationAsArrayTests` (copy half)
- [x] Run ported Copy tests; all pass

### Move<TNode>

- [x] `[!]` Port `CopyMoveTests` (move half) with identical assertions
- [x] `[!]` Port `CopyMoveDestinationAsArrayTests` (move half)
- [ ] `[!]` Port `ParentNavigationTests` with identical assertions — deferred (requires ETL/Math/Text extensions)
- [x] Run ported Move tests; all pass

### IfElse<TNode>

- [x] Create `TLio.Commands/IfElse.cs` — port from JLio.Commands.IfElse (uses INodeAdapter.DeepEquals for First==Second comparison)
- [x] `[!]` Port `IfElseTests` with identical assertions
- [x] Run ported IfElse tests; all pass

### Compare<TNode>

- [x] Create `TLio.Commands/Advanced/Compare.cs` — port from JLio
- [x] `[!]` Port `CompareTests` with identical assertions
- [x] Run ported Compare tests; all pass

### Merge<TNode>

- [x] Create `TLio.Commands/Advanced/Merge.cs` — port from JLio (uses INodeAdapter.DeepMergeInto)
- [x] `[!]` Port `MergeTests` with identical assertions
- [x] Run ported Merge tests; all pass

### DecisionTable<TNode> (complex — split into sub-tasks)

- [x] Create `TLio.Commands/DecisionTable.cs` + `DecisionTableConfig` models — port type hierarchy from JLio (all models are format-agnostic)
- [x] Implement condition evaluation (operators: =, !=, >, <, >=, <=, &&, ||, array membership) using `INodeAdapter.TryGetDouble/TryGetBoolean/TryGetString`
- [x] Implement `firstMatch` execution strategy
- [x] Implement `bestMatch` execution strategy
- [x] Implement `allMatches` execution strategy
- [x] Implement conflict resolution: priority, merge, lastWins
- [x] `[!]` Port `DecisionTableTests` with identical assertions
- [x] `[!]` Port `DecisionTableAdvancedTests` with identical assertions
- [x] `[!]` Port `DecisionTableBuilderTests` with identical assertions
- [x] `[!]` Port `DecisionTableJsonParseTests` with identical assertions
- [x] Run all ported DecisionTable tests; all must pass

---

## Phase 4 — Functions & value pipeline

### Phase 4B — Value pipeline (prerequisite for all function tests)

- [x] Create `TLio.Core/Models/FunctionSupportedValue.cs` — wraps `IFunction<TNode>`, handles logging, mirrors JLio's `FunctionSupportedValue`
- [ ] Update `FixedValue<TNode>` — add support for nested `=func()` expansion via `FunctionConverter<TNode>` injection
- [x] Create `TLio.Core/Models/PathValue.cs` — `IFunctionSupportedValue<TNode>` that evaluates a path expression (replaces JLio's path-as-value pattern)
- [ ] Write `ValuePipelineTests` — FixedValue with literal, string, object, array; PathValue; nested FunctionSupportedValue

### Phase 4A — Core functions (`TLio.Functions`)

- [x] [P] Port `Fetch<TNode>` — evaluate path arg, return first match or default; identical to JLio
- [x] [P] Port `Indirect<TNode>` — path-to-path resolution
- [x] [P] Port `Promote<TNode>` — wrap node in new object property
- [x] [P] Port `Partial<TNode>` — filter object to named properties only
- [x] [P] Port `ScriptPath<TNode>` — return current node's path string; support relative `@.<--` args
- [x] [P] Port `Datetime<TNode>` — format current datetime; same time selection tokens as JLio
- [x] `[!]` Port and run `FetchTests`, `IndirectTests`, `PartialTests`, `PromoteTests`, `ScriptPathTests`, `DatetimeFunctionTests` with identical assertions

### Phase 4C — Math functions (`TLio.Extensions.Math`)

- [x] Create `TLio.Extensions.Math` project referencing only TLio.Core
- [x] [P] Port all 25+ math functions (Sum, Avg, Count, Min, Max, Median, Ceiling, Floor, Round, Sqrt, Pow, Abs, Subtract, Modulo, Calculate, SumIf, SumIfs, CountIf, CountIfs, AverageIf, AverageIfs, MinIfs, MaxIfs)
- [x] `[!]` Port and run all math test files (AvgTests, SumTests, CountTests, etc.) with identical assertions
- [x] `[!]` Port `MathIntegerOutputTests` — integer vs double output must match exactly
- [x] `[!]` Port `MathNullHandlingTests` — null argument handling must match exactly

### Phase 4D — Text functions (`TLio.Extensions.Text`)

- [x] Create `TLio.Extensions.Text` project referencing only TLio.Core
- [x] [P] Port all 25+ text functions (Concat, Length, Substring, ToUpper, ToLower, Trim, TrimStart, TrimEnd, StartsWith, EndsWith, Contains, Replace, Split, Join, IndexOf, Format, Parse, PadLeft, PadRight, NewGuid, IsEmpty)
- [x] `[!]` Port and run all text test files with identical assertions

### Phase 4E — TimeDate functions (`TLio.Extensions.TimeDate`)

- [ ] Create `TLio.Extensions.TimeDate` project referencing only TLio.Core
- [ ] [P] Port DateCompare, IsDateBetween, MinDate, MaxDate, AvgDate
- [ ] `[!]` Port and run `TimeDateFunctionTests` with identical assertions

### Phase 4F — ETL commands (`TLio.Extensions.ETL`)

- [ ] Create `TLio.Extensions.ETL` project referencing TLio.Core + TLio.Commands
- [ ] Port Flatten, Restore, Resolve, ToCsv (all use INodeAdapter for traversal)
- [ ] `[!]` Port and run `FlattenRestoreTests`, `ResolveTests` with identical assertions

---

## Phase 5 — Script parser (`TLio.Client`)

- [x] Create `FunctionConverter<TNode>` — parse `=funcName(arg1, arg2, ...)` strings; port `SplitText.GetChoppedElements` for delimiter-aware splitting
- [x] Create `CommandConverter<TNode>` — JSON discriminator deserialization using `ICommandsProvider<TNode>`
- [x] Create `ParseOptions<TNode>` — register all built-in commands and functions; `CreateDefault()` factory; fluent `RegisterCommand/RegisterFunction`
- [x] Implement `ScriptEngine<TNode>.Execute(string scriptJson, TNode data, IExecutionContext<TNode>)` — parse + execute
- [ ] `[!]` Port `JLioEngineTests`, `JLioEngineIntegrationTests`, `JLioEngineConfigurationTests` with identical assertions
- [ ] `[!]` Port `TextHandlingTests` (ScriptTextHandling) — script parse edge cases
- [ ] `[!]` Port `PathTests`, `JsonPathMethodsTests`, `JsonPathMethodsEdgeCasesTests`
- [ ] Run all engine tests; all must pass

---

## Phase 5B — Refactor inline tests to fixture triplets (Article VI compliance)

> ⚠ **Constitution §VI violation**: All current command and function tests use inline
> `[TestCase]` / hardcoded data instead of file-based fixture triplets. This must be
> corrected before Phase 7 (adapter compliance tests) can reuse the same fixtures.

- [ ] Define shared `FixtureTheoryLoader` helper — loads all `(input.json, script.json, result.json)` triplets from a folder and supplies them as xUnit `[MemberData]`
- [ ] Refactor `CommandsTests/AddTests.cs` → fixture triplets under `Fixtures/Add/`
- [ ] Refactor `CommandsTests/SetTests.cs` → fixture triplets under `Fixtures/Set/`
- [ ] Refactor `CommandsTests/PutTests.cs` → fixture triplets under `Fixtures/Put/`
- [ ] Refactor `CommandsTests/RemoveTests.cs` → fixture triplets under `Fixtures/Remove/`
- [ ] Refactor `CommandsTests/CopyMoveTests.cs` → fixture triplets under `Fixtures/CopyMove/`
- [ ] Refactor `CommandsTests/CopyMoveDestinationAsArrayTests.cs` → fixture triplets under `Fixtures/CopyMoveDestinationAsArray/`
- [ ] Refactor `CommandsTests/IfElseTests.cs` → fixture triplets under `Fixtures/IfElse/`
- [ ] Refactor `CommandsTests/CompareTests.cs` → fixture triplets under `Fixtures/Compare/`
- [ ] Refactor `CommandsTests/MergeTests.cs` → fixture triplets under `Fixtures/Merge/`
- [ ] Refactor `FunctionsTests/FetchTests.cs` → fixture triplets under `Fixtures/Fetch/`
- [ ] Refactor `FunctionsTests/IndirectTests.cs` → fixture triplets under `Fixtures/Indirect/`
- [ ] Refactor `FunctionsTests/PartialTests.cs` → fixture triplets under `Fixtures/Partial/`
- [ ] Refactor `FunctionsTests/PromoteTests.cs` → fixture triplets under `Fixtures/Promote/`
- [ ] Refactor `FunctionsTests/ScriptPathTests.cs` → fixture triplets under `Fixtures/ScriptPath/`
- [ ] Refactor `FunctionsTests/DatetimeFunctionTests.cs` → fixture triplets under `Fixtures/Datetime/`
- [ ] `[!]` All refactored tests pass with zero assertion changes

---

## Phase 6 — Porting guide and reference tests

- [ ] Write `specs/002-migration-from-jlio/porting-guide.md` — exact substitution table (JLio type → TLio type)
- [ ] Commit `TLio.UnitTests/CommandsTests/SetTests.cs` as the reference ported test file
- [ ] Verify ported test count matches JLio test count (73 test files)

---

## Phase 7 — System.Text.Json adapter (`TLio.Json.SystemText`)

- [ ] Implement `SystemTextJsonNodeAdapter.Replace` — find key in parent JsonObject/JsonArray and swap
- [ ] Implement `SystemTextJsonNodeAdapter.RemoveFromParent` — remove self from parent
- [ ] Implement `SystemTextJsonNodeAdapter.DeepMergeInto` — recursive merge
- [ ] Implement `SystemTextJsonNodeAdapter.GetParentPropertyName`
- [ ] Implement `SystemTextJsonPathItemsFetcher.GetPath` — build path string from node lineage
- [ ] Implement `SystemTextJsonPathItemsFetcher.ResolveRelativePath`
- [ ] Implement `SystemTextJsonPathItemsFetcher.EnsurePath`
- [ ] Implement `SystemTextJsonPathItemsFetcher.SplitParentAndLeaf`
- [ ] Implement `SystemTextJsonPathItemsFetcher.ProcessIndirectPath`
- [ ] Create `JsonAdapterComplianceTestBase<TNode>` — shared abstract test class; concrete subclasses supply the execution context
- [ ] `[!]` Run all ported command + function tests against `SystemTextJsonExecutionContext.CreateDefault()` — all must pass
- [ ] Document any `JsonCons.JsonPath` vs Newtonsoft deviations in `specs/002-migration-from-jlio/jsonpath-compatibility.md`

---

## Continuous validation

After each phase, run this checklist:
- [ ] `grep -r "JToken\|XElement\|YamlNode\|Newtonsoft\|System\.Xml\|YamlDotNet" TLio.Core TLio.Commands TLio.Functions` returns no results (constitutional check)
- [ ] All tests in `TLio.UnitTests` that are marked complete pass with no assertion changes
- [ ] No test previously passing has been broken
