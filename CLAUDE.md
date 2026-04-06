# Tlio.claude Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-06 (updated by 006)

## Active Technologies

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
TLio.Functions.Tests/       ← Built-in function tests + extension-pack fixture tests (Math, Text, TimeDate, ETL)
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

- 006-yaml-multidoc-array: Updating YamlNodeAdapter.Parse() to treat multi-document YAML inputs as array roots (parity with JSON array root)
- 005-samples-api-cli: Adding samples/TLio.Sample.Api (Minimal API, 3 format endpoints) + samples/TLio.Sample.Cli (file transform CLI), both under samples/
- 004-core-test-reorganization: Completed — TLio.Json.Tests (114), TLio.Json.SystemText.Tests (33), TLio.Functions.Tests (248) all active; TLio.UnitTests now core/commands/engine only (403 tests)
- 003-xml-xpath-implementation: Adding NativeXPathItemsFetcher + TLio.Xml.Tests + TLio.Yaml.Tests

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
