# Implementation Plan: Universal Format Converter via Intermediate Model

**Branch**: `017-im-format-converter` | **Date**: 2026-04-25 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/017-im-format-converter/spec.md`

## Summary

Build a new standalone .NET 10 solution (`FormatConverter`) that enables TLio scripts to pipeline data across multiple formats in a single script execution. A `convert` command signals a format boundary; the `MultiFormatScriptRunner` splits the script into strongly-typed sections and uses an Intermediate Model (IM) as the internal transport between sections — script authors never interact with IM nodes directly. Three format adapters (JSON via `System.Text.Json`, XML via `System.Xml`, YAML via YamlDotNet) implement `IFormatAdapter` with a `ConversionSettings` parameter so every conversion boundary can carry its own per-step settings (attribute prefix, text property key, type inference, etc.). The `FormatConverter.TLio` integration package bridges the converter to TLio's command and registration model.

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: `System.Text.Json` (built-in), `System.Xml` (built-in), `YamlDotNet` (MIT, YAML adapter only), `NUnit 4.x` (tests), `TLio.Core` (FormatConverter.TLio only)  
**Storage**: N/A — pure in-memory library  
**Testing**: NUnit 4.x, file-based fixture pairs per adapter scenario; end-to-end pipeline fixture scripts  
**Target Platform**: .NET 10 class library  
**Project Type**: Multi-project standalone solution (6 projects, separate from TLio solution)  
**Performance Goals**: 1 MB document converts in under 2 seconds; script section switch overhead negligible  
**Constraints**: Zero Newtonsoft.Json dependency anywhere; adapter + command registration follows TLio conventions; IM never exposed to script authors  
**Scale/Scope**: Library consumed by TLio hosts; not a service

## Constitution Check

*Source: `specs/constitution.md` and `.specify/memory/constitution.md`.*  
*Note: New standalone project — Articles II–V and VIII apply in spirit, adapted for this project's architecture. TLio-specific grep compliance checks apply only to the `FormatConverter.TLio` package's interaction with TLio.Core.*

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Does `FormatConverter.Core` import any format-specific type? | **No** — Core contains only IM types, `IFormatAdapter`, `FormatConverter`, `ConversionSettings`, and exceptions. Format libraries live exclusively in adapter projects. |
| Dependency Inversion | II | Does any orchestration code `new` a concrete adapter or fetcher? | **No** — `MultiFormatScriptRunner` and `FormatConverter` dispatch through `IFormatAdapter`. Concrete adapters are injected via registration. |
| Generic-First | III | Does `MultiFormatScriptRunner` carry `<TNode>` correctly? | **Yes** — the runner creates `IExecutionContext<TNode>` instances per section via registered context factories; no boxing to `object`. |
| Process/Execution separation | IV | Does `ConvertCommand` avoid direct `TNode` method calls? | **Yes** — `ConvertCommand` delegates to `MultiFormatScriptRunner` which serialises through `IFormatAdapter.FromIM`; it never calls methods on a `TNode` variable directly. |
| Swappable Selection | V | Are path strings hard-coded anywhere? | **N/A** — no path expressions in the converter library. |
| Test-First + Fixture Pairs | VI | Will tests use file-based fixtures? | **Yes** — each adapter scenario uses an input file + expected output file driven by NUnit `[TestCaseSource]`. Pipeline tests use full fixture scripts. |
| Simplicity Gate | VII | Could fewer projects satisfy the requirements? | **Justified at 6 projects**: `FormatConverter.TLio` must reference both `FormatConverter.Core` and `TLio.Core` — it must be separate. Adapter projects must be independently referenceable for licensing isolation. No further reduction possible. |
| Backward Migration Path | VIII | Does this change any JLio-equivalent behaviour? | **No** — entirely new library; no existing TLio/JLio behaviour modified. |
| No Leaking Internals | IX | Does `FormatConverter.Core` expose format-specific types? | **No** — public API exposes only IM types, `IFormatAdapter`, `ConversionSettings`, and `FormatConverter`. `IntermediateNode` never appears in TLio.Core's public API. |
| Logging as Observability | X | How are errors surfaced? | **Structured exceptions** from the library layer. `MultiFormatScriptRunner` logs each section switch and convert boundary via TLio's `IExecutionLogger`. |
| AI Component Reference | XI | Does this add TLio commands? | **Yes** — `ConvertCommand` is a new TLio command. It MUST have `docs/ai-ref/commands/ConvertCommand.md` when merged into the TLio solution. `FormatConverter.TLio` includes this file. |

## Project Structure

### Documentation (this feature)

```text
specs/017-im-format-converter/
├── plan.md          ← this file
├── research.md      ← Phase 0 (updated)
├── data-model.md    ← Phase 1 (updated)
├── quickstart.md    ← Phase 1 (updated)
├── contracts/       ← Phase 1 (updated)
│   ├── IFormatAdapter.md
│   ├── ConversionSettings.md
│   ├── FormatConverter.md
│   └── ConvertCommand.md
└── tasks.md         ← generated alongside this plan
```

### Source Code (new standalone solution)

```text
FormatConverter/                              ← new solution root
├── FormatConverter.sln
├── Directory.Build.props                     ← net10.0, Nullable enable, ImplicitUsings enable
│
├── src/
│   ├── FormatConverter.Core/
│   │   ├── FormatConverter.Core.csproj       ← no format deps, no TLio deps
│   │   ├── Model/
│   │   │   ├── IntermediateNode.cs           ← abstract base: Name, Metadata
│   │   │   ├── ObjectNode.cs                 ← ordered children
│   │   │   ├── ArrayNode.cs                  ← ordered items
│   │   │   ├── ScalarNode.cs                 ← typed leaf (ScalarType + RawValue)
│   │   │   ├── MixedContentNode.cs           ← interleaved text + element (XML)
│   │   │   ├── ScalarType.cs                 ← String|Integer|Decimal|Boolean|Null
│   │   │   └── NodeMetadata.cs               ← Dictionary<string,string> + key constants
│   │   ├── ConversionSettings.cs             ← per-command settings bag with typed defaults
│   │   ├── IFormatAdapter.cs                 ← ToIM(string,settings) + FromIM(node,settings) + FormatId
│   │   ├── FormatConverter.cs                ← Register + ToIM/FromIM/Convert dispatch
│   │   └── Exceptions/
│   │       ├── FormatNotRegisteredException.cs
│   │       └── FormatParseException.cs
│   │
│   ├── FormatConverter.Json/
│   │   ├── FormatConverter.Json.csproj       ← ref Core; System.Text.Json built-in
│   │   └── JsonFormatAdapter.cs              ← honours settings (no mandatory settings in v1)
│   │
│   ├── FormatConverter.Xml/
│   │   ├── FormatConverter.Xml.csproj        ← ref Core; System.Xml built-in
│   │   └── XmlFormatAdapter.cs               ← honours textProperty, attributePrefix, namespacePrefix,
│   │                                            inferTypes, cdataAsText from ConversionSettings
│   │
│   ├── FormatConverter.Yaml/
│   │   ├── FormatConverter.Yaml.csproj       ← ref Core; YamlDotNet (MIT)
│   │   └── YamlFormatAdapter.cs              ← honours inferTypes, flattenAnchors from ConversionSettings
│   │
│   └── FormatConverter.TLio/
│       ├── FormatConverter.TLio.csproj       ← ref Core + all adapters + TLio.Core
│       ├── ConvertCommand.cs                 ← ICommand<TNode>; intercepted by MultiFormatScriptRunner
│       ├── MultiFormatScriptRunner.cs        ← splits script, manages context switching, logs boundaries
│       ├── ScriptSection.cs                  ← internal: command list + format ID for one segment
│       └── docs/
│           └── ai-ref/
│               └── commands/
│                   └── ConvertCommand.md     ← Article XI ai-ref (≤150 lines)
│
└── tests/
    └── FormatConverter.Tests/
        ├── FormatConverter.Tests.csproj      ← ref all src; NUnit 4.x + NUnit3TestAdapter
        ├── Fixtures/
        │   ├── Json/                         ← per-scenario input + expected files
        │   ├── Xml/                          ← includes attribute + namespace fixtures
        │   ├── Yaml/                         ← includes anchor + type-inference fixtures
        │   ├── RoundTrip/                    ← cross-format round-trip fixture pairs
        │   └── Pipeline/                     ← full TLio script fixtures (multi-section)
        ├── Core/
        │   ├── FormatConverterTests.cs       ← registry, dispatch, error cases
        │   └── ConversionSettingsTests.cs    ← defaults, overrides, unknown keys
        ├── Adapters/
        │   ├── JsonAdapterTests.cs
        │   ├── XmlAdapterTests.cs            ← settings-parameterised tests
        │   └── YamlAdapterTests.cs
        ├── Integration/
        │   ├── RoundTripTests.cs             ← all 6 cross-format paths
        │   └── PipelineTests.cs              ← MultiFormatScriptRunner end-to-end
        └── Stubs/
            └── EchoFormatAdapter.cs          ← extensibility proof
```

**Structure Decision**: 6-project layout justified — `FormatConverter.TLio` must bridge Core and TLio.Core in isolation; adapter projects must be independently referenceable for licensing control.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| 6 projects instead of 1 | `FormatConverter.TLio` must reference both `FormatConverter.Core` and `TLio.Core` without contaminating adapter projects with TLio deps; adapters must be independently referenceable | Merging would force consumers to take all dependencies regardless of which formats they use |
