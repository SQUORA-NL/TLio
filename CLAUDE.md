# Tlio.claude Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-09-29

## Active Technologies
- C# / .NET 10 + Newtonsoft.Json (TLio.Json), NUnit (tests) (010-unify-script-notation)
- C# / .NET 10; YAML (GitHub Actions workflows) + MSBuild SDK, GitHub Actions, NuGet.org API (011-nuget-packaging)
- C# / .NET 10 + NuPlane 0.0.1 (NuGet hot-loading) + CShells 0.0.14 (modular host) + Docker (012-docker-plugin-api)
- Filesystem only (`/plugins` volume mount); no database (012-docker-plugin-api)
- C# / .NET 10 + `System.Text.Json` (in-box), `TLio.JsonPath` (own engine, no third-party deps), NUnit (tests) (013-parse-once-stj-optimize, superseded by feature/jsonpath-in-system-text-json)
- C# / .NET 10 + Docker, MSBuild SDK, `Directory.Build.props` (global MSBuild properties) (main)
- C# / .NET 10 + NUnit 4.x, Newtonsoft.Json (TLio.Json.Tests), System.Text.Json (TLio.Json.SystemText.Tests), YamlDotNet (TLio.Yaml.Tests), System.Xml (TLio.Xml.Tests) (015-expand-test-coverage)
- N/A — test fixtures are file-based (input/script/result triplets) or programmatically generated (015-expand-test-coverage)
- C# / .NET 10 + NUnit 4.x, Newtonsoft.Json (JToken), TLio.Extensions.Text, TLio.Extensions.ETL, TLio.Extensions.Math, TLio.Functions (016-deepen-test-coverage)
- C# / .NET 10 + `System.Text.Json` (built-in, JSON adapter), `System.Xml` (built-in, XML adapter), `YamlDotNet` (YAML adapter only — MIT licensed), `NUnit 4.x` (tests) (017-im-format-converter)
- N/A — pure in-memory library (017-im-format-converter)
- C# / .NET 10 + `System.Text.Json` (built-in), `System.Xml` (built-in), `YamlDotNet` (MIT, YAML adapter only), `NUnit 4.x` (tests), `TLio.Core` (TLio.FormatConverter only) (017-im-format-converter)
- C# / .NET 10 + ASP.NET Core Minimal API; `TLio.Client` (`ScriptEngine<TNode>`, `CompiledScript<TNode>`); `TLio.Json` (`JsonExecutionContext`, `JsonNodeAdapter`); `TLio.Xml` (`XmlExecutionContext`); `TLio.Yaml` (`YamlExecutionContext`); NuPlane + CShells (DockerPlugin host only) (018-api-script-slug-cache)
- In-memory `ConcurrentDictionary<string, ScriptRegistryEntry>` — ephemeral, process-scoped (018-api-script-slug-cache)
- C# / .NET 10 + `ModelContextProtocol` (Anthropic MCP SDK, stdio server), `System.Threading.RateLimiting` (in-box .NET), `TLio.Json`, `TLio.Json.SystemText`, `TLio.Xml`, `TLio.Yaml`, `TLio.Client`, `TLio.Commands`, `TLio.Functions`, `TLio.Extensions.*` (019-mcp-tlio-server)
- N/A — stateless, in-memory execution per request; configuration from `appsettings.json` / environment variables (019-mcp-tlio-server)
- C# / .NET 10 + `TLio.Core`, `TLio.Commands` (`TLio.Extensions.Looping`: `forEach`/`while`); `TLio.Extensions.TimeDate` (`dayCountFraction`); ASP.NET Core Minimal API sample (`samples/TLio.Sample.Actus.Api`) (feature/actus-pam-contract)

- C# / .NET 10 + Newtonsoft.Json, System.Text.Json, JsonPath.Net (json-everything), NUnit (002-migration-from-jlio)
- TLio.Xml: XmlNodeAdapter + SlashPathItemsFetcher (existing) + NativeXPathItemsFetcher (003, planned)
- TLio.Yaml: YamlNodeAdapter + YamlPathItemsFetcher (dot-notation)

## Project Structure

