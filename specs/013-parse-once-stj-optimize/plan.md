# Implementation Plan: Parse-Once Script Reuse and STJ Path Fetcher Optimization

**Branch**: `013-parse-once-stj-optimize` | **Date**: 2026-04-22 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/013-parse-once-stj-optimize/spec.md`

## Summary

TLio currently re-parses the script text on every `ScriptEngine.Execute(string, ...)` call, and `SystemTextJsonPathItemsFetcher` serializes and re-parses the entire `JsonNode` tree on every path selection. This plan introduces a compile-once `CompiledScript<TNode>` type (surfaced in `TLio.Client`) that clones pre-parsed command instances cheaply per execution, and optimises `SystemTextJsonPathItemsFetcher` with a static selector cache and a mutation-aware per-execution document cache — all without changing the user's choice of System.Text.Json or introducing new NuGet dependencies. Performance tests covering single-execution and large-batch scenarios are included alongside correctness and concurrency tests.

---

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: `System.Text.Json` (in-box), `JsonCons.JsonPath` 1.1.0 (existing in TLio.Json.SystemText), NUnit (tests)  
**Storage**: N/A  
**Testing**: NUnit — correctness tests via fixture triplets, concurrency tests, and allocation-based performance assertions using `GC.GetAllocatedBytesForCurrentThread()` and `Stopwatch`  
**Target Platform**: .NET 10 class library  
**Project Type**: Library  
**Performance Goals**: (a) Single compile-once execution allocates measurably fewer bytes than parse-and-execute for the same script. (b) Batch of 1000 compile-once executions allocates a fraction of the bytes of 1000 parse-and-execute calls (parse overhead eliminated after first compile). (c) STJ fetcher invokes `JsonDocument.Parse` at most once per command's selection phase when no mutation has occurred between selections.  
**Constraints**: No new external NuGet packages; changes confined to `TLio.Client`, `TLio.Json.SystemText`, and minimal non-behavioral Core additions; all existing tests must pass  
**Scale/Scope**: Two projects modified; one new type (`CompiledScript<TNode>`); one interface extension (`ICommand<TNode>.Clone`); one class extension (`CommandBase<TNode>.Clone`); one class optimized (`SystemTextJsonPathItemsFetcher`); performance test class added

---

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing a format-specific type (JToken, XElement, …)? | **No.** The only Core change is adding `Clone()` to `ICommand<TNode>` and `CommandBase<TNode>`. No format-specific types are imported anywhere in Core. `CompiledScript<TNode>` in TLio.Client imports only `TLio.Core.Contracts` and `TLio.Core.Models`. |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? | **Yes.** No new adapters or fetchers are `new`'d in commands or functions. `CompiledScript.Execute` accepts an `IExecutionContext<TNode>` and delegates to `TLioScript.Execute`. |
| Generic-First | III | Does every new public API carry `<TNode>` as a generic parameter? No `object` in signatures? | **Yes.** `CompiledScript<TNode>`, `ScriptEngine<TNode>.Compile` — all new public APIs are generic over `TNode`. |
| Process/Execution separation | IV | Do all node reads and mutations go through `context.NodeAdapter` or `context.ItemsFetcher`? | **Yes.** No direct method calls on `TNode` variables in new code. The STJ fetcher internals call `JsonNode`-specific methods (permitted — it is an adapter assembly). |
| Swappable Selection | V | Are all path expressions supplied by callers? No paths hard-coded inside commands? | **Yes.** No changes to path-expression handling in commands or functions. |
| Test-First + Fixture Triplets | VI | Will every full-script-execution test use file-based fixture triplets? | **Yes.** Correctness and concurrency tests in `TLio.Json.SystemText.Tests/` use fixture triplets. Performance tests use fixture-based scripts with measured data; unit-level isolation tests (no full execution path) may use `[TestCase]`. |
| Simplicity Gate | VII | Could this be done with fewer projects/layers? | **Yes — and it is.** No new projects. All changes fit in existing assemblies. `CompiledScript<TNode>` is a single new file in `TLio.Client`. |
| Backward Migration Path | VIII | Is any JLio-equivalent behaviour changed or dropped? | **N/A.** Greenfield — no existing users. No JLio behaviour changes. The compiled API is the primary surface; no preservation obligation on prior `Execute(string, ...)` overloads. |
| No Leaking Internals | IX | Do TLio.Core public APIs expose only `TNode`-parameterised types? | **Yes.** `ICommand<TNode>.Clone()` returns `ICommand<TNode>`. No format types appear in Core contracts. |
| Logging as Observability | X | Does every `Execute()` path call `LogInfo` on success? | **No change required.** The clone path calls `TLioScript.Execute()` which delegates to existing command `Execute()` methods that already log. No new execute paths bypass logging. |
| AI Component Reference | XI | Does every new command, function, and adapter have a corresponding `ai-ref.md`? | **N/A.** No new commands, functions, or adapter variants are introduced. `CompiledScript<TNode>` is a client utility. No `ai-ref.md` required. |

**Gate result**: All gates pass. No violations to justify.

---

## Project Structure

### Documentation (this feature)

```text
specs/013-parse-once-stj-optimize/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── api.md           ← Phase 1 output
└── tasks.md             ← Phase 2 output (/speckit.tasks)
```

### Source Code — changed files only

```text
TLio.Core/
  Contracts/
    ICommand.cs                          ← add Clone() default interface method

  Models/
    CommandBase.cs                       ← override Clone() with MemberwiseClone()

