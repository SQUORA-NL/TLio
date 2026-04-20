# Data Model: NuGet Package Deployment (011)

## Package Dependency Graph

```
TLio.Core                (no TLio deps)
├── TLio.Commands        → Core
├── TLio.Functions       → Core
├── TLio.Json            → Core
├── TLio.Json.SystemText → Core
├── TLio.Extensions.ETL  → Core, Commands
├── TLio.Extensions.Math → Core
├── TLio.Extensions.Text → Core
├── TLio.Extensions.TimeDate → Core
└── TLio.Client          → Core, Commands, Functions
    ├── TLio.Xml         → Core, Client
    └── TLio.Yaml        → Core, Client
```

## Version Number Anatomy

| Scenario | VersionPrefix | VersionSuffix | Resulting NuGet version |
|----------|--------------|--------------|------------------------|
| Main push (preview) | `0.1.0` | `preview.{run_number}` | `0.1.0-preview.42` |
| Tag `v1.0.0` (release) | _(overridden)_ | _(none)_ | `1.0.0` |
| Tag `v1.2.3-beta` (pre-tag) | _(overridden)_ | _(none)_ | `1.2.3-beta` |

All 12 packages in a single pipeline run receive the **same version number**.

## Shared Package Metadata (Directory.Build.props)

| Property | Value |
|----------|-------|
| Authors | `Frans van Ek` |
| PackageLicenseExpression | `MIT` |
| RepositoryUrl | `https://github.com/FransVanEk/TLio` |
| RepositoryType | `git` |
| PackageProjectUrl | `https://github.com/FransVanEk/TLio` |
| IsPackable | `false` (default; library projects override to `true`) |
| VersionPrefix | `0.1.0` |

## Per-Project Package Metadata

Each library project adds only `IsPackable=true` and optionally a `PackageTags` value.
`Description` is already present in every `.csproj`. No duplication of shared fields.

## Pipeline Triggers

| Event | Workflow | Jobs | Version produced |
|-------|----------|------|-----------------|
| Push to any branch | `ci.yml` | `test` | n/a |
| PR to `main` | `ci.yml` | `test` | n/a |
| Push to `main` | `publish.yml` | `test` → `publish` | `0.1.0-preview.{run}` |
| Push tag `v*` | `publish.yml` | `test` → `publish` | `{tag without v}` |

## Pipeline Job: `publish`

```
inputs:
  VERSION   string   (e.g. "0.1.0-preview.42" or "1.0.0")

steps:
  dotnet pack  /p:Version={VERSION}  → produces 12 .nupkg files
  dotnet nuget push *.nupkg  --skip-duplicate  → uploads to NuGet.org
```