```text
TLio.Core/
TLio.Commands/
TLio.Functions/
TLio.Client/
TLio.Json/
TLio.Json.SystemText/
TLio.JsonPath/              ← standalone JSONPath engine for JsonNode (no TLio/Newtonsoft deps): Newtonsoft, Rfc9535, Extended dialects
TLio.Xml/           ← XmlNodeAdapter, SlashPathItemsFetcher, NativeXPathItemsFetcher (003)
TLio.Yaml/          ← YamlNodeAdapter, YamlPathItemsFetcher
TLio.UnitTests/             ← Core / Commands / Engine tests only (no functions, no JSON adapter)
TLio.Json.Tests/            ← JSON (Newtonsoft) adapter tests (JsonNodeAdapter, JsonPathItemsFetcher)
TLio.Json.SystemText.Tests/ ← System.Text.Json adapter fixture tests
TLio.JsonPath.Tests/        ← RFC 9535 compliance suite (pinned), Newtonsoft differential oracle tests, divergence table, API/limits
TLio.Json.AdapterParity.Tests/ ← every fixture + the sweep through BOTH JSON adapters; outputs must be identical
TLio.JsonPath.Benchmarks/   ← BenchmarkDotNet: engine vs Newtonsoft vs the old JsonCons strategy
TLio.Functions.Tests/       ← Built-in function tests + extension-pack fixture tests (Math, Text, TimeDate, ETL, TextPack)
TLio.Extensions.Text/      ← Optional text function pack: concat, toString, parse, format, length, substring, replace, toLower, toUpper, trim (008), regexReplace, regexExtract, right (023)
TLio.Functions/Collections/ ← distinct, sort, sortBy, last — built in, registered by ParseOptions (023)
TLio.Extensions.Looping/   ← forEach, while — the loop primitives, opt-in via RegisterLooping (feature/actus-pam-contract)
TLio.Xml.Tests/             ← XML adapter tests, SlashPath + NativeXPath fixtures
TLio.Yaml.Tests/            ← YAML adapter tests and fixtures
TLio.Mcp/                   ← MCP stdio server (tlio_list_commands, tlio_describe, tlio_execute, tlio_analyze, 019)
TLio.Mcp.Tests/             ← MCP server tests (DiscoveryTools, ExecutionTools, AnalysisTools, E2E workflow)
TLio.Parity.Tests/          ← Cross-format regression net (020): one fixture corpus run against
                              JSON, XML and YAML through each format's own script notation
TLio.FormatConverter/       ← Format conversion (017, 022, 024). One assembly, one package.
  Core/                       IM model, ConversionSettings, MetadataConvention
  {Json,Xml,Yaml}/            the three adapters
  Commands/                   convert, convertValue, MultiFormatScriptRunner,
                              ScriptEngineSectionExecutor
TLio.FormatConverter.Tests/  ← adapters, round trips, mid-script convert, canonical shape
TLio.Benchmarks/            ← BenchmarkDotNet console app: statistically-rigorous throughput,
                              allocation, and core-utilization benchmarks. Not a test project —
                              `dotnet test` never touches it, `dotnet run` does.
docs/                       ← reference docs (ai-ref/), behaviour decisions, versioning
specs/
```

