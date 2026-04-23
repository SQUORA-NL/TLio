# Tlio.claude Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-23 (updated by 008)

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
samples/
  TLio.Sample.Api/          ← Minimal API sample (JSON/XML/YAML endpoints, 005)
  TLio.Sample.Cli/          ← CLI sample (file-in / transformed-out, 005)
  TLio.Sample.DockerPlugin/ ← Docker API with NuPlane hot-loading of .nupkg plugins (012)
specs/
```

## XML Path Formats (TLio.Xml)

| Fetcher | Class | Path style | Root |
|---|---|---|---|
| Slash-path (existing) | `SlashPathItemsFetcher` | `/address/city` | `/` |
| Native XPath (003) | `NativeXPathItemsFetcher` | `address/city`, `//name`, `item[@id='1']` | `.` |

Choose via `XmlExecutionContext.CreateWithSlashPaths()` or `CreateWithNativeXPath()`.

## Commands

```sh
dotnet build
dotnet test
```

## Code Style

C# / .NET 10: Follow standard conventions

## Recent Changes
- 016-deepen-test-coverage: Added C# / .NET 10 + NUnit 4.x, Newtonsoft.Json (JToken), TLio.Extensions.Text, TLio.Extensions.ETL, TLio.Extensions.Math, TLio.Functions
- 015-expand-test-coverage: Added C# / .NET 10 + NUnit 4.x, Newtonsoft.Json (TLio.Json.Tests), System.Text.Json (TLio.Json.SystemText.Tests), YamlDotNet (TLio.Yaml.Tests), System.Xml (TLio.Xml.Tests)
- main: Added C# / .NET 10 + Docker, MSBuild SDK, `Directory.Build.props` (global MSBuild properties)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
