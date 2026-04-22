# Data Model: Parse-Once Script Reuse and STJ Path Fetcher Optimization

**Branch**: `013-parse-once-stj-optimize` | **Date**: 2026-04-22

---

## New Types

### `CompiledScript<TNode>` — `TLio.Client`

An immutable handle representing a parsed script template. Produced once from script text; produces per-execution instances on demand.

| Member | Kind | Visibility | Description |
|--------|------|-----------|-------------|
| `CompiledScript(TLioScript<TNode> template)` | ctor | `internal` | Wraps the parsed template. Not for direct construction by callers. |
| `CreateExecutable()` | method | `public` | Returns a new `TLioScript<TNode>` with every command cloned. Each returned instance has independent mutable state. |
| `Execute(TNode data, IExecutionContext<TNode> context)` | method | `public` | Convenience: calls `CreateExecutable()` then `TLioScript.Execute()`. Returns `TLioExecutionResult<TNode>`. |

Invariants:
- The internal template is never exposed directly; `CreateExecutable()` always returns a defensive clone.
- Thread-safe for concurrent calls to `CreateExecutable()` and `Execute()` — the template list is read-only after construction.

---

### Changes to `ICommand<TNode>` — `TLio.Core/Contracts`

A new default interface method is added. No existing implementations are broken.

| Member | Kind | Default impl | Description |
|--------|------|-------------|-------------|
| `Clone()` | method | `throw new NotSupportedException(...)` | Returns an independent copy of this command with its own execution state. |

Implementations that derive from `CommandBase<TNode>` inherit the override automatically. External implementors that do not derive from `CommandBase` receive a `NotSupportedException` when `Clone()` is called, indicating they must override it to participate in `CompiledScript` usage.

---

### Changes to `CommandBase<TNode>` — `TLio.Core/Models`

One method override added.

| Member | Kind | Description |
|--------|------|-------------|
| `Clone()` | override | `return (ICommand<TNode>)MemberwiseClone();` — shallow copy. Since `_executionFailed` is a `bool` (value type), the clone has its own independent flag. Configuration properties (path, value, config) are reference-shared but read-only during execution. |

---

### Changes to `ScriptEngine<TNode>` — `TLio.Client`

Two new `Compile` overloads. Existing `Execute` signatures unchanged.

| Member | Kind | Description |
|--------|------|-------------|
| `Compile(string scriptText, INodeAdapter<TNode> adapter)` | method | Parses `scriptText` using `CommandConverter` with the provided adapter. Returns `CompiledScript<TNode>`. |
| `Compile(string scriptText, IExecutionContext<TNode> context)` | method | Convenience overload. Delegates to the above using `context.NodeAdapter`. |

---

### Changes to `SystemTextJsonPathItemsFetcher` — `TLio.Json.SystemText`

State added for selector cache and per-execution document cache.

| Member | Kind | Scope | Description |
|--------|------|-------|-------------|
| `_selectorCache` | field | `private static readonly ConcurrentDictionary<string, JsonSelector>` | Application-lifetime cache of compiled path selectors, keyed by path string. Thread-safe; shared across all instances and executions. |
| `_cachedRoot` | field | `private JsonNode?` | Last root node for which a document was built. Used to detect when the root reference changes. |
| `_cachedJson` | field | `private string?` | Serialised JSON of the last cached root. Used to detect content mutations (different JSON = stale cache). |
| `_cachedDocument` | field | `private JsonDocument?` | Cached `JsonDocument` for the last root. Reused when `_cachedJson` matches the current serialisation. |
| `Dispose()` | method | `public` | Disposes `_cachedDocument` to return internal arrays to pool. Implements `IDisposable`. |

Cache invalidation rule: on each `SelectNodes` / `SelectNode` call, serialize `data` → if JSON string equals `_cachedJson` → reuse `_cachedDocument` (skip `JsonDocument.Parse`); else dispose old, parse new, update all three fields.

---

## Unchanged Types

| Type | Reason |
|------|--------|
| `TLioScript<TNode>` | Used as the executable instance type returned by `CreateExecutable()`. No changes needed. |
| `FunctionBase<TNode>` | `Arguments` is read-only during execution — no per-execution mutable state. No clone needed. |
| `ExecutionContext<TNode>` | Not changed. Per-execution isolation is achieved by callers creating a new context per execution (existing recommendation). |
| `SystemTextJsonNodeAdapter` | Not changed. |
| `SystemTextJsonExecutionContext` | Not changed. `CreateDefault()` already produces a fresh `SystemTextJsonPathItemsFetcher` per call. |

---

## Lifecycle Diagram

```
                          [startup]
                     ScriptEngine<TNode>
                           │
                    Compile(scriptText, adapter)
                           │
                    CompiledScript<TNode>        ← immutable; safe to hold long-term
                     /        \        \
       CreateExecutable()  Execute()  Execute()   ← per concurrent request
            │                  │          │
   TLioScript<TNode>       (same)      (same)    ← cloned per call; discarded after
    [cloned commands]                            ← owns _executionFailed per command

                          [per execution]
            SystemTextJsonPathItemsFetcher       ← one instance per CreateDefault()
                    ├─ _selectorCache (static)   ← shared, warm across executions
                    └─ _cachedDocument (instance)← scoped to this execution, disposed
```
