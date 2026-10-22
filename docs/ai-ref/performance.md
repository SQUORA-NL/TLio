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

## 5. Documents with large strings, and the garbage collector

A document that carries a few very large string values — base64-encoded files of a few hundred KB
each is the case that was measured — does **not** make individual commands slower. A command
touches the nodes its path selects, never the whole tree, so `put`, `copy`, `fetch`, wildcards and
functions cost the same on a document with 900 KB of base64 as on one without
(`BigObjectPerformanceTests` in `TLio.Parity.Tests` asserts this for every command and function
class, on Newtonsoft and System.Text.Json).

What grows with the strings is the work *around* the commands: parsing the document, converting
between formats, and serializing the result. Each of those allocates a string about the size of
the document, and a string over ~85 KB goes on the large object heap, which is only reclaimed by
a full (gen 2) collection. With many executions running at once those collections pause every
thread. So the symptom is that *everything* gets slower under load, with no single command to
blame.

**What to do, in order:**

1. **Parse once per run.** A script that crosses a format boundary pays one parse and one
   serialize per section; put edits together rather than splitting them across `convert`s.
2. **Compile the script once** (§2), so only the document is parsed per run.
3. **Choose the GC mode by measuring — Server GC is not always the faster one.** Workstation GC is
   the default for a library host; Server GC gives each core its own heap and collects in
   parallel. Measured with the harness below on a 14-core Apple M4 Pro, 14 threads, Release:

   | Workload | Workstation GC | Server GC |
   |---|---|---|
   | A real 69-command script, JSON → XML, 600 KB input with base64 files | **304 runs/s** (69% of wall time paused; slower than one thread: 348) | **1 803 runs/s** (12% paused) |
   | Only `convert` + a few edits on a 900 KB XML order (14.5 MB allocated per run) | **1 507 runs/s** | 963–1 266 runs/s |

   A script with many commands produces a lot of small garbage; under Workstation GC every thread
   shares one heap and one collector, and that is where the time goes, so Server GC helps a great
   deal. A run that is mostly one big parse-convert-serialize is bound by memory traffic, and
   there Server GC's larger allocation budgets did not pay. Your document, script and core mix
   decide — run the harness on yours. Numbers move with the machine; the *shape* of the
   difference is the useful part.
4. **Allocation per run is the lever that helps in both modes.** TLio converts and serializes
   through pooled buffers and writes XML without building a DOM when no namespaces are involved;
   `BringingInABigDocument_AllocatesABoundedMultipleOfItsSize` keeps that from regressing.

**Turning Server GC on** (it is a process-wide setting of the host, not of TLio):

```xml
<!-- the host's .csproj -->
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
</PropertyGroup>
```

or `DOTNET_gcServer=1` in the environment, or `"System.GC.Server": true` in
`runtimeconfig.json`. In a container, Server GC sizes itself from the CPU limit the container
sees; check it after changing the limit. It also uses more memory than Workstation GC.

**Measuring it on your own document and script**

```sh
# the built-in order: 3 x 300 000 base64 characters, convert + a few edits
dotnet run -c Release --project TLio.Benchmarks -- --big-document

# your script and document (any notation, any format)
dotnet run -c Release --project TLio.Benchmarks -- --big-document \
    --script my-script.json --input my-input.json --from json --seconds 20
```

It starts one process per GC mode (so the mode is really the one printed), once on a single thread
and once on every core (`--threads N` to change), and prints runs per second, gen 2 collections,
the share of wall time spent paused in GC, and megabytes allocated per run. A large gen 2 count
with a high paused share is the signature described above. Start from
`TLio.Benchmarks/BigDocumentGcComparison.cs`.

---

See also: [overview.md](overview.md) for adapter selection, [TLio_AI_Reference.md](TLio_AI_Reference.md)
for the full command/function reference, and [concurrency-investigation.md](concurrency-investigation.md)
for the complete thread-safety analysis this page summarises.
