# Tlio.claude Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-05-01 (updated by 008)

## Active Technologies
- C# / .NET 10 + Newtonsoft.Json (TLio.Json), NUnit (tests) (010-unify-script-notation)
- C# / .NET 10; YAML (GitHub Actions workflows) + MSBuild SDK, GitHub Actions, NuGet.org API (011-nuget-packaging)
- C# / .NET 10 + NuPlane 0.0.1 (NuGet hot-loading) + CShells 0.0.14 (modular host) + Docker (012-docker-plugin-api)
- Filesystem only (`/plugins` volume mount); no database (012-docker-plugin-api)
- C# / .NET 10 + `System.Text.Json` (in-box), `JsonCons.JsonPath` 1.1.0 (existing in TLio.Json.SystemText), NUnit (tests) (013-parse-once-stj-optimize)
- C# / .NET 10 + Docker, MSBuild SDK, `Directory.Build.props` (global MSBuild properties) (main)
- C# / .NET 10 + NUnit 4.x, Newtonsoft.Json (TLio.Json.Tests), System.Text.Json (TLio.Json.SystemText.Tests), YamlDotNet (TLio.Yaml.Tests), System.Xml (TLio.Xml.Tests) (015-expand-test-coverage)
- N/A — test fixtures are file-based (input/script/result triplets) or programmatically generated (015-expand-test-coverage)
- C# / .NET 10 + NUnit 4.x, Newtonsoft.Json (JToken), TLio.Extensions.Text, TLio.Extensions.ETL, TLio.Extensions.Math, TLio.Functions (016-deepen-test-coverage)
- [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION] + [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION] (017-im-format-converter)
- [if applicable, e.g., PostgreSQL, CoreData, files or N/A] (017-im-format-converter)
- C# / .NET 10 + `System.Text.Json` (built-in, JSON adapter), `System.Xml` (built-in, XML adapter), `YamlDotNet` (YAML adapter only — MIT licensed), `NUnit 4.x` (tests) (017-im-format-converter)
- N/A — pure in-memory library (017-im-format-converter)
- C# / .NET 10 + `System.Text.Json` (built-in), `System.Xml` (built-in), `YamlDotNet` (MIT, YAML adapter only), `NUnit 4.x` (tests), `TLio.Core` (FormatConverter.TLio only) (017-im-format-converter)
- C# / .NET 10 + ASP.NET Core Minimal API; `TLio.Client` (`ScriptEngine<TNode>`, `CompiledScript<TNode>`); `TLio.Json` (`JsonExecutionContext`, `JsonNodeAdapter`); `TLio.Xml` (`XmlExecutionContext`); `TLio.Yaml` (`YamlExecutionContext`); NuPlane + CShells (DockerPlugin host only) (018-api-script-slug-cache)
- In-memory `ConcurrentDictionary<string, ScriptRegistryEntry>` — ephemeral, process-scoped (018-api-script-slug-cache)
- C# / .NET 10 + `ModelContextProtocol` (Anthropic MCP SDK, stdio server), `System.Threading.RateLimiting` (in-box .NET), `TLio.Json`, `TLio.Json.SystemText`, `TLio.Xml`, `TLio.Yaml`, `TLio.Client`, `TLio.Commands`, `TLio.Functions`, `TLio.Extensions.*` (019-mcp-tlio-server)
- N/A — stateless, in-memory execution per request; configuration from `appsettings.json` / environment variables (019-mcp-tlio-server)

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
TLio.Xml/           ← XmlNodeAdapter, SlashPathItemsFetcher, NativeXPathItemsFetcher (003)
TLio.Yaml/          ← YamlNodeAdapter, YamlPathItemsFetcher
TLio.UnitTests/             ← Core / Commands / Engine tests only (no functions, no JSON adapter)
TLio.Json.Tests/            ← JSON (Newtonsoft) adapter tests (JsonNodeAdapter, JsonPathItemsFetcher)
TLio.Json.SystemText.Tests/ ← System.Text.Json adapter fixture tests
TLio.Functions.Tests/       ← Built-in function tests + extension-pack fixture tests (Math, Text, TimeDate, ETL, TextPack)
TLio.Extensions.Text/      ← Optional text function pack: concat, toString, parse, format, length, substring, replace, toLower, toUpper, trim (008)
TLio.Xml.Tests/             ← XML adapter tests, SlashPath + NativeXPath fixtures
TLio.Yaml.Tests/            ← YAML adapter tests and fixtures
TLio.Mcp/                   ← MCP stdio server (tlio_list_commands, tlio_describe, tlio_execute, tlio_analyze, 019)
TLio.Mcp.Tests/             ← MCP server tests (DiscoveryTools, ExecutionTools, AnalysisTools, E2E workflow)
TLio.Parity.Tests/          ← Cross-format regression net (020): one fixture corpus run against
                              JSON, XML and YAML through each format's own script notation
samples/
  TLio.Sample.Api/          ← Minimal API sample (JSON/XML/YAML endpoints, 005)
  TLio.Sample.Cli/          ← CLI sample (file-in / transformed-out, 005)
  TLio.Sample.DockerPlugin/ ← Docker API with NuPlane hot-loading of .nupkg plugins (012)
specs/
```

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

`TLio.Parity.Tests` fails when a format drifts. Known, deliberate divergences are section E of
`docs/behaviour-decisions.md`.

## Commands

```sh
dotnet build
dotnet test
```

## Code Style

C# / .NET 10: Follow standard conventions

## Recent Changes
- 020-xml-alignment: XML and YAML brought onto the JSON data model — canonical document shape,
  array/object split, script-notation parity (bools, enums, nested scripts, settings), YAML
  recursive descent, format-aware path detection in functions. Added `TLio.Parity.Tests`.
- 019-mcp-tlio-server: Added C# / .NET 10 + `ModelContextProtocol` (Anthropic MCP SDK, stdio server), `System.Threading.RateLimiting` (in-box .NET), `TLio.Json`, `TLio.Json.SystemText`, `TLio.Xml`, `TLio.Yaml`, `TLio.Client`, `TLio.Commands`, `TLio.Functions`, `TLio.Extensions.*`
- 018-api-script-slug-cache: Added C# / .NET 10 + ASP.NET Core Minimal API; `TLio.Client` (`ScriptEngine<TNode>`, `CompiledScript<TNode>`); `TLio.Json` (`JsonExecutionContext`, `JsonNodeAdapter`); `TLio.Xml` (`XmlExecutionContext`); `TLio.Yaml` (`YamlExecutionContext`); NuPlane + CShells (DockerPlugin host only)
- 017-im-format-converter: Added C# / .NET 10 + `System.Text.Json` (built-in), `System.Xml` (built-in), `YamlDotNet` (MIT, YAML adapter only), `NUnit 4.x` (tests), `TLio.Core` (FormatConverter.TLio only)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
