# Tlio.claude Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-20 (updated by 008)

## Active Technologies
- C# / .NET 10 + Newtonsoft.Json (TLio.Json), NUnit (tests) (010-unify-script-notation)
- C# / .NET 10; YAML (GitHub Actions workflows) + MSBuild SDK, GitHub Actions, NuGet.org API (011-nuget-packaging)

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
- 011-nuget-packaging: Added C# / .NET 10; YAML (GitHub Actions workflows) + MSBuild SDK, GitHub Actions, NuGet.org API
- 010-unify-script-notation: Added C# / .NET 10 + Newtonsoft.Json (TLio.Json), NUnit (tests)

- 008-jlio-api-parity: Adding TLio.Extensions.Text (12 text functions); fluent builder API + TLioConvert in TLio.Client; alias properties in Compare/Merge; ETL settings deserialization fix; newGuid function; fetch/promote optional args; path alias; ai-ref.md updates

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
