# Tasks 001 — TLio Core Architecture

**Plan:** [plan.md](./plan.md)
**Status:** In Progress

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

## Phase 2 — JSON adapter

- [ ] [P] Complete `JsonNodeAdapter.GetProperty` — decide attribute-vs-element-vs-property semantics
- [ ] [P] Complete `JsonPathItemsFetcher.GetParent` — port parent-navigation logic from JLio
- [ ] [P] Complete `JsonPathItemsFetcher.ResolveRelativePath` — port `@` and `<--` syntax from JLio
- [ ] [P] Add `=indirect()` path function to `JsonPathItemsFetcher`
- [ ] Write `JsonAdapterTests` — cover all `INodeAdapter` members for object, array, primitive, null nodes
- [ ] Write `JsonPathFetcherTests` — cover root, nested, array-index, recursive-descent, parent-navigation paths

---

## Phase 3 — Commands implementation

- [ ] [P] Implement `Set<TNode>.Execute` — select targets via `ItemsFetcher`; replace value via `NodeAdapter`
- [ ] [P] Implement `Add<TNode>.Execute` — add property to object or element to array
- [ ] [P] Implement `Remove<TNode>.Execute` — detach matched nodes from parent
- [ ] [P] Implement `Copy<TNode>.Execute` — deep-clone source and write to destination path
- [ ] [P] Implement `Move<TNode>.Execute` — copy then remove source
- [ ] [P] Implement `Put<TNode>.Execute` — upsert: set if exists, add if not
- [ ] Write `SetCommandTests` — port JLio SetTests, running against JSON adapter
- [ ] Write `AddCommandTests` — port JLio AddTests
- [ ] Write `RemoveCommandTests` — port JLio RemoveTests
- [ ] Write `CopyCommandTests` — port JLio CopyMoveTests (copy half)
- [ ] Write `MoveCommandTests` — port JLio CopyMoveTests (move half)
- [ ] Write `PutCommandTests`
- [ ] Confirm all JLio backward-compat test cases pass

---

## Phase 4 — Functions

- [ ] [P] Implement `PathValue<TNode>` — resolves a path expression as a function argument
- [ ] [P] Implement `concat(a, b, ...)` function
- [ ] [P] Implement `toUpper(s)` function
- [ ] [P] Implement `toLower(s)` function
- [ ] [P] Implement `now()` function (returns ISO-8601 timestamp as string node)
- [ ] [P] Implement `typeOf(node)` function (returns type name as string node)
- [ ] [P] Implement `count(path)` function (returns integer node)
- [ ] Write unit tests for all functions

---

## Phase 5 — Script parser

- [ ] Design script serialisation format (JSON envelope, backward-compat with JLio)
  - Document in `specs/002-script-parser/spec.md` before starting implementation
- [ ] Implement `ScriptEngine<TNode>.Execute(string scriptText, ...)` — JSON deserialise to `TLioScript<TNode>`
- [ ] Implement `CommandConverter` — resolves command name → `ICommand<TNode>` factory
- [ ] Implement `FunctionConverter` — resolves function name in value expressions
- [ ] Write `ScriptParserTests` — round-trip parse → execute → serialise for each built-in command
- [ ] Write `ScriptTextHandlingTests` — invalid script text, unknown command names, missing fields

---

## Phase 6 — XML adapter

- [ ] Complete `XmlNodeAdapter.GetProperty` — element child semantics
- [ ] Complete `XmlNodeAdapter.SetProperty`
- [ ] Complete `XmlNodeAdapter.RemoveProperty`
- [ ] Complete `XmlNodeAdapter.GetValue<T>`
- [ ] Complete `XmlNodeAdapter.DeepClone`
- [ ] Complete `XmlNodeAdapter.Replace`
- [ ] Complete `XPathItemsFetcher.GetPath` — build full XPath from element lineage
- [ ] Complete `XPathItemsFetcher.ResolveRelativePath`
- [ ] Add `XmlExecutionContext.CreateDefault()` factory
- [ ] Write `XmlAdapterTests`
- [ ] Write `XmlCommandIntegrationTests` — Set + Add + Remove against a real XElement tree

---

## Phase 7 — YAML adapter

- [ ] Complete `YamlNodeAdapter.InsertIntoArray`
- [ ] Complete `YamlNodeAdapter.RemoveFromArray`
- [ ] Complete `YamlNodeAdapter.GetValue<T>`
- [ ] Complete `YamlNodeAdapter.DeepClone`
- [ ] Complete `YamlNodeAdapter.Replace` — requires parent tracking or YamlDotNet visitor
- [ ] Complete `YamlPathItemsFetcher.SelectNodes` — dot-notation path traversal
- [ ] Complete `YamlPathItemsFetcher.SelectNode`
- [ ] Complete `YamlPathItemsFetcher.GetPath`
- [ ] Complete `YamlPathItemsFetcher.GetParent`
- [ ] Add `YamlExecutionContext.CreateDefault()` factory
- [ ] Write `YamlAdapterTests`
- [ ] Write `YamlCommandIntegrationTests` — Set + Add + Remove against a real YamlMappingNode

---

## Future Specs (not in scope here)

- `002-script-parser` — JSON-envelope script text format
- `003-xml-script-syntax` — whether XML scripts can be expressed as XML documents
- `004-extended-commands` — IfElse, DecisionTable, Merge for TLio
- `005-etl-extension` — Flatten/Restore for TLio
- `006-tlio-client-nuget` — NuGet packaging and versioning strategy
