# TLio Performance

Guidance for running TLio at throughput: what must stay per-execution, how to avoid paying parse
cost on every call, and which specific commands and functions are the ones to watch. Everything
here is either measured (a dedicated performance test, or the AfdApi sample's own numbers) or
verified directly against source while writing this page — pointers are given so you can check
either.

---

## 1. Safe-usage rules (thread-safety and isolation)

TLio is a mutable in-memory transformation engine. It is fast and safe when each execution owns
its own runtime objects, and unsafe the moment those objects are shared across concurrent
executions. Full analysis, file-by-file: [concurrency-investigation.md](concurrency-investigation.md).
The rules that come out of it:

- **One `IExecutionContext<TNode>` per execution.** `JsonExecutionContext.CreateDefault()` /
  `XmlExecutionContext...` / `YamlExecutionContext...` are cheap to create and hold mutable
  logger/fetcher/adapter state — sharing one across concurrent requests interleaves logs and can
  leak per-request data between runs.
- **Never share a parsed script across threads.** A `TLioScript<TNode>` (and the `CommandBase`
  instances inside it) carries mutable execution state. `CompiledScript<TNode>.CreateExecutable()`
  exists precisely to hand each execution its own clone cheaply — see §2.
- **Never share a mutable input node graph.** Commands and functions mutate `dataContext` in
  place through the adapter. Reusing the same `JToken`/`JsonNode`/`XElement`/`YamlNode` instance
  across parallel executions causes cross-item contamination. Parse fresh input, or deep-clone,
  per execution.
- **Freeze provider registration at startup.** `CommandsProvider`/`FunctionsProvider` are plain
  dictionaries — safe for concurrent reads, not for a write racing a read. Register every
  command/function/extension pack once at startup and never register again while executions are
  in flight.
- **Don't reuse a YAML execution context long-lived across many large documents.**
  `YamlParentTracker` accumulates parent metadata keyed by node reference and is never cleared
  automatically; a per-execution context avoids the growth.

## 2. Precompile: the AfdApi worked example

Parsing script text happens on every `ScriptEngine<TNode>.Execute(string scriptText, ...)` call
and on every `MultiFormatScriptRunner.Run(...)` call. For a small script this is noise; for a
script of any real size it dominates.

**Measured before/after** ([`TLio.Sample.AfdApi/README.md`](https://github.com/SQUORA-NL/TLio-Samples/blob/main/samples/TLio.Sample.AfdApi/README.md) in TLio-Samples — "Speed"): each of the three
AFD conversion scripts is several MB. Parsing one on every request cost ~220ms per conversion for
`afdshort-to-afd2`, ~130ms of which was pure parse time. Compiling once at startup and sending a
real warm-up request through each direction before accepting traffic (so the JIT has already
tiered up) brought steady-state latency for all three directions down to **15–70ms**
(occasional spikes into the low hundreds of ms under Server GC are inherent to any .NET service
processing multi-MB payloads repeatedly, not specific to this sample).

**The two compile APIs**, matched to whether the script crosses a format boundary:

- **Single-format script** — `ScriptEngine<TNode>.Compile(scriptText, adapter)` returns an
  immutable `CompiledScript<TNode>`. `CompiledScript<TNode>.CreateExecutable()` clones a fresh,
  thread-owned `TLioScript<TNode>` cheaply (no re-parse); `CompiledScript<TNode>.Execute(data,
  context)` is the convenience form. `CreateExecutable`/`Execute` on the same `CompiledScript`
  may be called concurrently from any number of threads — each call gets its own independent
  execution state. Documented at the command level in
  [commands/ConvertValue.md](commands/ConvertValue.md#performance).
- **Script that crosses a format boundary (`convert`)** — `MultiFormatScriptRunner.Compile(
  initialFormatId, script)` returns a `CompiledMultiFormatScript`; `CompiledMultiFormatScript.Run(
  inputDocument)` re-executes it against new documents without re-parsing. Only sections whose
  registered executor implements `ICompilableFormatSectionExecutor` are pre-parsed — mixing a
  compilable and a non-compilable executor degrades gracefully rather than failing. This is what
  `ConversionRegistry.cs` in the AfdApi sample does at startup; documented at the command level in
  [commands/Convert.md](commands/Convert.md#performance).

```csharp
// Single-format
var compiled = engine.Compile(scriptText, JsonExecutionContext.CreateDefault().NodeAdapter);
var result   = compiled.Execute(data, JsonExecutionContext.CreateDefault()); // per-call context

// Crosses a format boundary (convert)
var compiled = runner.Compile("json", scriptText);
var result   = compiled.Run(inputDocumentText); // per-call document
```

Send one real request through each code path before accepting traffic — the AfdApi sample's
`Program.cs` does this — so the JIT has tiered up before a real client's first request lands.

## 3. Command and function hotspots

| Where | Cost shape | Why | Proof |
|---|---|---|---|
| `resolve` | O(targets), not O(targets × references) | The reference collection is indexed once per `resolveSettings` entry (hash bucket keyed on the join key) and reused for every target; a bucket hit is re-verified with `DeepEquals`, so a collision only costs speed, never correctness. An array-based (`[*]`) key can't be indexed and falls back to an exact linear scan for that one reference. | `TLio.Extensions.ETL/Commands/Resolve.cs` (`BuildIndex`/`FindMatches`); [commands/Resolve.md#performance](commands/Resolve.md#performance); `ResolveIndexingTests.EachTargetFindsItsOwnMatch_AmongManyReferences` (5,000 references) |
| `resolve` (function) | O(collection size) **per call**, no cache | A linear scan from the first element to the first match, comparing one property with the `equals` rule. Nothing is kept between calls on purpose: a script edits the document between one call and the next, so a cached collection or index would go stale. For many fields of one record, select it once with `with` instead of calling `resolve` per field; for a join across a whole collection use the ETL `resolve` *command*, which does index. | `TLio.Functions/Collections/Resolve.cs`; [functions/Resolve.md](functions/Resolve.md) |
| `with` | Path evaluated **once**, not once per nested command | The motivating script re-evaluated a nested filter (`party[?(@.refKey == policy[0].party[?(...)].partyRef[0])]`) in each of 28 values. With `with` it is evaluated twice. Measured on that script (`TLio.Parity.Tests/Equivalence/policy-mapping`, byte-identical output): BenchmarkDotNet 222 µs → 181 µs per run (0.81×), allocation 753 → 573 KB; `--big-document` runs/s, 1 thread 3 144 → 4 225 (Workstation) and 3 350 → 4 190 (Server); 14 threads 12 051 → 18 630 (Server). A modest, real gain — about 20 % — not an order of magnitude; the main benefit is a script that states each lookup once. | `TLio.Extensions.Looping/With.cs`; `TLio.Benchmarks/PolicyMappingBenchmarks.cs`; [commands/With.md](commands/With.md) |
| `decisionTable` | No wasted writes on a failing result; O(matched results), not O(declared outputs) | `ApplyResults` evaluates a rule's result value *before* calling `EnsurePath`/writing, so a result that fails to evaluate never leaves an ensured-but-empty `{}` placeholder, and under `allMatches` a later failing rule can no longer clobber an earlier rule's value that way. It also looks a matched rule's results up in an `outputs`-by-name index (built once per command instance, cached across every execution of a compiled script) instead of walking the full, statically-declared `outputs` list — which can run to tens of thousands of entries in a generated table — to find the handful of keys that rule actually set; results are still applied in each output's original declared order. `outputPathTemplate` is the matching size fix: an output can skip repeating its `path` when it follows a fixed pattern like `"@._new.{name}"`. | `TLio.Commands/DecisionTable.cs::ApplyResults`; [commands/DecisionTable.md#performance](commands/DecisionTable.md#performance) |
| `calculate` (Math) | Full parse+evaluate cost, every call | `Calculate<TNode>.Execute` builds a fresh `System.Data.DataTable` and calls `dt.Compute(expression, null)` on *every* invocation — nothing is parsed once and cached. Paid again for each row in a loop (`setProperties` over a wildcard selection, for example). | `TLio.Extensions.Math/Calculate.cs`; [functions/Calculate.md#performance](functions/Calculate.md#performance) |
| `sumif`/`sumifs`, `countif`/`countifs`, `averageif`/`averageifs`, `minifs`/`maxifs` | O(n) per call, O(n×k) across k conditions | `ConditionEvaluator.EvaluateCondition` re-parses the operator prefix (`>=`, `<>`, …) on every scanned element, and for a wildcard criteria (`*`/`?`) rebuilds a regex pattern and calls `Regex.IsMatch` per element — no compiled/cached pattern reused across elements or across calls. | `TLio.Extensions.Math/ConditionEvaluator.cs::EvaluateCondition`; [functions/SumIf.md#performance](functions/SumIf.md#performance) |
| `regexExtract` vs `regexReplace` | Asymmetric — one bypasses .NET's regex cache, one uses it | `regexExtract` constructs `new Regex(pattern, ...)` directly on every call, so it never reads or writes .NET's static, process-wide regex cache. `regexReplace` calls the **static** `Regex.Replace(str, pattern, replacement, options, timeout)` overload, which *does* hit that cache (15 entries by default, keyed on pattern+options+timeout) — the same pattern applied across many rows is compiled once and reused from the second call on. | `TLio.Extensions.Text/RegexExtract.cs`, `RegexReplace.cs`; [functions/RegexExtract.md#performance](functions/RegexExtract.md#performance), [functions/RegexReplace.md#performance](functions/RegexReplace.md#performance) |
| `scriptpath`/`path` find mode (`recursive: true`) | O(subtree size), not O(result size) | `CollectChildren` walks the **entire subtree** under the current node — every object, array and scalar — regardless of how many nodes actually match `kinds`. The default (path-string) shape is one ordinary `SelectNodes` lookup, same cost as any other path resolution. Call find mode once per document region, not once per node — `setProperties` already takes the whole set it returns and writes to all of them in one pass. | [functions/ScriptPath.md#performance](functions/ScriptPath.md#performance); `TLio.Xml.Tests/Performance/XmlPath_PerformanceTests.cs`, `TLio.Yaml.Tests/Performance/YamlPath_PerformanceTests.cs` |
| `parse` / `toString` | Full round-trip serialization cost | `parse` does a full text parse via `NodeAdapter.Parse` on every call; there is nothing to cache because the string is per-row data, not a fixed template. A `parse`/`toString` round trip in a hot loop costs a full stringify-then-reparse each time — if only one property of the embedded structure is needed, compare against leaving the value as a string and using a text function directly on it. | [functions/Parse.md#performance](functions/Parse.md#performance) |
| System.Text.Json path selection — **resolved** | Comparable to Newtonsoft; no longer serializes the document | Paths are evaluated by `TLio.JsonPath` directly on the `JsonNode` tree (it used to serialize the tree, parse a `JsonDocument` and navigate back on every selection). Measurements: `docs/benchmarks/jsonpath-engine.md`. | `TLio.JsonPath/`; `TLio.Json.SystemText/SystemTextJsonPathItemsFetcher.cs`; [concurrency-investigation.md](concurrency-investigation.md) §6 |
| `formatDate` / `parseDate` — **already fine** | No avoidable per-call cost | Contrast case: the TimeDate base class's fallback format list is `private static readonly string[]`, and every parse/format call uses the shared static `CultureInfo.InvariantCulture` — neither is rebuilt per call. The only real cost is the `DateTimeOffset` parse/render itself, which every TimeDate function pays regardless. Nothing here needs caching, and nothing would meaningfully benefit from it. | `TLio.Extensions.TimeDate/TimeDateFunctionBase.cs`; [functions/FormatDate.md#performance](functions/FormatDate.md#performance), [functions/ParseDate.md#performance](functions/ParseDate.md#performance) |

**Rule of thumb:** if the same expression, pattern, or condition runs once per row across a large
array, ask whether a fixed-operator function exists (`sum`/`subtract`/`multiply`/`divide`/`modulo`
instead of `calculate`; `regexReplace` instead of `regexExtract` when you don't need the extracted
text back) before reaching for the general-purpose one.

## 4. Reproducing or extending the numbers

Three dedicated performance test projects hold the measured baselines referenced above:

- `TLio.UnitTests/Performance/CommandEngine_PerformanceTests.cs` — 500 fresh-context executions
  of a simple `put` script, wall-clock threshold 2000ms; a `SingleExecution_ProducesCorrectResult`
  correctness check runs alongside it.
- `TLio.Xml.Tests/Performance/XmlPath_PerformanceTests.cs` — GC-allocation baselines for
  `SlashPathItemsFetcher`: ≤131,072 bytes/iteration over 1,000 iterations for a shallow and a
  deep path, and a ~500-row × 20-field document queried within 2000ms and 50MB.
- `TLio.Yaml.Tests/Performance/YamlPath_PerformanceTests.cs` — the same shape for
  `YamlPathItemsFetcher`: ≤16,384 bytes/iteration over 1,000 iterations, and a 200-entry nested
  document queried within 2000ms and 50MB.

All three use a warmup pass + forced GC + measure pattern, and thresholds are set at roughly 2×
the measured value on a developer machine — they exist to catch regressions, not to claim a
specific number is guaranteed on every machine.

## 5. Choosing settings for your workload: large documents and the garbage collector

TLio is a library; the garbage-collector (GC) mode is a setting of the *host process* you run it
in, and it is the single biggest throughput lever when many executions run at once. This section
says what to set, for which kind of client, and how to check it on your own data.

### 5.1 The short version

| Your client | Set | Why |
|---|---|---|
| A service (web API, worker, queue consumer) running **several scripts at the same time** — scripts of more than a handful of commands, any document size, small, large or mixed | **Server GC**, concurrent GC on | Measured 6–14× more throughput on a real 69-command script at 14 threads; Workstation GC *lost* throughput when threads were added. Memory: Server GC used about 2× more with large documents, but about **4–5× less** with small ones (§5.3) |
| A service that runs **only a few commands per script, but on large documents** (conversion pipelines: `convert` plus a couple of edits on documents of hundreds of KB) | **Workstation GC** (the default) | 1.1–1.6× faster **and about 5× less memory** (0.7 GB against 3.6 GB at 14 threads). One big parse-convert-serialize run is limited by memory traffic, not by collector contention |
| A service that runs **only a few commands per script on small documents** (≈1 KB) | **Server GC** | 2.9× at 14 threads |
| **One thread** at a time (CLI tool, batch job, desktop app, a service handling one request at once) | Leave the **default** | Single-thread throughput was equal for convert-only and 10–30 % *higher* under Server GC for the real script, while Server GC used about 2–2.7× the memory (252–394 MB against 105–193 MB) — not a reason to switch by itself |
| Tests, tools, anything short-lived | Default | |

If you cannot tell which row you are in, run the harness in §5.4 with your own script and
document — it takes a minute and prints the answer for your machine.

### 5.2 Why: what is and is not slow with big documents

A document that carries a few very large string values — base64-encoded files of a few hundred KB
each is the case that was measured — does **not** make individual commands slower. A command
touches the nodes its path selects, never the whole tree, so `put`, `copy`, `fetch`, wildcards and
functions cost the same on a document with 900 KB of base64 as on one without
(`BigObjectPerformanceTests` in `TLio.Parity.Tests` asserts this for every command and function
class, on Newtonsoft and System.Text.Json).

What grows with the strings is the work *around* the commands: parsing the document, converting
between formats, and serializing the result. Each allocates a string about the size of the
document; a string over ~85 KB lands on the large object heap, which is only reclaimed by a full
(gen 2) collection. And separately from document size, every command execution produces small
garbage (nodes, path results, logging). With many threads running at once, Workstation GC has one
heap and one collector for all of them, and the time goes into collections that pause every
thread. So the symptom is that *everything* slows down under load, with no single command to
blame.

### 5.3 What was measured

14-core Apple M4 Pro (mixed performance and efficiency cores), Release build, 8–10 s per cell,
runs per second. "Real script" is a 69-command JSON → XML script on a 615 KB document with three
base64 files; its "small" document is the same document without the files (245 KB). "Convert
only" is `convert` + four edits on an XML order (3 × 300 000 base64 characters; small = ~1 KB).

| Workload | Big share | 1 thread, Workstation | 1 thread, Server | **14 threads, Workstation** | **14 threads, Server** | Server ÷ Workstation (14) | Peak memory, 14 threads: Workstation / Server |
|---|---|---|---|---|---|---|---|
| Real script | 100 % | 348 | 392 | 304 (69 % GC pause) | 1 803 (12 %) | **5.9×** | 1.05 GB / 2.15 GB |
| Real script | 10 % | 492 | 651 | 350 (67 %) | 4 209 (17 %) | **12×** | not measured |
| Real script | 0 % | 637 | 786 | 372–383 (72–73 %) | 2 158–5 411 (8–18 %) | **5.6–14×** | **4.4–4.8 GB / 0.97 GB** |
| Convert only | 100 % | 178 | 173 | 1 507 (20 %) | 963–1 266 (4 %) | 0.64–0.84× | **0.71 GB / 3.6 GB** |
| Convert only | 10 % | 1 644 | 1 574 | 11 906 (27 %) | 10 544 (5 %) | 0.89× | not measured |
| Convert only | 0 % | 53 563 | 51 207 | 104 891 (37 %) | 299 097 (17 %) | 2.9× | not measured |

Read it like this:

- **Workstation GC on the real script got slower with more threads** (14 threads: 304–383 runs/s,
  against 348–637 on one thread), with 67–73 % of wall time spent paused. Server GC scaled
  (14 threads: 1 803–5 411). The range is run-to-run noise at 14 threads — one small-only run
  gave 2 158, two others 5 011 and 5 411 — but Workstation never came near any of them.
- **Memory goes both ways.** On the real script with *small* documents Workstation GC at 14
  threads grew to 4.4–4.8 GB, because the collector could not keep up with the garbage the
  threads made; Server GC held 0.97 GB. With *large* documents, and for the large convert-only run,
  Server GC used 2× and 5× more (its per-core heaps and larger allocation budgets). Memory is
  the working set sampled every 50 ms.
- The deciding factor is **what a run is made of**, not how big the document is: a script with
  many commands makes small garbage everywhere (Server GC wins at every document size); a run
  that is mostly one large parse-convert-serialize makes large strings (Workstation GC held up
  better).
- Not measured: thread counts between 1 and the core count, other machines, memory in a
  container with a memory limit.
  Numbers move with the hardware; the *shape* is the useful part.

### 5.4 Measure it on your own script and document

```sh
# the built-in order: 3 x 300 000 base64 characters, convert + a few edits
dotnet run -c Release --project TLio.Benchmarks -- --big-document

# your script and document (any notation, any format)
dotnet run -c Release --project TLio.Benchmarks -- --big-document \
    --script my-script.json --input my-big-input.json --from json --seconds 20

# a mixed workload: 10 % of runs get the big document, the rest a small one
dotnet run -c Release --project TLio.Benchmarks -- --big-document \
    --script my-script.json --input my-big-input.json --small-input my-small-input.json \
    --from json --big-share 10
```

It starts one process per GC mode (so the mode printed is the mode that ran), once on a single
thread and once on every core (`--threads N` to match the concurrency your service really has),
and prints runs per second, gen 2 collections, the share of wall time spent paused in GC, and
megabytes allocated per run. **A high paused share with many threads, and Workstation getting
slower as threads are added, is the signature of the problem above.** Run it on the hardware you
deploy to, not a laptop.

### 5.5 Setting it

It is a property of the host process:

```xml
<!-- the host's .csproj -->
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
</PropertyGroup>
```

or `DOTNET_gcServer=1` in the environment, or `"System.GC.Server": true` in `runtimeconfig.json`.
Check at start-up that it took effect: `System.Runtime.GCSettings.IsServerGC`.

Server GC's memory use depends on the workload (§5.3: 5× more for large conversions, 4–5× less
for a many-command script on small documents), so check the peak on your own workload; in a
container it sizes itself from the CPU limit the container reports — re-check after changing the limit.

### 5.6 What else helps, in order

1. **Compile the script once** (§2) and send a warm-up request before taking traffic, so only the
   document is parsed per run.
2. **Keep format boundaries to the ones you need.** A script that crosses a boundary pays a
   parse and a serialize per section; put edits together rather than splitting them across
   `convert`s.
3. **Do not carry content the script never reads.** With the base64 content of the real document
   replaced by a placeholder, the same script ran roughly 40 % faster at 14 threads under
   Workstation GC (about 240 against 170 runs/s, measured before the changes below). If a file
   only travels through, consider keeping it beside the document and re-attaching it afterwards.
4. **One execution context per run, never a shared input node** (§1) — sharing is a correctness
   bug first and a contention source second.
5. **Allocation per run** is the lever that helps in both GC modes. TLio converts and serializes
   through pooled buffers and writes XML without building a DOM when no namespaces are
   involved; `BringingInABigDocument_AllocatesABoundedMultipleOfItsSize` keeps that from
   regressing.

---

See also: [overview.md](overview.md) for adapter selection, [TLio_AI_Reference.md](TLio_AI_Reference.md)
for the full command/function reference, and [concurrency-investigation.md](concurrency-investigation.md)
for the complete thread-safety analysis this page summarises.
