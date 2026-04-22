# Feature Specification: Parse-Once Script Reuse and STJ Path Fetcher Optimization

**Feature Branch**: `013-parse-once-stj-optimize`  
**Created**: 2026-04-22  
**Status**: Draft  
**Input**: User description: "Parse once, reuse — optimized clone model in TLio.Client; optimize SystemTextJsonPathItemsFetcher without changing the adapter library."

## Clarifications

### Session 2026-04-22

- Q: Is backward-compatible preservation of the existing `Execute(string, ...)` API a requirement? → A: No — greenfield, no existing users. The new compiled API is the primary surface.
- Q: Must the STJ document cache remain valid and reflect node state after mid-execution mutations by previous commands? → A: Yes — the cache must detect mutations (via content comparison) and rebuild so every path selection sees the current post-mutation state of the object, not just the initial structure.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pre-Parse Script and Execute Many Times (Priority: P1)

A developer integrating TLio into a high-throughput service wants to parse a transformation script once at startup and then execute it against many data payloads concurrently, without paying the parsing cost on each call and without risking cross-execution state contamination.

**Why this priority**: Eliminating per-call parse overhead is the primary performance gain and directly unblocks safe concurrent use. Everything else builds on this.

**Independent Test**: A caller can call `ScriptEngine.Compile(scriptText)`, receive a reusable handle, then call `.CreateExecutable()` N times and run all N instances in parallel — each producing correct, independent output.

**Acceptance Scenarios**:

1. **Given** a script compiled once, **When** `CreateExecutable()` is called 100 times and all 100 execute concurrently against distinct input nodes, **Then** every result matches what a single isolated parse-and-execute would produce.
2. **Given** a compiled script handle, **When** one executable instance fails mid-execution, **Then** other running instances are unaffected and produce correct results.
3. **Given** a compiled script handle, **When** `CreateExecutable()` is called, **Then** the returned instance has its own independent mutable state (execution flags) and does not share mutable internals with the template or other executables.

---

### User Story 2 - System.Text.Json Path Selection Reflects Current Object State (Priority: P2)

A developer using `SystemTextJsonExecutionContext` runs TLio scripts where commands mutate the JSON object at each step. They want path selections to always reflect the current state of the object after previous commands have run — with no redundant serialize/parse overhead for selections that occur before the next mutation.

**Why this priority**: Correctness of mutation-aware caching is a hard requirement; the throughput benefit (eliminating redundant parses for unchanged content) is the performance win.

**Independent Test**: A multi-command script where command 1 sets a value and command 2 selects that same path returns the value written by command 1 — proving the cache reflects post-mutation state. Additionally, a single command with two path selections against the same unmodified node triggers only one `JsonDocument` parse, not two.

**Acceptance Scenarios**:

1. **Given** a script where command 1 writes a value to path `$.a`, **When** command 2 selects `$.a`, **Then** the selection returns the value written by command 1, not the original value from before execution started.
2. **Given** a script with 20 path selections within a single command (no mutations between them), **When** executed, **Then** `JsonDocument.Parse` is called at most once for that command's selection phase, not 20 times.
3. **Given** a script where command 1 mutates the node and command 2 performs path selections, **When** the fetcher's document cache is checked, **Then** the cache detects the content change and rebuilds before serving command 2's selections.
4. **Given** any sequence of commands that interleave mutations and path selections, **When** executed to completion, **Then** every path selection returns a result identical to the original (non-cached) implementation.

---

### Edge Cases

