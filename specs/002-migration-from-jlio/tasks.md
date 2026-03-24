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

- [x] [P] Implement `JsonNodeAdapter.RemoveFromParent` — port `JsonMethods.RemoveItemFromTarget` (handles JProperty and JArray cases)
- [x] [P] Implement `JsonNodeAdapter.DeepMergeInto` — port deep-merge from JLio's `CopyMove` root-merge and `Merge` command logic
- [x] [P] Implement `JsonNodeAdapter.GetParentNode` — port semantic-parent logic from `JsonPathItemsFetcher.NavigateToParent` (skips JProperty, JArray intermediaries)
- [x] [P] Implement `JsonNodeAdapter.GetParentPropertyName` — return property name under which node lives in parent JObject
- [x] [P] Implement `JsonNodeAdapter.TryGetBoolean` — coerce JToken to bool? (handles JValue only)
- [x] [P] Implement `JsonNodeAdapter.TryGetDouble` — coerce JToken to double? (handles numeric JValue types)
- [x] [P] Implement `JsonNodeAdapter.TryGetString` — coerce JToken to string? (handles all JValue types via JToken.ToString)
- [x] [P] Implement `JsonNodeAdapter.DeepEquals` — use `JToken.DeepEquals()`
- [ ] Write `JsonNodeAdapterTests` — cover all INodeAdapter members for object, array, primitive, null nodes
- [ ] `[!]` All `JsonNodeAdapterTests` pass

---

## Phase 2B — Complete Newtonsoft JsonPathItemsFetcher (`TLio.Json`)

- [x] [P] Implement `JsonPathItemsFetcher.GetParent` — port `NavigateToParent` including semantic-parent level counting (skips JProperty/JArray wrappers)
- [x] [P] Implement `JsonPathItemsFetcher.ResolveRelativePath` — port `@` and `<--` resolution from JLio
- [x] [P] Implement `JsonPathItemsFetcher.EnsurePath` — port `JsonMethods.CheckOrCreateParentPath` (walk path, create missing JObject nodes)
- [x] [P] Implement `JsonPathItemsFetcher.SplitParentAndLeaf` — port `JsonSplittedPath` / `JsonPathMethods.SplitPath` (respects bracket nesting, handles `[*]` etc.)
- [x] [P] Implement `JsonPathItemsFetcher.ProcessIndirectPath` — port regex-based `=indirect(path)` substitution from JLio
- [x] [P] Implement `JsonPathItemsFetcher.GetIntellisense` — port property suggestion logic from JLio
- [ ] Write `JsonPathFetcherTests` — cover root, nested, array-index, recursive-descent, wildcard, filter, parent-navigation, indirect paths
- [ ] `[!]` All `JsonPathFetcherTests` pass; results match JLio's existing `JsonPathMethodsTests`

---

## Phase 3 — Commands implementation (Newtonsoft)

All command tasks below have this invariant: **no `JToken`, `JObject`, or `JArray`
reference in the command body — all node operations via INodeAdapter/IItemsFetcher.**

### PropertyChangeCommand<TNode> (base for Add/Set/Put)

- [ ] Complete `PropertyChangeCommand.ExecuteNewSyntax` — new-syntax loop using INodeAdapter
- [ ] Complete `PropertyChangeCommand.ExecuteLegacySyntax` — legacy path-split + EnsurePath + loop
- [ ] Complete `PropertyChangeCommand.ReplaceProperty` — full implementation using INodeAdapter
- [ ] Write `PropertyChangeCommandBaseTests` — legacy vs new syntax, missing path, type mismatches

### Add<TNode>

- [ ] `[!]` Port all `JLio.UnitTests.CommandsTests.AddTests` → `TLio.UnitTests.CommandsTests.AddTests` with identical assertions
- [ ] `[!]` Port `PropertyFieldBackwardsCompatibilityTests` for Add
- [ ] Run ported Add tests; all must pass

### Set<TNode>

- [ ] `[!]` Port all `SetTests` → `TLio.UnitTests.CommandsTests.SetTests` with identical assertions
- [ ] Run ported Set tests; all must pass

### Put<TNode>

- [ ] `[!]` Port all `PutTests` → `TLio.UnitTests.CommandsTests.PutTests` with identical assertions
- [ ] Run ported Put tests; all must pass

### Remove<TNode>

- [ ] `[!]` Port all `RemoveTests` → `TLio.UnitTests.CommandsTests.RemoveTests` with identical assertions
- [ ] Run ported Remove tests; all must pass

### CopyMoveBase<TNode> (base for Copy/Move)

- [ ] Complete `CopyMoveBase` array-index alignment logic — port `GetInnerArrayIndex` from JLio
- [ ] Complete `CopyMoveBase` many-to-many vs one-to-one dispatch
- [ ] Write `CopyMoveBaseTests` — alignment, root merge, indirect path, DestinationAsArray

### Copy<TNode>

- [ ] `[!]` Port `CopyMoveTests` (copy half) with identical assertions
- [ ] `[!]` Port `CopyMoveDestinationAsArrayTests` (copy half)
- [ ] Run ported Copy tests; all must pass

### Move<TNode>

- [ ] `[!]` Port `CopyMoveTests` (move half) with identical assertions
- [ ] `[!]` Port `CopyMoveDestinationAsArrayTests` (move half)
- [ ] `[!]` Port `ParentNavigationTests` with identical assertions
- [ ] Run ported Move tests; all must pass

### IfElse<TNode>

- [ ] Create `TLio.Commands/IfElse.cs` — port from JLio.Commands.IfElse (uses INodeAdapter.DeepEquals for First==Second comparison)
- [ ] `[!]` Port `IfElseTests` with identical assertions
- [ ] Run ported IfElse tests; all must pass

