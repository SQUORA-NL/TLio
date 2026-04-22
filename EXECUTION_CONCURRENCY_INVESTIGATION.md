# TLio Execution Investigation: Thread Safety, Performance, and Data Leakage

Date: 2026-04-22  
Scope: `TLio.Core`, `TLio.Client`, `TLio.Commands`, `TLio.Functions`, `TLio.Json`, `TLio.Json.SystemText`, `TLio.Xml`, `TLio.Yaml`, and sample runtime usage.

## Executive summary

TLio is designed as a mutable in-memory transformation engine. It is safe and performant when each execution owns its own runtime objects, but it is **not safe** to share parsed scripts, execution contexts, or mutable data nodes across concurrent executions.

Main risks identified:

1. **Thread-safety risk (high):** parsed command/function objects and execution context internals contain mutable state.
2. **Data leakage risk (high):** shared context/logger or shared input node graphs can leak data across parallel runs.
3. **Performance bottlenecks (medium/high):** repeated script parsing and heavy path-selection implementations (especially `SystemTextJsonPathItemsFetcher`) increase CPU and allocations under parallel load.

---

## Findings

## 1) Parsed script objects are not thread-safe for concurrent execution (HIGH)

Relevant implementation:
- `TLio.Core/Models/TLioScript.cs`
- `TLio.Core/Models/CommandBase.cs`
- command classes in `TLio.Commands/*`
- function base in `TLio.Core/Models/FunctionBase.cs`

Why:
- Commands are class instances with mutable properties (for example `Path`, `Value`, `Config`, etc.).
- `CommandBase<TNode>` stores mutable execution state (`_executionFailed`).
- Functions store mutable `Arguments` via `SetArguments(...)`.
- `TLioScript<TNode>` is a mutable list of command objects.

Impact:
- Running the **same parsed `TLioScript<TNode>` instance** from multiple threads can cause race conditions and non-deterministic outcomes.

Conclusion:
- Treat parsed scripts as **single-thread-owned**, or create separate instances per concurrent worker.

## 2) Execution context is mutable and not thread-safe for sharing (HIGH)

Relevant implementation:
- `TLio.Core/Models/ExecutionContext.cs`
- `TLio.Core/Models/ExecutionLogger.cs`
- `TLio.Core/Models/Logging/LogEntries.cs`

Why:
- `ExecutionContext<TNode>` exposes mutable settable properties (`ItemsFetcher`, `NodeAdapter`, `Logger`).
- `ExecutionLogger` writes into `LogEntries`, which inherits from `List<LogEntry>` (not synchronized).

Impact:
- Sharing one context across concurrent executions can interleave logs, corrupt list state, and leak per-request information between runs.

Conclusion:
- Use **one execution context per execution unit** (request/message/job item).

## 3) Providers are safe for read-mostly usage, unsafe for concurrent mutation (MEDIUM)

Relevant implementation:
- `TLio.Client/CommandsProvider.cs`
- `TLio.Client/FunctionsProvider.cs`
- `TLio.Client/ParseOptions.cs`

Why:
- Providers use plain `Dictionary<string, Func<...>>`.
- Concurrent reads are acceptable only when there are no writes.
- Registering commands/functions while other threads are executing lookups is unsafe.

Impact:
- Dynamic registration at runtime under load can cause races or failures.

Conclusion:
- Register everything at startup, then freeze configuration (no further registration).

## 4) YAML parent tracking introduces statefulness and potential growth risk (MEDIUM)

Relevant implementation:
- `TLio.Yaml/YamlParentTracker.cs`
- `TLio.Yaml/YamlNodeAdapter.cs`
- `TLio.Yaml/YamlPathItemsFetcher.cs`

Why:
- Parent metadata is tracked in a dictionary keyed by node reference.
- Tracker state grows as more nodes are touched and is not cleared automatically.

Impact:
- Reusing long-lived YAML context instances across many large documents can increase memory pressure and stale tracking risk.

Conclusion:
- Do not reuse YAML execution context for long-lived multi-tenant workloads; create per-execution contexts.