TLio.Client/
  CompiledScript.cs                      ← NEW: sealed class, CreateExecutable() + Execute()
  ScriptEngine.cs                        ← add two Compile() overloads

TLio.Json.SystemText/
  SystemTextJsonPathItemsFetcher.cs      ← static selector cache + mutation-aware doc cache + IDisposable

TLio.Json.SystemText.Tests/
  CompiledScriptTests/
    Fixtures/
      simple-set/
        input.json
        script.json
        result.json
    CompiledScript_ConcurrencyTests.cs   ← 100-concurrent-execution correctness test
    CompiledScript_IsolationTests.cs     ← clone produces independent execution state
  SystemTextJsonPathItemsFetcherTests/
    Fixtures/  (existing)
    FetcherOptimizationTests.cs          ← verify no redundant JsonDocument.Parse per unchanged phase
    FetcherMutationCorrectnessTests.cs   ← post-mutation selections return updated values
  PerformanceTests/
    Fixtures/
      perf-script/
        input.json                       ← representative JSON payload (medium complexity)
        script.json                      ← script with multiple path selections and mutations
        result.json
    CompiledScript_PerformanceTests.cs   ← single-execution and batch allocation/timing assertions
```

**Structure Decision**: Single-project modifications only (existing 3 projects). No new projects or layers.

---

## Implementation Notes (for task generation)

### Part 1 — Core clone support (minimally invasive)

**`ICommand<TNode>.Clone()` default method**

```csharp
// In ICommand<TNode> interface
ICommand<TNode> Clone() =>
    throw new NotSupportedException(
        $"{GetType().Name} does not implement Clone(). " +
        "Derive from CommandBase<TNode> or override Clone() to use CompiledScript.");
```

**`CommandBase<TNode>.Clone()` override**

```csharp
public override ICommand<TNode> Clone() => (ICommand<TNode>)MemberwiseClone();
```

`_executionFailed` is `bool` (value type) → independent per clone. All configuration properties are reference-copied but read-only during execution → safe to share.

### Part 2 — `CompiledScript<TNode>` in TLio.Client

```csharp
public sealed class CompiledScript<TNode>
{
    private readonly TLioScript<TNode> _template;

    internal CompiledScript(TLioScript<TNode> template) => _template = template;

    public TLioScript<TNode> CreateExecutable()
    {
        var script = new TLioScript<TNode>();
        script.AddRange(_template.Select(cmd => cmd.Clone()));
        return script;
    }

    public TLioExecutionResult<TNode> Execute(TNode data, IExecutionContext<TNode> context) =>
        CreateExecutable().Execute(data, context);
}
```

### Part 3 — `ScriptEngine<TNode>` additions

```csharp
public CompiledScript<TNode> Compile(string scriptText, INodeAdapter<TNode> adapter)
{
    var converter = new CommandConverter<TNode>(_commandsProvider, _functionsProvider, adapter);
    var template  = converter.ParseScript(scriptText);
    return new CompiledScript<TNode>(template);
}

public CompiledScript<TNode> Compile(string scriptText, IExecutionContext<TNode> context) =>
    Compile(scriptText, context.NodeAdapter);
```

### Part 4 — STJ fetcher optimisation

```csharp
private static readonly ConcurrentDictionary<string, JsonSelector> _selectorCache = new();

