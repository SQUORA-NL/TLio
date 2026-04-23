# Implementation Plan: Deepen Test Coverage

**Branch**: `016-deepen-test-coverage` | **Date**: 2026-04-23 | **Spec**: [spec.md](spec.md)

## Summary

Add deep test coverage across five areas: 22 text extension functions (with `ToStringFunction` at zero), logging assertion tests for functions, ToCsv edge-case tests, math function edge cases (empty/zero/negative), and error-path tests for built-in functions (Fetch, Indirect, Partial, Datetime). All work is test-only — no production code changes.

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: NUnit 4.x, Newtonsoft.Json (JToken), TLio.Extensions.Text, TLio.Extensions.ETL, TLio.Extensions.Math, TLio.Functions  
**Storage**: N/A  
**Testing**: NUnit 4.x — `context.GetLogEntries()` for log assertions, `result.Data[0].ToObject<T>()` / `result.Data.First!.Value<T>()` for function results  
**Target Platform**: .NET 10 test runner  
**Project Type**: Test additions to existing library (no new projects)  
**Performance Goals**: N/A — tests must pass `dotnet test` in under 60 s total  
**Constraints**: No new NuGet packages, no new test infrastructure, no production code changes  
**Scale/Scope**: ~120 new test methods across 5 areas in 2 existing test projects

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing a format-specific type? | No — test-only work; test projects already depend on TLio.Json (Newtonsoft). |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? | Yes — all tests use `JsonExecutionContext.CreateDefault()`. No direct adapter construction in production code. |
| Generic-First | III | Does every new public API carry `<TNode>`? | N/A — no new public APIs; test classes are internal to test projects. |
| Process/Execution separation | IV | Do all node reads/mutations go through `context.NodeAdapter`? | N/A — test code only; production code unchanged. |
| Swappable Selection | V | Are path expressions supplied by callers? | N/A — no new commands or functions. |
| Test-First + Fixture Triplets | VI | Will every full-script-execution test use fixture triplets? | Where applicable. Inline `[TestCase]` used for unit-level edge cases (null args, path-not-found) per Article VI allowance. No new full-script tests without fixture triplets. |
| Simplicity Gate | VII | Could this be done with fewer projects? | Yes — no new projects; all tests go into existing `TLio.Functions.Tests` and `TLio.UnitTests`. |
| Backward Migration Path | VIII | Any JLio-equivalent behaviour changed or dropped? | No — test-only. |
| No Leaking Internals | IX | Do new APIs expose only `TNode`-parameterised types? | N/A — test code only. |
| Logging as Observability | X | Does every `Execute()` path log on success/skip? | Tests ASSERT this contract is satisfied; no production Execute() methods added. |
| AI Component Reference | XI | Does every new command/function/adapter have ai-ref.md? | No new commands or functions; existing ones not changed. N/A. |

## Project Structure

### Documentation (this feature)

```text
specs/016-deepen-test-coverage/
├── plan.md              ← this file
├── research.md          ← decision log
├── quickstart.md        ← test patterns and examples
└── tasks.md             ← task breakdown (/speckit.tasks output)
```

### Source Code (test files only)

```text
TLio.Functions.Tests/
  FunctionsTests/
    TextTests/
      ToStringTests.cs          ← NEW: 5+ tests for ToStringFunction
      ConcatTests.cs            ← EXPAND: add success + logging tests
      SubstringTests.cs         ← EXPAND: add success + edge cases
      CaseTests.cs              ← EXPAND: add logging assertions
      FormatTests.cs            ← EXPAND: add logging assertions
      IndexOfTests.cs           ← EXPAND: add edge cases + logging
      IsEmptyTests.cs           ← EXPAND: add edge cases
      LengthTests.cs            ← EXPAND: add edge cases + logging
      NewGuidTests.cs           ← EXPAND: add logging assertions
      PadTests.cs               ← EXPAND: add edge cases + logging
      ParseTests.cs             ← EXPAND: add error paths
      PredicateTests.cs         ← EXPAND: add match + logging tests
      ReplaceTests.cs           ← EXPAND: add empty pattern + logging
      SplitJoinTests.cs         ← EXPAND: add edge cases
      TrimTests.cs              ← EXPAND: add logging assertions
    MathTests/
      MathEdgeCaseTests.cs      ← NEW: empty/zero/negative/single edge cases
    ETLTests/
      ToCsvTests.cs             ← NEW: dedicated ToCsv edge-case tests
    FetchTests.cs               ← EXPAND: add error-path + logging tests
    IndirectTests.cs            ← EXPAND: add missing-path + logging tests
    PartialTests.cs             ← EXPAND: add too-few-args + logging tests
    DatetimeFunctionTests.cs    ← EXPAND: add invalid date + logging tests
```

## Complexity Tracking

No constitution violations. This is test-only work with no new projects or layers.