## 5) Input data models are mutable and must not be shared across runs (HIGH)

Relevant behavior:
- Commands/functions mutate `dataContext` in place through adapters.

Impact:
- Reusing the same `JToken`, `JsonNode`, `XElement`, or `YamlNode` instance across parallel executions can cause cross-item contamination (data leakage and incorrect results).

Conclusion:
- Ensure each run gets its own input graph (parse fresh input or deep clone before execution).

## 6) System.Text.Json path fetcher has higher CPU/allocation cost per selection (HIGH for throughput)

Relevant implementation:
- `TLio.Json.SystemText/SystemTextJsonPathItemsFetcher.cs`

Why:
- Each selection serializes `JsonNode` to text and parses to `JsonDocument` before path evaluation.
- This is repeated often during command execution.

Impact:
- Under concurrency, this becomes a major throughput bottleneck and increases temporary memory usage.

Conclusion:
- Prefer Newtonsoft adapter for highest current throughput, or prioritize optimization in `SystemTextJsonPathItemsFetcher`.

## 7) Parse-per-execution overhead can be avoided (MEDIUM)

Relevant implementation:
- `TLio.Client/ScriptEngine.cs` (`Execute(string scriptText, ...)` reparses each time)

Why:
- Script text execution constructs a converter and parses every call.

Impact:
- Repeated transformations with the same script incur avoidable CPU/allocations.

Conclusion:
- In high-throughput workloads, pre-parse scripts and provide each worker with an isolated executable instance.

## 8) Additional data leakage vectors (MEDIUM)

Relevant implementation:
- `ExecutionLogger` and command/function logging
- `TLio.Client/TlioLlmClient.cs`

Risks:
- Logs can contain business paths, error details, and operational context; if context is shared, logs mix between items.
- `TlioLlmClient` sends prompts/payloads to an external/local LLM endpoint; sensitive data should be scrubbed before use.

Conclusion:
- Keep logs per execution, restrict retention, and avoid sending sensitive payloads to LLM endpoints unless explicitly allowed.

---

## Safe instance model for parallel processing

For parallel item processing, use this lifecycle:

1. **Startup (once):**
   - Build parse options/providers and register all commands/functions/extensions.
   - Do not mutate providers after startup.

2. **Per worker / per item execution:**
   - Create a fresh `ExecutionContext<TNode>` (`JsonExecutionContext.CreateDefault()`, `XmlExecutionContext...`, `YamlExecutionContext...`).
   - Use a unique input node graph for that item.
   - Use a script instance not shared concurrently.
   - Collect and emit logs from that execution only.

3. **Never share concurrently:**
   - `ExecutionContext<TNode>`
   - parsed `TLioScript<TNode>` (current implementation)
   - mutable node instances (`JToken`, `JsonNode`, `XElement`, `YamlNode`)

---

## Performance optimization priorities

## Priority 1 (operational, no core refactor required)
- Stop reusing execution contexts across items.
- Stop sharing mutable input node trees.
- Avoid parse-per-call where script reuse is high.
- Freeze provider registration at startup.

## Priority 2 (targeted code improvements)
- Make command/function runtime instances immutable or execution-stateless.
- Replace mutable execution flags (`_executionFailed`) with local execution variables.
- Consider thread-safe/immutable logging model (`ConcurrentQueue` or immutable snapshots).

## Priority 3 (throughput-focused)
- Optimize `SystemTextJsonPathItemsFetcher` to avoid full serialize/parse round-trips per select.
- Add explicit script cloning/compilation strategy for safe parallel reuse.

---

## Data leakage prevention checklist

- [ ] One context per item
- [ ] One input graph per item
- [ ] No concurrent shared script instance
- [ ] No runtime provider mutations after startup
- [ ] Per-item log isolation and retention policy
- [ ] Sensitive payload redaction before LLM usage

---

## Final assessment

TLio can run many items concurrently in a performant way **if instance boundaries are strict**.  
Current codebase supports concurrency best with **per-item isolation** and **startup-only registration**, while avoiding shared mutable runtime objects.
