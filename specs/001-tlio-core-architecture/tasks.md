# Tasks 001 — TLio Core Architecture

**Plan:** [plan.md](./plan.md)
**Status:** In Progress (Phases 1–3 & 5–7 complete; Phase 4 near-complete)

Legend: `[P]` = parallelisable with other `[P]` tasks in the same phase

---

## Phase 1 — Core contracts ✅ DONE

- [x] Create `TLio.Core` project + `Directory.Build.props`
- [x] Define `ICommand<TNode>`
- [x] Define `IFunction<TNode>`
- [x] Define `IFunctionSupportedValue<TNode>`
- [x] Define `IItemsFetcher<TNode>`
- [x] Define `INodeAdapter<TNode>`
- [x] Define `IExecutionContext<TNode>`
- [x] Define `IExecutionLogger`
- [x] Define `ICommandsProvider<TNode>` + registrar
- [x] Define `IFunctionsProvider<TNode>` + registrar
- [x] Implement `TLioScript<TNode>`
- [x] Implement `TLioExecutionResult<TNode>`
- [x] Implement `FunctionResult<TNode>`
- [x] Implement `SelectedNodes<TNode>`
- [x] Implement `ExecutionContext<TNode>`
- [x] Implement `ExecutionLogger`
- [x] Implement `CommandBase<TNode>`
- [x] Implement `FunctionBase<TNode>`
- [x] Implement `FixedValue<TNode>`
- [x] Implement `Arguments<TNode>`
- [x] Implement `ValidationResult`
- [x] Stub `Set<TNode>`, `Add<TNode>`, `Remove<TNode>`, `Copy<TNode>`, `Move<TNode>`, `Put<TNode>`
- [x] Write `ArchitectureTests.cs` (smoke tests, currently passing)

---

## Phase 2 — JSON adapter ✅ DONE

- [x] [P] Complete `JsonNodeAdapter.GetProperty` — decide attribute-vs-element-vs-property semantics
- [x] [P] Complete `JsonPathItemsFetcher.GetParent` — port parent-navigation logic from JLio
- [x] [P] Complete `JsonPathItemsFetcher.ResolveRelativePath` — port `@` and `<--` syntax from JLio
- [x] [P] Add `=indirect()` path function to `JsonPathItemsFetcher`
- [x] Write `JsonAdapterTests` — cover all `INodeAdapter` members for object, array, primitive, null nodes
- [x] Write `JsonPathFetcherTests` — cover root, nested, array-index, recursive-descent, parent-navigation paths

---

## Phase 3 — Commands implementation ✅ DONE

- [x] [P] Implement `Set<TNode>.Execute` — select targets via `ItemsFetcher`; replace value via `NodeAdapter`
- [x] [P] Implement `Add<TNode>.Execute` — add property to object or element to array
- [x] [P] Implement `Remove<TNode>.Execute` — detach matched nodes from parent
- [x] [P] Implement `Copy<TNode>.Execute` — deep-clone source and write to destination path
- [x] [P] Implement `Move<TNode>.Execute` — copy then remove source
- [x] [P] Implement `Put<TNode>.Execute` — upsert: set if exists, add if not
- [x] Write `SetCommandTests` — port JLio SetTests, running against JSON adapter
- [x] Write `AddCommandTests` — port JLio AddTests
- [x] Write `RemoveCommandTests` — port JLio RemoveTests
- [x] Write `CopyCommandTests` — port JLio CopyMoveTests (copy half)
- [x] Write `MoveCommandTests` — port JLio CopyMoveTests (move half)
- [x] Write `PutCommandTests`
- [x] Confirm all JLio backward-compat test cases pass

---

## Phase 4 — Functions

- [x] [P] Implement `PathValue<TNode>` — resolves a path expression as a function argument
- [x] [P] Implement `concat(a, b, ...)` function
- [x] [P] Implement `toUpper(s)` function
- [x] [P] Implement `toLower(s)` function
- [x] [P] Implement `now()` function — implemented as `datetime(format?)` in `TLio.Functions`
- [ ] [P] Implement `typeOf(node)` function (returns type name as string node) — not present in JLio migration; pending decision
- [x] [P] Implement `count(path)` function (returns integer node) — implemented in `TLio.Extensions.Math`
- [x] Write unit tests for all functions

---

## Phase 5 — Script parser ✅ DONE

- [x] Design script serialisation format (JSON envelope, backward-compat with JLio)
- [x] Implement `ScriptEngine<TNode>.Execute(string scriptText, ...)` — JSON deserialise to `TLioScript<TNode>`
- [x] Implement `CommandConverter` — resolves command name → `ICommand<TNode>` factory
- [x] Implement `FunctionConverter` — resolves function name in value expressions
- [x] Write `ScriptParserTests` — round-trip parse → execute → serialise for each built-in command
- [x] Write `ScriptTextHandlingTests` — invalid script text, unknown command names, missing fields

---

## Phase 6 — XML adapter ✅ DONE

- [x] Complete `XmlNodeAdapter.GetProperty` — element child semantics
- [x] Complete `XmlNodeAdapter.SetProperty`
- [x] Complete `XmlNodeAdapter.RemoveProperty`
- [x] Complete `XmlNodeAdapter.GetValue<T>`
- [x] Complete `XmlNodeAdapter.DeepClone`
- [x] Complete `XmlNodeAdapter.Replace`
- [x] Complete `XPathItemsFetcher.GetPath` — build full XPath from element lineage
- [x] Complete `XPathItemsFetcher.ResolveRelativePath`
- [x] Add `XmlExecutionContext.CreateDefault()` factory
- [x] Write `XmlAdapterTests` (`XmlNodeAdapterTests.cs`, `XmlPathFetcherTests.cs`)
- [x] Write `XmlCommandIntegrationTests` — Set + Add + Remove against a real XElement tree

---

## Phase 7 — YAML adapter ✅ DONE

- [x] Complete `YamlNodeAdapter.InsertIntoArray`
- [x] Complete `YamlNodeAdapter.RemoveFromArray`
- [x] Complete `YamlNodeAdapter.GetValue<T>`
- [x] Complete `YamlNodeAdapter.DeepClone`
- [x] Complete `YamlNodeAdapter.Replace` — implemented via shared `YamlParentTracker`
- [x] Complete `YamlPathItemsFetcher.SelectNodes` — dot-notation path traversal
- [x] Complete `YamlPathItemsFetcher.SelectNode`
- [x] Complete `YamlPathItemsFetcher.GetPath`
- [x] Complete `YamlPathItemsFetcher.GetParent`
- [x] Add `YamlExecutionContext.CreateDefault()` factory
- [x] Write `YamlAdapterTests` (`YamlNodeAdapterTests.cs`, `YamlPathFetcherTests.cs`)
- [x] Write `YamlCommandIntegrationTests` — Set + Add + Remove against a real YamlMappingNode

---

## Future Specs (not in scope here)

- `002-script-parser` — JSON-envelope script text format
- `003-xml-script-syntax` — whether XML scripts can be expressed as XML documents
- `004-extended-commands` — IfElse, DecisionTable, Merge for TLio
- `005-etl-extension` — Flatten/Restore for TLio
- `006-tlio-client-nuget` — NuGet packaging and versioning strategy
