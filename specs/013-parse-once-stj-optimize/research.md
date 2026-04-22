# Research: Parse-Once Script Reuse and STJ Path Fetcher Optimization

**Branch**: `013-parse-once-stj-optimize` | **Date**: 2026-04-22

---

## Decision 1: Clone strategy for `ICommand<TNode>`

**Decision**: Use `MemberwiseClone()` in `CommandBase<TNode>`, exposed via a default interface method on `ICommand<TNode>`.

**Rationale**: `CommandBase._executionFailed` is a `bool` (value type), so `MemberwiseClone()` gives each clone its own independent flag — no extra reset needed. All configuration properties (`Path`, `Value`, etc.) are set at parse time and only read during execution, making reference-sharing across clones safe. Using a default interface method (`ICommand<TNode>.Clone()`) avoids a breaking change for external implementors: it throws `NotSupportedException` by default, and `CommandBase` overrides it with `MemberwiseClone()`.

**Alternatives considered**:
- Dedicated `CloneCommand()` abstract method on every concrete command: rejected — requires changes to all command classes.
- Full deep clone (re-serialise command to JSON and re-parse): rejected — incurs the same parse cost we're trying to eliminate.
- Copy-constructor pattern: rejected — requires a parameterless base and coupling between base and derived constructors.

**What `FunctionBase` needs**: Nothing. `FunctionBase.Arguments` is set once at parse time via `SetArguments()` and only read during `Execute()`. Functions contain no per-execution mutable state, so they are safe to share across concurrent command clones without cloning.

---

## Decision 2: `CompiledScript<TNode>` API shape

**Decision**: Sealed class in `TLio.Client` with two public methods: `CreateExecutable()` (returns a `TLioScript<TNode>` ready to run) and `Execute(TNode data, IExecutionContext<TNode> context)` (convenience wrapper that creates and runs the executable internally). Constructed by `ScriptEngine<TNode>.Compile(string scriptText, INodeAdapter<TNode> adapter)`. This is the **primary API** — no backward-compat requirement on any existing `Execute(string, ...)` overload (greenfield).

**Rationale**: The two-call pattern (compile → execute) satisfies SC-005. `CreateExecutable()` is exposed for callers who need to inspect or pre-validate the clone before running. The class is sealed to prevent misuse patterns (e.g., subclassing to bypass isolation).

**Alternatives considered**:
- Expose `CompiledScript` as an interface: rejected — no polymorphism needed; sealing prevents misuse.
- Preserve existing `Execute(string, ...)` as a required contract: not required — greenfield project with no existing users. The compiled API is the canonical entry point.

---

## Decision 3: `ScriptEngine.Compile` signature

**Decision**: `Compile(string scriptText, INodeAdapter<TNode> adapter)` — takes an adapter explicitly since `CommandConverter` requires one at parse time.

**Rationale**: `ScriptEngine` does not store a default adapter (it currently receives one via `IExecutionContext<TNode>` at execute time). Rather than storing it at construction time (a breaking constructor change), we accept it as a parameter to `Compile`. A convenience overload `Compile(string scriptText, IExecutionContext<TNode> context)` delegates to the first overload using `context.NodeAdapter`.

---

## Decision 4: STJ `JsonSelector` caching

**Decision**: Add a `static ConcurrentDictionary<string, JsonSelector> _selectorCache` to `SystemTextJsonPathItemsFetcher`. Every `SelectNodes`/`SelectNode` call uses `_selectorCache.GetOrAdd(path, JsonSelector.Parse)` instead of calling `JsonSelector.Parse(path)` directly.

**Rationale**: `JsonSelector.Parse` is O(path-length) and allocates a compiled object. For a fixed script used many times, the same path strings repeat across every execution. A static cache shares compiled selectors across all executions and all threads at zero per-call cost after the first parse. `JsonSelector` instances are immutable, thread-safe, and safe to share.

**Alternatives considered**:
- Instance-level selector cache: rejected — forfeits cross-execution reuse and warms up from cold on every `CreateDefault()` call.
- LRU-bounded cache: not needed — the path strings in a fixed script set are bounded; unbounded `ConcurrentDictionary` is acceptable.

---

## Decision 5: STJ per-execution document caching

**Decision**: Add per-instance fields to `SystemTextJsonPathItemsFetcher`: `_cachedRoot (JsonNode?)`, `_cachedJson (string?)`, `_cachedDocument (JsonDocument?)`. On each `SelectNodes(path, data)` call: serialize `data` to JSON string; if the result equals `_cachedJson`, reuse `_cachedDocument`; otherwise parse a new document, dispose the old one, update the cache. This is correct because in-place node mutations produce different serialized JSON, so the cache automatically invalidates after any mutation.

**Rationale**: `SystemTextJsonExecutionContext.CreateDefault()` creates a `new SystemTextJsonPathItemsFetcher()` on every call, meaning one fetcher instance = one execution lifetime. Per-instance state is therefore per-execution state — no cross-execution leakage. The cache eliminates `JsonDocument.Parse()` for all `SelectNodes` calls within a single command's `Execute()` (before any mutation), where the serialized JSON is identical across calls. After a mutation, the next call produces a different JSON string and triggers a fresh parse — correctness is maintained.

**Alternatives considered**:
- `ConditionalWeakTable<JsonNode, JsonDocument>` keyed by reference: rejected — in-place mutations produce a stale document with the same reference as the key; undetectable without serialisation comparison.
- Per-execution fetcher constructed by `CompiledScript.Execute`: rejected — the fetcher already IS per-execution via `CreateDefault()`; adding another wrapper layer adds complexity without benefit.
- `IDisposable` on `SystemTextJsonPathItemsFetcher`: adopted — add `Dispose()` to release `_cachedDocument` deterministically and return pooled arrays. `ExecutionContext<TNode>` does not currently implement `IDisposable`; callers who use `using` on the fetcher directly benefit; callers who let it go out of scope are still correct (GC collects it).

**Correctness guarantee (mutation-aware)**: On every `SelectNodes` call, the node is serialised to a string. If the string matches the cached string → document is reused (no parse). If the string differs → the node was mutated by a previous command → the cached document is disposed, a new one is parsed, the cache is updated. This means:
- Consecutive selections within one command (before any mutation): single parse, N reuses. ✓
- Selection after a mid-script mutation: content change detected, document rebuilt. ✓
- Root reference replaced (new `JsonNode` returned by a command): different object serialises differently → cache miss → rebuild. ✓
All scenarios match original semantics exactly.

---

## Decision 6: No new external NuGet packages

**Decision**: The STJ optimization uses only the existing `JsonCons.JsonPath` and in-box `System.Text.Json`. `JsonPath.Net` (json-everything) is available in other projects but is not added to `TLio.Json.SystemText`.

**Rationale**: `JsonPath.Net` would support direct `JsonNode` traversal without serialisation, fully eliminating the round-trip. However, it introduces a new package reference to this project and may have compatibility differences with JsonCons's RFC 9535 implementation. The selector-cache + document-cache approach achieves the primary goal (no `JsonDocument.Parse` per selection in the common case) without new dependencies.

**Alternatives considered**:
- Switch to `JsonPath.Net` for direct `JsonNode` traversal: deferred — would eliminate serialisation entirely but risks behavioural differences and adds a dependency. Worth revisiting if the document-cache approach proves insufficient.