private JsonNode?     _cachedRoot;
private string?       _cachedJson;
private JsonDocument? _cachedDocument;

private JsonDocument GetDocument(JsonNode data)
{
    var json = data.ToJsonString();
    if (json != _cachedJson)
    {
        _cachedDocument?.Dispose();
        _cachedDocument = JsonDocument.Parse(json);
        _cachedJson     = json;
        _cachedRoot     = data;
    }
    return _cachedDocument!;
}

private static JsonSelector GetSelector(string path) =>
    _selectorCache.GetOrAdd(path, JsonSelector.Parse);

public void Dispose() => _cachedDocument?.Dispose();
```

`SelectNodes` / `SelectNode` replace their inline `var json = ...; using var doc = ...` with calls to `GetDocument(data)` and `GetSelector(path)`.

**Mutation-awareness guarantee**: On every `SelectNodes` call, `data` is serialised. If the resulting string matches `_cachedJson`, the document is reused. If different (mutation occurred, or root reference was replaced), the old document is disposed, a fresh one is parsed, and the cache is updated. Every path selection therefore reflects the current post-mutation state — identical to original semantics.

### Part 5 — Performance tests

Performance tests live in `TLio.Json.SystemText.Tests/PerformanceTests/` and use `GC.GetAllocatedBytesForCurrentThread()` for allocation assertions and `Stopwatch` for elapsed-time context (non-asserting, informational).

**Single-execution test** — asserts compile-once+execute allocates fewer bytes than parse+execute:

```csharp
[Test]
public void SingleExecution_CompiledAllocatesLessThanParseAndExecute()
{
    // warm up JIT
    _ = _engine.Compile(_scriptJson, _adapter).Execute(_inputNode.DeepClone(), _ctx());

    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

    long parseBefore = GC.GetAllocatedBytesForCurrentThread();
    _engine.Execute(_scriptJson, _inputNode.DeepClone(), _ctx());   // parse + execute
    long parseAlloc = GC.GetAllocatedBytesForCurrentThread() - parseBefore;

    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

    long compiledBefore = GC.GetAllocatedBytesForCurrentThread();
    _compiled.Execute(_inputNode.DeepClone(), _ctx());               // clone + execute
    long compiledAlloc = GC.GetAllocatedBytesForCurrentThread() - compiledBefore;

    Assert.Less(compiledAlloc, parseAlloc,
        $"Compiled path ({compiledAlloc} B) should allocate less than parse path ({parseAlloc} B)");
}
```

**Batch test (1000 items)** — asserts total allocations for compiled-once batch are a fraction of parse-per-item batch:

```csharp
[Test]
public void BatchExecution_1000Items_CompiledAllocatesSignificantlyLess()
{
    const int N = 1000;

    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

    long parseBefore = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < N; i++)
        _engine.Execute(_scriptJson, _inputNode.DeepClone(), _ctx());
    long parseTotal = GC.GetAllocatedBytesForCurrentThread() - parseBefore;

    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

    long compiledBefore = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < N; i++)
        _compiled.Execute(_inputNode.DeepClone(), _ctx());
    long compiledTotal = GC.GetAllocatedBytesForCurrentThread() - compiledBefore;

    // Compiled batch should allocate no more than 50% of parse batch
    // (parse overhead is eliminated; only clone + execute remains)
    Assert.Less(compiledTotal, parseTotal * 0.5,
        $"Compiled batch ({compiledTotal} B) should be <50% of parse batch ({parseTotal} B)");
}
```

**Fixture for performance tests** (`perf-script/`):
- `input.json`: a realistic medium-complexity JSON object (5–10 fields, 1 nested object, 1 array)
- `script.json`: 4–6 commands covering set, copy, and a conditional, providing realistic parse/clone overhead
- `result.json`: expected output (used by correctness tests in the same fixture folder)

**Notes**:
- `GC.GetAllocatedBytesForCurrentThread()` is available from .NET 5+, no new dependency.
- Tests run single-threaded (no `Parallel.ForEach`) to keep allocation measurement reliable.
- A JIT warmup pass is done before measuring to avoid first-call JIT overhead skewing results.
- The 50% threshold for the batch test is conservative; in practice the reduction should be much larger (parse cost per item eliminated × N items). The threshold guards against regression, not absolute performance.

---

## Complexity Tracking

No violations. No new projects. Simplest structure that satisfies the feature.
