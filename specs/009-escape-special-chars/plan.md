# Implementation Plan: Special Character Escaping in Value and Path Parsing

**Branch**: `009-escape-special-chars` | **Date**: 2026-04-19 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-escape-special-chars/spec.md`

---

## Summary

Fix and complete the escape-sequence support in `FunctionConverter<TNode>` so that values starting with `@`, `$`, or `=` can be expressed as literal strings using the `@@`, `$$`, `==` double-prefix conventions. Remove the duplicate dead code block introduced in the WIP commit. Apply the same escape rules inside function argument lists. Document and test bracket-notation escape paths for each `IItemsFetcher` implementation. Update `docs/ai-ref/` with a dedicated escaping reference section.

---

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: Newtonsoft.Json (TLio.Json adapter; used in tests), JsonPath.Net (TLio.Json.SystemText)
**Storage**: N/A
**Testing**: NUnit 4 with file-based fixture triplets (`TLio.UnitTests`, `TLio.Json.Tests`, `TLio.Xml.Tests`, `TLio.Yaml.Tests`)
**Target Platform**: .NET 10 class library (multi-adapter)
**Project Type**: Class library
**Performance Goals**: No measurable overhead — escape detection is a two-character prefix check
**Constraints**: All changes in `FunctionConverter<TNode>` must remain format-agnostic (Articles I, III, IX); no `JToken`, `XElement`, or `YamlNode` in `TLio.Client`
**Scale/Scope**: 3 user stories, 13 FRs; changes are additive except the dead-code removal

---

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing format-specific types? | No — `FunctionConverter<TNode>` uses only `INodeAdapter<TNode>`. The escape logic is pure string manipulation with no format dependency. |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? | Yes — no new adapters or fetchers are introduced. Existing injected instances are used unchanged. |
| Generic-First | III | Does every new public API carry `<TNode>`? | Yes — all changes are inside the existing `FunctionConverter<TNode>`. No new public APIs. |
| Process/Execution separation | IV | Do all node reads/mutations go through `context.NodeAdapter`? | Yes — `ParseValue` calls `adapter.CreateString(...)` as before; escape processing happens before the adapter call. |
| Swappable Selection | V | Are all path expressions supplied by callers? | Yes — escape sequences affect value literals, not path construction. Bracket-notation documentation is caller-facing. |
| Test-First + Fixture Triplets | VI | Will every full-script-execution test use file-based fixture triplets? | Yes — new escape tests that exercise a full script use fixture triplets in the appropriate test project. Pure parser unit tests (no full engine run) use inline `[Test]` in `TextHandlingTests`. |
| Simplicity Gate | VII | Could this be done with fewer projects? | No new projects are needed. Changes are limited to `TLio.Client/FunctionConverter.cs`, test additions in `TLio.UnitTests`, and documentation in `docs/ai-ref/`. |
| Backward Migration Path | VIII | Is changed/dropped behaviour documented? | Yes — the `@@` → `@` escape is a JLio parity feature. The new `$$` and `==` escapes are TLio extensions. The porting guide must note that `@@` inside unquoted strings was silently passed as a path in pre-009 TLio. |
| No Leaking Internals | IX | Do `TLio.Core` public APIs expose only `TNode`-parameterised types? | Yes — no changes to Core contracts. |
| Logging as Observability | X | Does every `Execute()` path log appropriately? | N/A — `FunctionConverter.ParseValue` is a pure parser, not an `Execute()` method. No logging needed. |
| AI Component Reference | XI | Does every new component have an `ai-ref.md`? | No new commands or functions are added. The escape convention must be added as a section to the existing `docs/ai-ref/overview.md`. |

---

## Project Structure

### Documentation (this feature)

```text
specs/009-escape-special-chars/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md              ← /speckit.tasks output
```

### Source Code Changes

```text
TLio.Client/
  FunctionConverter.cs    ← remove duplicate block; add @@/$$// == escape logic

TLio.UnitTests/
  EngineTests/
    TextHandlingTests.cs  ← add escape-sequence test cases (inline [Test])
    EscapeCharFixtures/   ← fixture triplets for full-engine escape round-trips
      AtEscape/           ← input / script / result
      DollarEscape/
      EqualsEscape/

TLio.Json.Tests/
  PathEscapeTests/
    Fixtures/
      BracketNotationDot/ ← property name with dot via $['a.b']

TLio.Yaml.Tests/
  PathEscapeTests/
    Fixtures/
      QuotedSegmentDot/   ← property name with dot (bracket or quoted notation)

TLio.Xml.Tests/
  PathEscapeTests/
    Fixtures/
      NativeXpathAttribute/
      SlashPathEscapedSegment/

docs/ai-ref/
  overview.md             ← add "Escape Sequences" section

specs/002-migration-from-jlio/
  porting-guide.md        ← note @@/$$// == conventions
```