- What happens if a compiled script handle is used with an execution context of the wrong node type?
- What happens when the script text is empty or malformed — does the error surface at compile time or at execution time?
- What happens when `CreateExecutable()` is called on a compiled script concurrently from many threads simultaneously?
- What happens when a per-execution `JsonDocument` cache is not explicitly disposed — is memory reclaimed correctly after the execution unit goes out of scope?
- What happens when a command replaces the root node entirely (returns a new `JsonNode` reference) rather than mutating in-place?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The client library MUST provide a method to compile a script text into a reusable, immutable-template object that can produce per-execution instances without re-parsing the text.
- **FR-002**: Each per-execution instance produced from a compiled template MUST have fully independent mutable state (execution flags) — no shared mutable internals between instances or with the template.
- **FR-003**: Producing a per-execution instance from a compiled template MUST be significantly cheaper than parsing the script from text (copy of pre-parsed structure, not re-parse).
- **FR-004**: The compiled template API MUST be surfaced in `TLio.Client` only — callers MUST NOT need to reference `TLio.Core` types to use it.
- **FR-005**: The `SystemTextJsonPathItemsFetcher` MUST NOT serialize a `JsonNode` to text and parse it back to `JsonDocument` on each individual path selection when the node content has not changed since the last selection.
- **FR-006**: After any command mutates the node graph, the next path selection MUST reflect the post-mutation state. The cache MUST detect content changes (by comparing serialized output) and rebuild the `JsonDocument` before serving the next selection.
- **FR-007**: The optimized STJ path fetcher MUST produce results identical to the original implementation for every scenario, including scripts with interleaved mutations and selections.
- **FR-008**: Any per-execution state in the STJ fetcher (cached `JsonDocument`, cached serialized string) MUST be scoped to a single execution and released when the execution ends, preventing memory leaks across executions.
- **FR-009**: All changes MUST be confined to `TLio.Client` and `TLio.Json.SystemText`; changes to `TLio.Core` are permitted only where strictly required to support cloning and MUST be minimally invasive with no behavioral changes to existing Core logic.
- **FR-010**: No new external NuGet dependencies may be introduced.
- **FR-011**: NUnit tests MUST cover: (a) compiled template produces independent execution state per instance, (b) concurrent executions via the compiled API produce correct, non-interfering results, (c) STJ path fetcher produces correct results after mutation sequences, (d) STJ fetcher does not re-parse when content is unchanged between consecutive selections, (e) single-execution allocation comparison (compiled vs parse-and-execute), (f) batch allocation comparison over 1000 executions (compiled-once batch vs parse-per-item batch).

### Key Entities

- **CompiledScript\<TNode\>** (new in TLio.Client): An immutable parsed representation of a script. Produced once from script text. Exposes a factory method to produce a ready-to-run executable instance per execution.
- **ScriptEngine** (new primary API): Gains `Compile(string scriptText, ...)` method returning `CompiledScript<TNode>`. This is the primary entry point; no backward-compat requirement on existing overloads.
- **SystemTextJsonPathItemsFetcher** (existing, optimized): Adds a content-aware document cache — serialize on each selection, compare with cached string, rebuild only when content changed. Eliminates `JsonDocument.Parse` overhead for consecutive selections on unchanged content.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Running 100 concurrent transformations using a pre-compiled script produces correct results for every execution, with zero cross-execution state contamination detected.
- **SC-002**: Repeated execution of the same script via the compiled API incurs zero additional parse allocations after the first compile — confirmed by test or allocation assertion.
- **SC-003**: Within a single command's selection phase (no mutation between calls), `JsonDocument.Parse` is invoked exactly once regardless of how many path selections occur — confirmed by a counter or mock assertion in tests.
- **SC-004**: A multi-command script where earlier commands mutate the object and later commands select from it produces results identical to the original non-cached implementation in all cases.
- **SC-005**: The public API for the compile-once pattern requires no more than two method calls (compile + execute) to set up and run a concurrent-safe execution unit.
- **SC-006**: A single compiled-once execution allocates fewer managed bytes than a single parse-and-execute call for the same script, confirmed by allocation assertion in a NUnit performance test.
- **SC-007**: The script-initialization overhead for 1000 `CreateExecutable()` calls allocates no more than 50% of the managed bytes of 1000 `Compile(scriptText)` calls — isolating the clone-vs-parse cost without shared execution overhead, confirmed by allocation assertion in a NUnit performance test.

## Assumptions

- `TLio.Core` command objects can be made clonable with a minimal, non-behavioral change (adding a `Clone()` default interface method and a `MemberwiseClone()` override in `CommandBase`). Configuration properties set at parse time are read-only during execution and safe to share across clones.
- `SystemTextJsonPathItemsFetcher` is instantiated once per `SystemTextJsonExecutionContext.CreateDefault()` call, meaning one fetcher instance corresponds to one execution lifetime. Instance-level caching state is therefore per-execution by construction.
- The mutation-detection mechanism (comparing `data.ToJsonString()` with the cached string on each `SelectNodes` call) is correct because any in-place mutation via `NodeAdapter` changes the serialized output, making the comparison a reliable mutation signal.
- Provider registration (commands/functions) is frozen at startup; this is a prerequisite for safe concurrent use but is not enforced programmatically by this feature.
- Performance verification is done via NUnit test assertions (parse-call counters or concurrency tests) — no external benchmarking framework is required.
