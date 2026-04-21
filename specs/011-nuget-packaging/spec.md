# Feature Specification: NuGet Package Deployment

**Feature Branch**: `011-nuget-packaging`  
**Created**: 2026-04-20  
**Status**: Draft  
**Input**: User description: "make all projects except the test packages a nuget package deployment. so all the relevant projects need to get an own nuget package, make it with preview and on a tag on github build a release version"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Consume a stable TLio release from NuGet (Priority: P1)

A developer wants to add TLio to their project. They search NuGet.org, find the TLio packages, pick a stable version, and install it — no source access required.

**Why this priority**: This is the primary goal — making TLio consumable by external developers without building from source.

**Independent Test**: A developer can `dotnet add package TLio.Core` in a blank project and reference `TLio.Core` types without errors.

**Acceptance Scenarios**:

1. **Given** a version tag is pushed to GitHub, **When** the release pipeline completes, **Then** all 12 library packages appear on NuGet.org with that version number and no pre-release suffix.
2. **Given** a NuGet release package, **When** a developer installs it into a .NET project, **Then** it compiles and runs without needing the TLio source repository.
3. **Given** multiple releases exist, **When** a developer browses NuGet.org for `TLio`, **Then** they see a consistent family of packages with matching version numbers.

---

### User Story 2 — Try the latest unreleased features via a preview package (Priority: P1)

A developer wants to test a feature not yet in a stable release. They install a preview package instead of building from source.

**Why this priority**: Preview packages unblock early adopters and contributors without requiring a formal release cycle.

**Independent Test**: After a commit lands on main, a developer can install a `-preview` package using `--prerelease` and exercise the new behaviour.

**Acceptance Scenarios**:

1. **Given** a commit is merged to `main` (no tag), **When** the pipeline runs, **Then** all 12 packages are published with a pre-release version suffix.
2. **Given** a preview package exists on NuGet.org, **When** a developer installs it using `--prerelease`, **Then** it installs and compiles correctly.
3. **Given** a version tag is later pushed, **When** the release pipeline runs, **Then** the stable version supersedes the preview in version ordering.

---

### User Story 3 — Install only what you need and get the right transitive dependencies (Priority: P2)

A developer installs a single top-level TLio package and all required TLio dependencies are restored automatically — no manual hunting for transitive packages.

**Why this priority**: Correct inter-package dependency declarations are essential for a usable package family.

**Independent Test**: Installing only `TLio.Json` in a blank project automatically restores `TLio.Core`, `TLio.Commands`, and `TLio.Functions` without any additional commands.

**Acceptance Scenarios**:

1. **Given** a developer installs `TLio.Json`, **When** the package is restored, **Then** `TLio.Core`, `TLio.Commands`, and `TLio.Functions` are also restored automatically.
2. **Given** a developer installs `TLio.Extensions.Text`, **When** the package is restored, **Then** `TLio.Functions` and `TLio.Core` are also available.
3. **Given** any published package, **When** a developer views it on NuGet.org, **Then** it shows a description, project URL, license, and repository link.

---

### Edge Cases

- What happens when a tag is pushed but the pipeline fails mid-way — is a partial set of packages published?
- What version number is used for preview packages when multiple commits land on main between tags?
- What happens if a package with the same version already exists on NuGet.org (duplicate push)?
- Are sample projects (`TLio.Sample.Api`, `TLio.Sample.Cli`) correctly excluded?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The following 12 projects MUST each produce their own NuGet package: `TLio.Core`, `TLio.Commands`, `TLio.Functions`, `TLio.Client`, `TLio.Extensions.ETL`, `TLio.Extensions.Math`, `TLio.Extensions.Text`, `TLio.Extensions.TimeDate`, `TLio.Json`, `TLio.Json.SystemText`, `TLio.Xml`, `TLio.Yaml`.
- **FR-002**: Test projects and sample projects MUST NOT produce NuGet packages.
- **FR-003**: On every push to the `main` branch, all 12 packages MUST be published as pre-release (preview) versions to NuGet.org.
- **FR-004**: On a version tag push (format `vX.Y.Z`), all 12 packages MUST be published as stable versions to NuGet.org with the version derived from the tag.
- **FR-005**: Preview version numbers MUST include a pre-release suffix so they sort below the corresponding stable release (e.g. `1.0.0-preview.42` before `1.0.0`).
- **FR-006**: All 12 packages in a single pipeline run MUST share the same version number.
- **FR-007**: Each package MUST declare correct NuGet dependencies on other TLio packages so consumers do not need to manually install transitive packages.
- **FR-008**: Each package MUST include metadata: description, project URL (GitHub repository), license (MIT), and repository source link.
- **FR-009**: Packages MUST only be published after all automated tests pass.
- **FR-010**: If any package fails to publish, the pipeline run MUST be marked failed (no silent partial deploys).

### Key Entities

- **NuGet Package**: A versioned, distributable unit containing one library's compiled output and metadata. Each of the 12 projects produces one.
- **Preview Release**: A package version bearing a pre-release suffix, published on every main-branch commit.
- **Stable Release**: A package version without a pre-release suffix, published only on a version tag push.
- **Version Tag**: A Git tag in the form `vX.Y.Z` that triggers the stable release pipeline.
- **Pipeline Run**: A single automated build-and-publish execution triggered by a git event (push or tag).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Within 10 minutes of a main-branch commit, all 12 preview packages are available on NuGet.org.
- **SC-002**: Within 10 minutes of a version tag push, all 12 stable packages are available on NuGet.org with the exact tag version.
- **SC-003**: A developer can add any TLio package to a blank .NET project and build successfully without accessing the TLio source repository.
- **SC-004**: Zero test-project or sample-project packages appear on NuGet.org.
- **SC-005**: Installing a single top-level package (e.g. `TLio.Json`) automatically restores all required TLio dependencies with no manual steps.
- **SC-006**: Every package published has a description, license, and repository link visible on its NuGet.org page.

## Assumptions

- NuGet.org is the target package registry (not GitHub Packages).
- The license is MIT, consistent with the repository's existing license.
- Version tags follow the format `vX.Y.Z` (semantic versioning with a `v` prefix).
- Preview version suffix format is `-preview.{N}` where `{N}` is the build number — exact format to be confirmed during planning.
- A NuGet API key will be stored as a GitHub Actions secret (`NUGET_API_KEY`).
- The existing GitHub Actions CI already runs tests; the publish step will be added to that workflow.
- No pre-existing NuGet packages exist for TLio; no version continuity migration is needed.
- Sample projects are out of scope for packaging.
