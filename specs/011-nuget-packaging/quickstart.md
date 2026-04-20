# Quickstart: NuGet Package Deployment (011)

## How Packages Are Published

### Preview (automatic — every `main` push)

Every merge to `main` triggers a preview publish. Packages are versioned `0.1.0-preview.{N}` where `N` is the GitHub Actions run number.

Install a preview package:

```sh
dotnet add package TLio.Json --prerelease
```

### Stable Release (on git tag)

Push a semver tag to trigger a release:

```sh
git tag v1.0.0
git push origin v1.0.0
```

The pipeline extracts `1.0.0` from the tag and publishes all 12 packages with that version.

## Consuming TLio from NuGet

### Minimal setup (Newtonsoft JSON)

```sh
dotnet add package TLio.Json
```

This automatically pulls in `TLio.Core`, `TLio.Commands`, and `TLio.Functions` as transitive dependencies.

### With extensions

```sh
dotnet add package TLio.Json
dotnet add package TLio.Extensions.Text
dotnet add package TLio.Extensions.Math
```

### YAML

```sh
dotnet add package TLio.Yaml
```

## Package Family

| Package | Purpose |
|---------|---------|
| `TLio.Core` | Contracts and models (no format dependency) |
| `TLio.Commands` | Built-in commands (set, add, remove, copy, …) |
| `TLio.Functions` | Built-in functions (fetch, indirect, partial, …) |
| `TLio.Client` | Script engine and parse options |
| `TLio.Json` | Newtonsoft.Json adapter |
| `TLio.Json.SystemText` | System.Text.Json adapter (RFC 9535) |
| `TLio.Xml` | XML adapter (slash-path and XPath) |
| `TLio.Yaml` | YAML adapter |
| `TLio.Extensions.ETL` | ETL commands (flatten, restore, resolve, tocsv) |
| `TLio.Extensions.Math` | Math functions |
| `TLio.Extensions.Text` | Text/string functions |
| `TLio.Extensions.TimeDate` | Date/time functions |

## Required Secret

Add a NuGet API key as a GitHub Actions secret named `NUGET_API_KEY` in the repository settings before the first publish run.
