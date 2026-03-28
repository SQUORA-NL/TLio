# Tlio.claude Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-28

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
TLio.UnitTests/     ← Core / Commands / Functions / JSON tests only (XML+YAML moved in 003)
TLio.Xml.Tests/     ← XML adapter tests, SlashPath + NativeXPath fixtures (003, planned)
TLio.Yaml.Tests/    ← YAML adapter tests and fixtures (003, planned)
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

- 003-xml-xpath-implementation: Adding NativeXPathItemsFetcher + TLio.Xml.Tests + TLio.Yaml.Tests
- 002-migration-from-jlio: Added C# / .NET 10 + Newtonsoft.Json, System.Text.Json, JsonPath.Net (json-everything), NUnit

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