Samples are **not** in this repository. The sample applications, `docs/samples`,
`docs/showcase` and the Azure demo live in
[TLio-Samples](https://github.com/SQUORA-NL/TLio-Samples), which references TLio only as NuGet
packages (floating to the newest 1.x, previews included) and runs nightly against whatever was
published last. A change here reaches the samples by being published — every push to `main`
publishes a preview — never by a project reference. When a change legitimately moves a
committed sample output, update it there after the preview is out.

## XML Path Formats (TLio.Xml)

Both fetchers anchor on the **document node**, exactly as XPath defines it: `/` is the
document node (not an element — it selects nothing), `/order` is the document element, and
`/order/customer` a child of it. **The document element is always named in the path.**
A bare `customer` is `child::customer` of the document node and matches nothing — relative
steps never skip a level; use `//customer` for "at any depth".

| Fetcher | Class | Path style | Supports |
|---|---|---|---|
| Slash-path | `SlashPathItemsFetcher` | `/order/address/city` | simple hierarchies, `*`, `//` |
| Native XPath | `NativeXPathItemsFetcher` | `/order/address/city`, `//name`, `/order/item[@id='1']` | full XPath 1.0 |

Choose via `XmlExecutionContext.CreateWithSlashPaths()` or `CreateWithNativeXPath()`.

`XmlNodeAdapter.Parse` returns an element still attached to its `XDocument` — that document
node is what makes absolute paths resolve. Any `XElement` handed to the engine must be a
document element; a hand-built detached `XElement` will not resolve absolute paths.

Renaming the document element (`<order>` → `<opdracht>`) is the `rename` command; `move` with
`toPath: "/"` replaces the document body but keeps the element's name.

## Cross-format behaviour (020)

Commands are written against the JSON data model; each adapter answers "which kind of node is
this?" for its format. That mapping — object / array / scalar / null in JSON, XML and YAML — is
`docs/ai-ref/adapters/document-shape.md`, and the three script notations are
`docs/ai-ref/adapters/script-notation.md`.

Two rules to keep in mind when touching an adapter:

- An XML **array** is an element whose children share one name, with either more than one child
  or the canonical item name `item`. `IsObject` and `IsArray` are mutually exclusive.
- An **empty XML element** is null, `""`, `{}` and `[]` at once. It reads as null *and* as a
  container that a property can be written into — that second answer is what makes deep-path
  `add` work.

`TLio.Parity.Tests` fails when a format drifts. It holds two things:

- **Fixtures** — one corpus written in JSON, run against all three formats through each
  format's own script notation.
- **The sweep** — every registered command and every registered function, run from an empty
  document, as a script someone would actually write. Two files, because there are two path
  languages: `Sweep/sweep.json` drives JSON *and* YAML (they share the path language, and JSON
  is a subset of YAML, so the same text is read by both), and `Sweep/sweep.xml` is written in
  XPath. Change one, change the other — `SweepTests.EveryRegistered*IsExercised` runs against
  both. After changing either, re-record with
  `dotnet test TLio.Parity.Tests --filter "Name~RecordSweep"` and read the diff.

Known, deliberate divergences are section E of `docs/behaviour-decisions.md`.

## Target frameworks

All libraries and tests build for `net8.0;net9.0;net10.0` (set once in `Directory.Build.props`); applications are `net10.0`. New runtimes
are added when they ship, old ones dropped only in a major version, conditionals use `NETx_0_OR_GREATER`, and every target is tested on its own
runtime. Details and the rules: `docs/target-frameworks.md`.

## Commands

```sh
dotnet build
dotnet test
```

## Performance testing (concurrency, leakage, load, memory)

Three different concerns, three different places — none of them run in CI by default except the
one that's cheap enough to:

- **Data-leakage / thread-safety regression checks** — `ConcurrentExecutionTests`
  (`TLio.UnitTests/CommandsTests/LoopingTests/`) and `CompiledScript_ConcurrencyTests`
  (`TLio.Json.SystemText.Tests/CompiledScriptTests/`) force real concurrency (`Parallel.For`) over
  a shared `CompiledScript<TNode>` and assert no cross-execution corruption. `[Explicit]` — CI
  runners can behave differently under forced concurrency than a developer machine, so these are
  not gated on every push, but run by CI's `release-gate` job on every PR opened from a
  `release/*` branch (and via *Run workflow* on CI); locally:
  `dotnet test TLio.UnitTests -c Release --filter "FullyQualifiedName~ConcurrentExecutionTests"`.
- **Memory-growth regression check** — `Looping_MemoryLeakTests`
  (`TLio.UnitTests/Performance/`) runs a shared compiled script thousands of times in batches,
  forces a full GC after each, and asserts the managed heap doesn't scale with execution count.
  Not `[Explicit]` — cheap and deterministic enough to run in CI every time.
- **Load/throughput benchmarks** — `Looping_PortfolioBenchmarkTests`
  (`TLio.UnitTests/Performance/`), Stopwatch-based, `[Explicit]`, up to 100k documents,
  sequential and parallel. Single-sample numbers: useful for a quick order-of-magnitude read, not
  for judging small deltas or diagnosing *why* a number is what it is.
- **Statistically-rigorous benchmarks with root-cause diagnostics** — `TLio.Benchmarks`
  (BenchmarkDotNet). Same portfolio workload as `Looping_PortfolioBenchmarkTests`, but multiple
  iterations with warmup (Mean/Error/StdDev instead of one sample), `[MemoryDiagnoser]`, and a
  hand-rolled measurement of `Monitor.LockContentionCount` and CPU-time/wall-time ("effective
  cores used") around each parallel benchmark — BenchmarkDotNet's own `[ThreadingDiagnoser]`
  refuses to run against `net10.0` on the BenchmarkDotNet version this project pins, so lock
  contention is measured by hand instead. Also runs a synthetic, lock-free control workload
  (comparable per-item work, zero TLio, zero shared state) alongside the real one: its own
  parallel speedup is *this machine's actual achievable ceiling* — the number to judge TLio's
  parallel speedup against, not a theoretical `Environment.ProcessorCount`x (on a 14-core Apple M4
  Pro, that ceiling is ~6x, not ~14x, because P-cores and E-cores aren't equal; TLio lands around
  80% of it, with zero measured lock contention). `<ServerGarbageCollection>true</ServerGarbageCollection>`
  is set in the `.csproj` rather than left to `DOTNET_gcServer` — BenchmarkDotNet launches each
  job as its own child process, which doesn't reliably inherit an env var from the launching
  shell. Run: `dotnet run -c Release --project TLio.Benchmarks -- --filter '*'`.

## Versioning

The git tag is the version. No version number is written down in this repository — MinVer reads
the nearest `v*` tag at build time and stamps it on the assemblies *and* the packages, so a
local `dotnet pack` produces exactly what the pipeline produces. Never add a `VersionPrefix`,
`<Version>` or a `/p:Version=` to a build: that is how `0.1.0-preview.N` packages came to be
published for months after `v0.8.0` was released.

- Untagged commit on `main` → next **minor** of the last release tag plus the commit height,
  e.g. `0.9.0-preview.3`.
- Tag `vX.Y.Z` → exactly `X.Y.Z`.

Cut a release with the **Release** workflow (patch / minor / major / exact), or by hand with
`git tag vX.Y.Z && git push origin vX.Y.Z` — the two are equivalent. Steering the previews
toward a major or a patch is done by pushing a pre-release tag (`v2.0.0-alpha.1`), not by
editing a number. Full rules: `docs/versioning.md`.

Anything that builds this repo in CI needs `fetch-depth: 0`; a shallow checkout has no tags and
MinVer falls back to `0.0.0-alpha.0`.

`AssemblyVersion` stays major-only (`0.9.0` → `0.0.0.0`) and must not be widened to
major.minor. `TLio.Sample.DockerPlugin` (TLio-Samples) hot-loads extension packs at runtime and the CLR binds
them by AssemblyVersion — moving it on every minor breaks every plugin already in the wild.
`Directory.Build.targets` says so at the point of temptation.

## Strong naming

Every assembly is signed with `tlio.snk`, committed at the repo root. Strong naming here is an
identity mechanism, not a security one: NuPlane's shared-assembly policy is keyed on
`(name, publicKeyToken, majorVersion)` and refuses a token that is not 16 hex characters, so an
unsigned TLio.Core cannot be shared with a plugin's load context. Public key and token live once
in `Directory.Build.props` as `TlioPublicKey` / `TlioPublicKeyToken`; `InternalsVisibleTo` is
declared in the two csproj files that need it so it can interpolate the key.

Consequence for consumers: assembly identity changed, so anything compiled against an unsigned
TLio build must be recompiled. Pre-1.0, that is the moment to do it.

## Code Style

C# / .NET 10: Follow standard conventions

## Script notation (021)

A script can be written in JSON, XML or YAML, whichever format the *document* is in — only the
paths inside it have to speak the document's path language. `ScriptEngine` reads JSON on its
own; the other two register in one line, because their parsers ship in the format packages,
which `TLio.Client` cannot reference without a cycle:

```csharp
var engine = new ScriptEngine<JToken>(commands, functions).UseXmlScripts().UseYamlScripts();
engine.Execute(scriptText, data, context);   // notation detected from the text
```

Any command in any notation may carry an optional `title` and `description` — free text about
the step, never read during execution. They live on `CommandBase`, so every command has them and
each parser binds them like any other string property. XML is the one exception worth
remembering: there they are **attributes only**, because a `<title>` child element is part of the
value being written. `CommandDocumentationTests` in `TLio.Parity.Tests` holds both halves.

`ScriptFormatDetector` decides from the first meaningful character: `<` XML, `[`/`{` JSON,
anything else YAML. All three parsers implement `IScriptParser<TNode>`.

The rule that keeps them honest: a structured value is converted to JSON and rebuilt through
`CommandConverter`, never handed to `INodeAdapter.Parse` as the notation's own text — the
adapter's format is the format of the *data*. `ScriptNotationTests` in `TLio.Parity.Tests`
holds every notation against every document format.

## Format conversion (022)

Two commands, and the difference between them is what changes format:

- **`convert`** changes the *whole document*. That means a different `TNode`, which no
  `ICommand<TNode>` can return, so it is not something the engine can run — `MultiFormatScriptRunner`
  splits the script at each boundary and re-hosts each section on the engine for that format via
  `ScriptEngineSectionExecutor<TNode>`. Run on a bare engine it fails and says so.
  `MultiFormatScriptRunner.CrossesAFormatBoundary(script)` is how a host decides which path to take;
  the MCP server and the CLI sample both use it.
- **`convertValue`** changes *one value* at a path. The document's format never changes, so it is
  an ordinary command on the ordinary engine. This is the embedded-payload case.

Paths after a `convert` speak the new format's path language — that is inherent, not a wart.

**Conversion output is the canonical document shape**, not a second one. That matters only because
`convert` works mid-script: the commands after it address the tree that
`docs/ai-ref/adapters/document-shape.md` describes, so the converter has to write it.
`CanonicalShapeTests` walks converted output with TLio's own `XmlNodeAdapter` and `YamlNodeAdapter`
and fails when the two drift. The old repeated-sibling XML shape survives as
`arrayHandling: "repeated"`; it is not the default because in that shape the *parent* element reads
as the array.

Settings exist where XML leaves the answer open — `attributePrefix`, `textProperty`,
`namespacePrefix`, `arrayItemName`, `arrayHandling`, `nullRepresentation`, `nameSanitization`,
`inferTypes`, `cdataAsText`, `flattenAnchors`. `MetadataConvention` in `TLio.FormatConverter.Core` holds
the rules JSON and YAML must spell identically; when they were separate, an attribute that survived
`xml → json` vanished on `xml → yaml`.

One asymmetry to know: TLio's XML adapter ignores attributes by design, but after a `convert` to
JSON or YAML they are ordinary `@name` properties. Converting is how a script edits an attribute.

## Recent Changes

The full log, including the design notes behind each entry (the `@` resolution and its
concurrency bug, `setProperties`, the format converter), is in `docs/history.md`. Highlights:

- jsonpath-in-system-text-json: `TLio.JsonPath` — an own JSONPath engine on `JsonNode` replacing JsonCons.
  Default dialect is Newtonsoft's, held to Newtonsoft.Json 13.0.4 by differential tests; RFC 9535 passes the
  pinned compliance suite. See `docs/adr/0001-tlio-owned-jsonpath-engine.md`.

- Repo hygiene + CI: nullable warnings are errors, `-warnaserror` in CI, macOS/Windows/nl-NL
  runs, coverage collection, and the `[Explicit]` concurrency checks gate every `release/*` PR.
- split-samples: samples, `docs/samples`, `docs/showcase` and `demo/` live in TLio-Samples.
- feature/actus-pam-contract: `TLio.Extensions.Looping` (`forEach`/`while`), `CurrentNode`.
  **Rule that came out of it:** a compiled script's commands are shared, concurrently-executed
  singletons — request-scoped state belongs on `IExecutionContext<TNode>`, never on a mutable
  property of the command, even temporarily.
- 023-function-gaps, 024-package-format-converter, 022-format-convert-command,
  021-script-notation-parsers, 020-xml-alignment: see `docs/history.md`. Specs 020+ have no
  `specs/` folder; `docs/history.md` and `docs/` are their record.
