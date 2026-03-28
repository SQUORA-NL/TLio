# Research: Sample Projects — API and CLI

**Branch**: `005-samples-api-cli` | **Date**: 2026-03-28

---

## Decision 1: HTTP Framework for API Sample

**Decision**: ASP.NET Core Minimal API (built into .NET 10, no extra package)

**Rationale**: Minimal API requires zero boilerplate beyond `WebApplication.CreateBuilder` + one `MapPost` call per endpoint. This matches the "very basic" requirement exactly. Full MVC or Razor Pages would add unnecessary complexity for a reference sample.

**Alternatives considered**:
- Full MVC (`Microsoft.AspNetCore.Mvc`) — rejected: heavier, controller classes obscure the TLio wiring
- gRPC/SignalR — rejected: wrong protocol for a JSON/XML/YAML REST example

---

## Decision 2: CLI Argument Parsing

**Decision**: Manual positional/named argument parsing using `Environment.GetCommandLineArgs()` — no third-party package

**Rationale**: The CLI sample has exactly two required arguments (`--input`, `--script`) and one optional (`--output`). A full framework like `System.CommandLine` would add a NuGet dependency and obscure the TLio-specific code in the sample. Manual parsing keeps the sample self-contained and readable.

**Alternatives considered**:
- `System.CommandLine` — rejected for this sample: adds package dependency, more code than the transformation logic itself
- `CommandLineParser` — rejected: same reason

---

## Decision 3: Project Layout

**Decision**: `samples/TLio.Sample.Api/` and `samples/TLio.Sample.Cli/` as standalone `.csproj` files added to `TLio.sln`

**Rationale**: Placing samples under a `samples/` top-level directory follows OSS conventions (see dotnet/runtime, ASP.NET Core itself). Each sample is a separate project so it can be `dotnet run`-ed independently. Both reference TLio library projects via project references — no NuGet packaging required for the sample to work.

**Alternatives considered**:
- Single `samples/` project with multiple entry points — rejected: `dotnet run` would be ambiguous
- Placing samples inside `TLio.Client/` — rejected: would pollute the library project

---

## Decision 4: Script Storage in API Sample

**Decision**: Scripts are embedded as `.json` files in a `Scripts/` subfolder within each sample project (`<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`). The API endpoint selects the script based on the format route segment.

**Rationale**: Keeps samples self-contained — no database, no configuration required. A developer clones and runs immediately. Scripts live next to the code that uses them, making the mapping obvious.

**Alternatives considered**:
- Scripts supplied in the HTTP request body alongside data — rejected: conflates transformation script with payload; the spec says "runs a certain script per type"
- Scripts loaded from an external path via config — rejected: adds configuration friction for a sample

---

## Decision 5: Format Detection in CLI

**Decision**: Auto-detect from file extension: `.json` → JSON adapter, `.xml` → XML adapter, `.yaml` / `.yml` → YAML adapter. No explicit `--format` flag for v1.

**Rationale**: Matches spec assumption ("auto-detected from file extension"). Keeps the CLI invocation minimal. An explicit flag is easy to add later but adds friction for the sample.

**Alternatives considered**:
- Require explicit `--format` flag — rejected: unnecessary for a demo, contradicts spec assumption

---

## Decision 6: YAML Inclusion

**Decision**: Include YAML as a full participant in both samples. `TLio.Yaml` is complete (YamlExecutionContext, YamlNodeAdapter, YamlPathItemsFetcher all exist and have passing tests).

**Rationale**: The YAML adapter is present and tested. Excluding it from the samples would give a misleading picture of TLio's capabilities.

**Alternatives considered**:
- JSON + XML only — rejected: YAML adapter is already functional

---

## Decision 7: Sample Scripts Content

**Decision**: Each sample ships with one bundled script per format. The script performs a simple but meaningful transformation: copy a field to a new location (Set/Add command). This demonstrates the engine without requiring domain knowledge.

**Rationale**: Simple scripts keep the focus on the integration pattern (how to wire TLio) rather than on the transformation logic. The bundled scripts must work out-of-the-box with the bundled sample input files.

---

## Decision 8: Article VII Simplicity Gate

**Question**: Could this be done with fewer projects and still satisfy Articles I–V?

**Answer**: No. The API sample needs a web host (can't live in a console project). The CLI needs a console entry point (can't host a web server). Two projects is the minimum. Each is a pure consumer of existing TLio library projects — no new library layer is needed.

**Format Neutrality note**: Sample projects are consumer entry-points, not library code. They are permitted to reference concrete adapter types (`JsonExecutionContext`, `XmlExecutionContext`, `YamlExecutionContext`) directly, just as any consuming application would. Articles I–V apply to `TLio.Core`, `TLio.Commands`, `TLio.Functions` — they are unaffected by these samples.
