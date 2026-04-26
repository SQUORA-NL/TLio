# Tasks: Universal Format Converter via Intermediate Model

**Branch**: `017-im-format-converter`  
**Input**: Design documents from `/specs/017-im-format-converter/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

---

## Phase 1: Solution Setup

**Purpose**: Create the 6-project solution skeleton with correct references.

- [X] T001 Create solution directory `FormatConverter/` with `FormatConverter.sln` and `Directory.Build.props` (net10.0, Nullable enable, ImplicitUsings enable)
- [X] T002 [P] Create `src/FormatConverter.Core/FormatConverter.Core.csproj` — no format deps, no TLio deps
- [X] T003 [P] Create `src/FormatConverter.Json/FormatConverter.Json.csproj` — ref Core; System.Text.Json built-in
- [X] T004 [P] Create `src/FormatConverter.Xml/FormatConverter.Xml.csproj` — ref Core; System.Xml built-in
- [X] T005 [P] Create `src/FormatConverter.Yaml/FormatConverter.Yaml.csproj` — ref Core; add YamlDotNet (MIT)
- [X] T006 [P] Create `src/FormatConverter.TLio/FormatConverter.TLio.csproj` — ref Core + all adapters + TLio.Core
- [X] T007 Create `tests/FormatConverter.Tests/FormatConverter.Tests.csproj` — ref all src projects; NUnit 4.x + NUnit3TestAdapter
- [X] T008 Add all 6 projects to `FormatConverter.sln`; verify `dotnet build FormatConverter.sln` succeeds with zero errors

**Checkpoint**: Solution builds clean with all projects referencing correctly.

---

## Phase 2: Foundational — Core IM Types and ConversionSettings

**Purpose**: Define the types that every adapter and the runner depend on.

**⚠️ CRITICAL**: No adapter or runner work can begin until this phase is complete.

- [X] T009 Create `src/FormatConverter.Core/Model/ScalarType.cs` — enum: `String | Integer | Decimal | Boolean | Null`
- [X] T010 Create `src/FormatConverter.Core/Model/NodeMetadata.cs` — `Dictionary<string,string>` with key-prefix constants: `AttributePrefix = "@"`, `NamespacePrefix = "xmlns:"`, `TextProperty = "#text"`, `CdataKey = "#cdata"`, `MixedKey = "#mixed"`
- [X] T011 Create `src/FormatConverter.Core/Model/IntermediateNode.cs` — abstract: `Name: string?`, `Metadata: NodeMetadata`
- [X] T012 [P] Create `src/FormatConverter.Core/Model/ObjectNode.cs` — `Children: IList<IntermediateNode>`
- [X] T013 [P] Create `src/FormatConverter.Core/Model/ArrayNode.cs` — `Items: IList<IntermediateNode>`
- [X] T014 [P] Create `src/FormatConverter.Core/Model/ScalarNode.cs` — `Type: ScalarType`, `RawValue: string?`; guard: `RawValue != null` unless `Type == Null`
- [X] T015 Create `src/FormatConverter.Core/Model/MixedContentNode.cs` — `Content: IList<MixedContentItem>`; `MixedContentItem` is a discriminated union of `TextRun(string)` and `ChildNode(IntermediateNode)`
- [X] T016 Create `src/FormatConverter.Core/ConversionSettings.cs` — immutable record/class; all six settings with documented defaults; static `ConversionSettings.Empty`; unknown-key tolerance policy documented in XML doc comments
- [X] T017 Create `src/FormatConverter.Core/IFormatAdapter.cs` — `string FormatId { get; }`, `IntermediateNode ToIM(string source, ConversionSettings settings)`, `string FromIM(IntermediateNode root, ConversionSettings settings)`
- [X] T018 [P] Create `src/FormatConverter.Core/Exceptions/FormatNotRegisteredException.cs` — `FormatId: string`, `RegisteredIds: IReadOnlyList<string>`
- [X] T019 [P] Create `src/FormatConverter.Core/Exceptions/FormatParseException.cs` — `FormatId: string`, `Operation: string`
- [X] T020 Create `src/FormatConverter.Core/FormatConverter.cs` — `Register(IFormatAdapter)`, `ToIM(formatId, source, settings)`, `FromIM(formatId, node, settings)`, `Convert(srcId, doc, tgtId, settings)`; case-insensitive format ID; replace on duplicate registration
- [X] T021 Write `tests/FormatConverter.Tests/Core/FormatConverterTests.cs`:
  - `ToIM_ThrowsFormatNotRegistered_WhenUnknownFormat`
  - `Register_SecondAdapter_ReplacesFirst`
  - `Convert_PassesSettingsToBothAdapters`
  - `FormatId_Lookup_IsCaseInsensitive`
- [X] T022 Write `tests/FormatConverter.Tests/Core/ConversionSettingsTests.cs`:
  - `Empty_HasAllDefaults`
  - `PartialOverride_PreservesUnspecifiedDefaults`

**Checkpoint**: Core compiles; unit tests pass. Ready for adapter work.

---

## Phase 3: JSON Adapter (Priority: P1) 🎯 MVP

**Goal**: `System.Text.Json`-only adapter with full type mapping. No Newtonsoft dependency.

### Fixtures

- [X] T023 [P] `tests/FormatConverter.Tests/Fixtures/Json/simple-object/` — `input.json`, `expected-roundtrip.json`
- [X] T024 [P] `tests/FormatConverter.Tests/Fixtures/Json/nested/` — `input.json`, `expected-roundtrip.json`
- [X] T025 [P] `tests/FormatConverter.Tests/Fixtures/Json/scalars/` — all scalar types; `expected-roundtrip.json`
- [X] T026 [P] `tests/FormatConverter.Tests/Fixtures/Json/arrays/` — root array, mixed items; `expected-roundtrip.json`
- [X] T027 [P] `tests/FormatConverter.Tests/Fixtures/Json/empty/` — `{}`, `[]`; `expected-roundtrip.json`

### Implementation

- [X] T028 Implement `src/FormatConverter.Json/JsonFormatAdapter.cs` — `FormatId = "json"`
  - `ToIM`: `JsonDocument` → IM; `Object→ObjectNode`, `Array→ArrayNode`, `String→ScalarNode(String)`, `Number→ScalarNode(Integer|Decimal)`, `True/False→ScalarNode(Boolean)`, `Null→ScalarNode(Null)`; pass `settings` through (no JSON-specific settings in v1)
  - `FromIM`: `Utf8JsonWriter` ← IM; reverse mapping; `MixedContentNode` collapses to string value
- [X] T029 Write `tests/FormatConverter.Tests/Adapters/JsonAdapterTests.cs` — `[TestCaseSource]` over Fixtures/Json/
- [X] T030 Verify: `grep -r "Newtonsoft" src/FormatConverter.Json/` → zero results

**Checkpoint**: JSON round-trip all fixtures pass. Zero Newtonsoft references.

---

## Phase 4: XML Adapter (Priority: P1)

**Goal**: `System.Xml`-only adapter; preserves attributes, namespaces, CDATA; honours all XML `ConversionSettings`.

### Fixtures

- [X] T031 [P] `tests/FormatConverter.Tests/Fixtures/Xml/simple-elements/` — `input.xml`, `expected-roundtrip.xml`
- [X] T032 [P] `tests/FormatConverter.Tests/Fixtures/Xml/attributes/` — `input.xml`, `expected-roundtrip.xml`, `expected-as-json.json` (Badgerfish `@`)
- [X] T033 [P] `tests/FormatConverter.Tests/Fixtures/Xml/namespaces/` — `input.xml` with `xmlns:`, `expected-roundtrip.xml`
- [X] T034 [P] `tests/FormatConverter.Tests/Fixtures/Xml/cdata/` — CDATA input, `expected-roundtrip.xml`
- [X] T035 [P] `tests/FormatConverter.Tests/Fixtures/Xml/mixed-content/` — `<p>text <b>bold</b> end</p>`, `expected-roundtrip.xml`, `expected-as-json.json` (collapsed)
- [X] T036 [P] `tests/FormatConverter.Tests/Fixtures/Xml/infer-types/` — XML with numeric/bool text; `expected-as-json-inferred.json` (typed values)

### Implementation

- [X] T037 Implement `src/FormatConverter.Xml/XmlFormatAdapter.cs` — `FormatId = "xml"`
  - `ToIM`: `XmlDocument` → IM; read `settings.AttributePrefix`, `settings.TextProperty`, `settings.NamespacePrefix`, `settings.InferTypes`, `settings.CdataAsText`; produce `MixedContentNode` for mixed content; store attributes in `Metadata[@{name}]`
  - `FromIM`: `XmlWriter` ← IM; restore `Metadata[@*]` as attributes; restore `Metadata[xmlns:*]` as namespace decls; expand `MixedContentNode` as interleaved text/elements; repeat `ArrayNode` items as sibling elements named by parent
- [X] T038 Write `tests/FormatConverter.Tests/Adapters/XmlAdapterTests.cs` — `[TestCaseSource]`; include parameterised tests for non-default `attributePrefix` and `textProperty` settings
- [X] T039 Verify: `grep -r "Newtonsoft" src/FormatConverter.Xml/` → zero results

**Checkpoint**: XML round-trip including attributes, namespaces, CDATA, mixed content.

---

## Phase 5: YAML Adapter (Priority: P1)

**Goal**: YamlDotNet adapter honouring `inferTypes` and `flattenAnchors` settings.

### Fixtures

- [X] T040 [P] `tests/FormatConverter.Tests/Fixtures/Yaml/simple-mapping/` — `input.yaml`, `expected-roundtrip.yaml`
- [X] T041 [P] `tests/FormatConverter.Tests/Fixtures/Yaml/sequences/` — sequence of mappings; `expected-roundtrip.yaml`
- [X] T042 [P] `tests/FormatConverter.Tests/Fixtures/Yaml/scalars/` — string, int, float, bool, null; `expected-roundtrip.yaml`
- [X] T043 [P] `tests/FormatConverter.Tests/Fixtures/Yaml/anchors/` — anchor/alias; `expected-roundtrip.yaml` (aliases dereferenced)
- [X] T044 [P] `tests/FormatConverter.Tests/Fixtures/Yaml/infer-types/` — untagged numeric/bool; `expected-as-json-inferred.json`

### Implementation

- [X] T045 Implement `src/FormatConverter.Yaml/YamlFormatAdapter.cs` — `FormatId = "yaml"`
  - `ToIM`: `YamlStream` → IM; read `settings.InferTypes`, `settings.FlattenAnchors`; mark flattened anchors with `Metadata["#anchor-flattened"] = "true"`
  - `FromIM`: YamlDotNet serialisation ← IM; collapse `MixedContentNode` to string; read settings for any output options
- [X] T046 Write `tests/FormatConverter.Tests/Adapters/YamlAdapterTests.cs` — `[TestCaseSource]` over Fixtures/Yaml/
- [X] T047 Verify: `grep -r "Newtonsoft" src/FormatConverter.Yaml/` → zero results

**Checkpoint**: All three adapters individually pass. Ready for cross-format and pipeline work.

---

## Phase 6: MultiFormatScriptRunner + ConvertCommand (Priority: P1)

**Goal**: The `FormatConverter.TLio` integration package — segmented pipeline execution and `convert` command.

### Implementation

- [X] T048 Create `src/FormatConverter.TLio/ScriptSection.cs` — internal record: `FormatId: string`, `Commands: IList<ICommand>`, `IncomingSettings: ConversionSettings?`
- [X] T049 Create `src/FormatConverter.TLio/ConvertCommand.cs` — implements `ICommand<TNode>`; parses `to` and `settings` from JSON; intercepted by `MultiFormatScriptRunner` (no-op if executed directly)
- [X] T050 Create `src/FormatConverter.TLio/MultiFormatScriptRunner.cs`:
  - Constructor: `MultiFormatScriptRunner(FormatConverter converter, IContextFactory contextFactory)`
  - `Execute(string initialFormatId, string inputDoc, IList<ICommand> script)` → `string`
  - Pre-processing: scan script for `ConvertCommand`, split into `ScriptSection` list
  - Execution loop: per section create `IExecutionContext<TNode>`, run commands, convert at boundary using `FormatConverter.Convert` with `IncomingSettings`
  - Logging: log each section start and each convert boundary via TLio's `IExecutionLogger`
- [X] T051 Create `src/FormatConverter.TLio/docs/ai-ref/commands/ConvertCommand.md` — Article XI compliant, ≤150 lines (see research.md §9 for content template)
- [X] T052 Register `ConvertCommand` via TLio's command registration mechanism in `FormatConverter.TLio` setup extension

### Pipeline Fixtures

- [X] T053 [P] `tests/FormatConverter.Tests/Fixtures/Pipeline/xml-to-json/` — `script.json` (XML in, one convert→JSON, JSON commands, JSON out), `input.xml`, `expected.json`
- [X] T054 [P] `tests/FormatConverter.Tests/Fixtures/Pipeline/xml-json-yaml/` — `script.json` (two convert commands), `input.xml`, `expected.yaml`
- [X] T055 [P] `tests/FormatConverter.Tests/Fixtures/Pipeline/settings-override/` — `script.json` (convert with non-default `attributePrefix`), `input.xml`, `expected.json`
- [X] T056 [P] `tests/FormatConverter.Tests/Fixtures/Pipeline/infer-types/` — `script.json` (convert with `inferTypes: true`), `input.xml`, `expected.json` (typed values)

### Pipeline Tests

- [X] T057 Write `tests/FormatConverter.Tests/Integration/PipelineTests.cs` — `[TestCaseSource]` over Fixtures/Pipeline/; drives `MultiFormatScriptRunner.Execute`
- [X] T058 Add inline tests:
  - `ConvertCommand_UnregisteredFormat_ThrowsAtBoundaryNotAtLoad`
  - `ConvertCommand_DefaultSettings_MatchDocumentedDefaults`
  - `MultiFormatScriptRunner_NoConvertCommands_ExecutesAsSingleSection`
  - `MultiFormatScriptRunner_TwoConvertCommands_ProducesThreeSections`

**Checkpoint**: SC-001 and SC-008 satisfied — `{ "command": "convert", "to": "json" }` works end-to-end.

---

## Phase 7: Round-Trip Cross-Format Fidelity (Priority: P2)

**Goal**: All 6 cross-format round-trip paths preserve structure, types, and ordering.

### Fixtures

- [X] T059 [P] `Fixtures/RoundTrip/json-xml-json/` — `input.json`, `expected.json`
- [X] T060 [P] `Fixtures/RoundTrip/json-yaml-json/` — `input.json`, `expected.json`
- [X] T061 [P] `Fixtures/RoundTrip/xml-json-xml/` — `input.xml` (with attributes), `expected.xml`
- [X] T062 [P] `Fixtures/RoundTrip/xml-yaml-xml/` — `input.xml`, `expected.xml`
- [X] T063 [P] `Fixtures/RoundTrip/yaml-json-yaml/` — `input.yaml`, `expected.yaml`
- [X] T064 [P] `Fixtures/RoundTrip/attribute-roundtrip/` — `input.xml`, `expected-json.json`, `expected-xml.xml`

### Tests

- [X] T065 Write `tests/FormatConverter.Tests/Integration/RoundTripTests.cs` — `[TestCaseSource]` over Fixtures/RoundTrip/; recursive IM node comparator (`ImNodeEqualityComparer`)
- [X] T066 Add `ImNodeEqualityComparer` helper in test project — recursive structural comparison ignoring metadata order

**Checkpoint**: SC-002 satisfied — all round-trip paths pass.

---

## Phase 8: Extensibility Proof (Priority: P3)

**Goal**: Prove adding a new format requires exactly 2 methods and zero existing code changes.

- [X] T067 Implement `tests/FormatConverter.Tests/Stubs/EchoFormatAdapter.cs` — `FormatId = "echo"`; `ToIM` wraps input in `ScalarNode`; `FromIM` returns `RawValue`; accepts but ignores all settings
- [X] T068 Write extensibility tests:
  - `EchoAdapter_RegisteredAlongsideExisting_WorksAsSourceAndTarget`
  - `EchoAdapter_AddedWithZeroChanges_ToExistingAdapters`
  - `ExistingAdapterTests_StillPass_AfterEchoRegistered`

**Checkpoint**: SC-003 satisfied.

---

## Phase 9: XML Attribute Preservation (Priority: P2)

**Goal**: XML attributes survive all conversion paths and honour `attributePrefix` setting.

- [X] T069 [P] `Fixtures/Xml/attr-to-json/` — `input.xml` (`<item id="42">`), `expected.json` (`{"item":{"@id":"42"}}`)
- [X] T070 [P] `Fixtures/Xml/json-to-xml-attr/` — `input.json` (`{"item":{"@id":"42"}}`), `expected.xml` (`<item id="42"/>`)
- [X] T071 [P] `Fixtures/Xml/custom-prefix/` — same as above but `attributePrefix = "attr_"`; `expected.json` (`{"item":{"attr_id":"42"}}`)
- [X] T072 Write attribute-specific tests in `XmlAdapterTests.cs`:
  - `XmlAttributes_SurviveJsonRoundTrip_WithDefaultPrefix`
  - `AttributePrefix_Override_AppliedPerCommand`
  - `NamespaceDeclarations_PreservedInMetadata`
- [X] T073 Verify SC-004: no adapter `FromIM` silently drops `Metadata` entries — grep for any unconditional Metadata clear

**Checkpoint**: SC-004 satisfied.

---

## Phase 10: Polish, Performance, and ai-ref (Cross-Cutting)

- [X] T074 Write `tests/FormatConverter.Tests/Integration/PerformanceTests.cs` — synthetic 1 MB JSON → XML; assert < 2 seconds (SC-005)
- [X] T075 Write error-handling tests:
  - `ToIM_MalformedJson_ThrowsFormatParseException`
  - `ToIM_MalformedXml_ThrowsFormatParseException`
  - `ToIM_MalformedYaml_ThrowsFormatParseException`
  - `FormatParseException_IncludesFormatIdAndOperation`
  - `FormatNotRegisteredException_ListsRegisteredIds`
  - `ConvertCommand_UnknownSettingKey_LogsWarningNotError`
- [X] T076 [P] Verify SC-006: `dotnet list package --include-transitive` on each project — assert Newtonsoft absent in all
- [X] T077 [P] Verify Article XI: `ConvertCommand.md` exists at `src/FormatConverter.TLio/docs/ai-ref/commands/ConvertCommand.md` and is ≤150 lines
- [X] T078 Run full `dotnet test FormatConverter.sln` — all tests green
- [X] T079 [P] Update `quickstart.md` with any corrections found during implementation

---

## Dependencies & Execution Order

| Phase | Depends On | Can Parallel With |
|-------|------------|-------------------|
| Phase 1 (Setup) | — | — |
| Phase 2 (Core IM) | Phase 1 | — |
| Phase 3 (JSON) | Phase 2 | Phase 4, 5 |
| Phase 4 (XML) | Phase 2 | Phase 3, 5 |
| Phase 5 (YAML) | Phase 2 | Phase 3, 4 |
| Phase 6 (Runner) | Phase 2, 3, 4, 5 | — |
| Phase 7 (Round-trip) | Phase 3, 4, 5 | Phase 8 |
| Phase 8 (Extensibility) | Phase 2 | Phase 7 |
| Phase 9 (Attributes) | Phase 3, 4 | Phase 7, 8 |
| Phase 10 (Polish) | All prior | — |

### MVP Path

Phase 1 → Phase 2 → Phase 3 (JSON only) → Phase 6 (basic pipeline, JSON→XML at minimum) → validate → continue.

---

## Notes

- `[P]` = touches different files, no intra-phase dependency — safe to parallelise
- Every adapter must pass the Newtonsoft grep before marking implementation task done
- `ConversionSettings.Empty` must be passable wherever settings are expected — no null checks in adapter code
- `MixedContentNode` serialisation to non-XML is lossy by design — mark with `#mixed` and document in test comments
- `ConvertCommand.md` (ai-ref) must be present before `FormatConverter.TLio` can be considered done (Article XI)
