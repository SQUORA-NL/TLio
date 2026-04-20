# Research: NuGet Package Deployment (011)

## Decision 1: Versioning Strategy

**Decision**: Manual `VersionPrefix` in `Directory.Build.props` (`0.1.0`), overridden at build time by the CI pipeline.
- Preview builds pass `/p:VersionSuffix=preview.{run_number}` → `0.1.0-preview.42`
- Release builds extract version from the git tag (`v1.0.0` → `1.0.0`) and pass `/p:Version=1.0.0`

**Rationale**: No additional tooling dependencies (no MinVer, no Nerdbank.GitVersioning). The tag is the single source of truth for the release version. Simple and transparent.

**Alternatives considered**:
- *MinVer*: Elegant automatic versioning from git tags/commits, but adds a package dependency to every library project.
- *Nerdbank.GitVersioning*: More powerful (assembly versioning, commit height), but heavier setup with `version.json` files.
- *Hard-coded version file*: Requires manual edits before every release; error-prone.

---

## Decision 2: Shared NuGet Metadata Location

**Decision**: Extend `Directory.Build.props` with shared NuGet properties (Authors, RepositoryUrl, PackageLicenseExpression, etc.) and set `IsPackable=false` as the default. Each library project explicitly sets `IsPackable=true` — this ensures test and sample projects are never packaged without any further exclusion logic.

**Rationale**: `Directory.Build.props` is already used for shared build settings (TargetFramework, Nullable, etc.), so it is the natural location. The opt-in `IsPackable=true` pattern is safer than opt-out.

**Alternatives considered**:
- *Per-project metadata*: All 12 projects would duplicate Authors, RepositoryUrl, License. Fragile and noisy.
- *Separate `NuGet.props` imported manually*: More explicit but unnecessary since `Directory.Build.props` is automatically imported.

---

## Decision 3: GitHub Actions Workflow Structure

**Decision**: Two workflow files:
1. `ci.yml` — triggered on push to any branch and on pull_requests to `main`; runs `dotnet test`. This is the always-on quality gate.
2. `publish.yml` — triggered on push to `main` (preview) and on `v*` tags (release); runs tests then publishes. Two jobs: `test` and `publish`, where `publish` depends on `test`.

**Rationale**: Separating CI from publish keeps concerns clean. Preview publishes on every `main` push ensures the latest code is always consumable without a formal release. Tag-triggered releases are the industry standard for NuGet.

**Alternatives considered**:
- *Single workflow with conditions*: Feasible but harder to read and maintain as logic grows.
- *Publish on every branch push*: Too noisy; preview packages should reflect `main` only.
- *Manual trigger only*: Removes automation benefit; defeats the purpose.

---

## Decision 4: Handling Duplicate Package Versions on NuGet.org

**Decision**: Use `--skip-duplicate` flag in `dotnet nuget push`. If a package version already exists, the push is skipped silently rather than failing the pipeline.

**Rationale**: Re-running a failed pipeline after a partial publish would otherwise be blocked by already-published packages. `--skip-duplicate` makes retries safe.

---

## Decision 5: NuGet API Key Storage

**Decision**: Store as a GitHub Actions secret named `NUGET_API_KEY`. The workflow references it as `${{ secrets.NUGET_API_KEY }}`. No key is ever written to files.

**Rationale**: Standard GitHub Actions secret pattern. The key is never visible in logs.

---

## Decision 6: Package Source URL

**Decision**: Push to `https://api.nuget.org/v3/index.json` (NuGet.org public feed).

**Rationale**: Spec requires NuGet.org. GitHub Packages is not in scope.