### Compare<TNode>

- [ ] Create `TLio.Commands/Advanced/Compare.cs` — port from JLio
- [ ] `[!]` Port `CompareTests` with identical assertions
- [ ] Run ported Compare tests; all must pass

### Merge<TNode>

- [ ] Create `TLio.Commands/Advanced/Merge.cs` — port from JLio (uses INodeAdapter.DeepMergeInto)
- [ ] `[!]` Port `MergeTests` with identical assertions
- [ ] Run ported Merge tests; all must pass

### DecisionTable<TNode> (complex — split into sub-tasks)

- [ ] Create `TLio.Commands/DecisionTable.cs` + `DecisionTableConfig` models — port type hierarchy from JLio (all models are format-agnostic)
- [ ] Implement condition evaluation (operators: =, !=, >, <, >=, <=, &&, ||, array membership) using `INodeAdapter.TryGetDouble/TryGetBoolean/TryGetString`
- [ ] Implement `firstMatch` execution strategy
- [ ] Implement `bestMatch` execution strategy
- [ ] Implement `allMatches` execution strategy
- [ ] Implement conflict resolution: priority, merge, lastWins
- [ ] `[!]` Port `DecisionTableTests` with identical assertions
- [ ] `[!]` Port `DecisionTableAdvancedTests` with identical assertions
- [ ] `[!]` Port `DecisionTableBuilderTests` with identical assertions
- [ ] `[!]` Port `DecisionTableJsonParseTests` with identical assertions
- [ ] Run all ported DecisionTable tests; all must pass

---

## Phase 4 — Functions & value pipeline

### Phase 4B — Value pipeline (prerequisite for all function tests)

- [ ] Create `TLio.Core/Models/FunctionSupportedValue.cs` — wraps `IFunction<TNode>`, handles logging, mirrors JLio's `FunctionSupportedValue`
- [ ] Update `FixedValue<TNode>` — add support for nested `=func()` expansion via `FunctionConverter<TNode>` injection
- [ ] Create `TLio.Core/Models/PathValue.cs` — `IFunctionSupportedValue<TNode>` that evaluates a path expression (replaces JLio's path-as-value pattern)
- [ ] Write `ValuePipelineTests` — FixedValue with literal, string, object, array; PathValue; nested FunctionSupportedValue

### Phase 4A — Core functions (`TLio.Functions`)

- [ ] [P] Port `Fetch<TNode>` — evaluate path arg, return first match or default; identical to JLio
- [ ] [P] Port `Indirect<TNode>` — path-to-path resolution
- [ ] [P] Port `Promote<TNode>` — wrap node in new object property
- [ ] [P] Port `Partial<TNode>` — filter object to named properties only
- [ ] [P] Port `ScriptPath<TNode>` — return current node's path string; support relative `@.<--` args
- [ ] [P] Port `Datetime<TNode>` — format current datetime; same time selection tokens as JLio
- [ ] `[!]` Port and run `FetchTests`, `FetchBuildersTests`, `IndirectTests`, `PartialTests`, `PromoteTests`, `ScriptPathTests`, `DatetimeFunctionTests` with identical assertions

### Phase 4C — Math functions (`TLio.Extensions.Math`)

- [ ] Create `TLio.Extensions.Math` project referencing only TLio.Core
- [ ] [P] Port all 25+ math functions (Sum, Avg, Count, Min, Max, Median, Ceiling, Floor, Round, Sqrt, Pow, Abs, Subtract, Modulo, Calculate, SumIf, SumIfs, CountIf, CountIfs, AverageIf, AverageIfs, MinIfs, MaxIfs)
- [ ] `[!]` Port and run all math test files (AvgTests, SumTests, CountTests, etc.) with identical assertions
- [ ] `[!]` Port `MathIntegerOutputTests` — integer vs double output must match exactly
- [ ] `[!]` Port `MathNullHandlingTests` — null argument handling must match exactly

### Phase 4D — Text functions (`TLio.Extensions.Text`)

- [ ] Create `TLio.Extensions.Text` project referencing only TLio.Core
- [ ] [P] Port all 25+ text functions (Concat, Length, Substring, ToUpper, ToLower, Trim, TrimStart, TrimEnd, StartsWith, EndsWith, Contains, Replace, Split, Join, IndexOf, Format, Parse, PadLeft, PadRight, NewGuid, IsEmpty)
- [ ] `[!]` Port and run all text test files with identical assertions

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

- [ ] Create `FunctionConverter<TNode>` — parse `=funcName(arg1, arg2, ...)` strings; port `SplitText.GetChoppedElements` for delimiter-aware splitting
- [ ] Create `CommandConverter<TNode>` — JSON discriminator deserialization using `ICommandsProvider<TNode>`
- [ ] Create `ParseOptions<TNode>` — register all built-in commands and functions; `CreateDefault()` factory; fluent `RegisterCommand/RegisterFunction`
- [ ] Implement `ScriptEngine<TNode>.Execute(string scriptJson, TNode data, IExecutionContext<TNode>)` — parse + execute
- [ ] `[!]` Port `JLioEngineTests`, `JLioEngineIntegrationTests`, `JLioEngineConfigurationTests` with identical assertions
- [ ] `[!]` Port `TextHandlingTests` (ScriptTextHandling) — script parse edge cases
- [ ] `[!]` Port `PathTests`, `JsonPathMethodsTests`, `JsonPathMethodsEdgeCasesTests`
- [ ] Run all engine tests; all must pass

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
