# Tlio.claude Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-28 (updated by 004)

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

- 004-core-test-reorganization: Completed — TLio.Json.Tests (114), TLio.Json.SystemText.Tests (33), TLio.Functions.Tests (248) all active; TLio.UnitTests now core/commands/engine only (403 tests)
- 003-xml-xpath-implementation: Adding NativeXPathItemsFetcher + TLio.Xml.Tests + TLio.Yaml.Tests
- 002-migration-from-jlio: Added C# / .NET 10 + Newtonsoft.Json, System.Text.Json, JsonPath.Net (json-everything), NUnit

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
