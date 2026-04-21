# Feature Specification: Docker-Based Plugin API with NuGet Hot-Loading

**Feature Branch**: `012-docker-plugin-api`  
**Created**: 2026-04-21  
**Status**: Draft  
**Input**: User description: "make a samples folder and move all samples there. I also want to have a sample there like the api but now as a docker image that can use the nugetpackages as plugins. The way to do that is using c-shells and nuplane. I want the docker running, being able to drop nuget packages into a folder that is linked to the docker and then the api expands the functionality. When I remove the nuget the functionality provided by the package should be removed and not working anymore."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consolidate Samples (Priority: P1)

A developer browsing the TLio repository wants to find all runnable examples in one place. Currently samples live at the root alongside library projects. Moving them into a dedicated top-level `samples/` folder makes the repository easier to navigate and understand at a glance.

**Why this priority**: Foundational housekeeping that unblocks US2 (the new Docker sample needs a home) and makes the repository immediately more navigable.

**Independent Test**: Clone the repo, navigate to `samples/`, run `dotnet build` — all sample projects compile and run correctly from their new location.

**Acceptance Scenarios**:

1. **Given** the repository is cloned, **When** a developer navigates to `samples/`, **Then** they find all existing sample projects (API, CLI) organised there with no broken project or solution references.
2. **Given** a developer runs the solution build from the repo root, **When** the build completes, **Then** all projects including the relocated samples compile without errors.

---

### User Story 2 - Drop NuGet Package → API Gains Functionality (Priority: P2)

An operator running the TLio Docker container wants to add new data-transformation capabilities without rebuilding or restarting the container. They copy a TLio extension NuGet package (e.g. `TLio.Extensions.Math.nupkg`) into a folder that is volume-mounted into the running container. The API automatically detects the package, loads it, and exposes the extension's functions through the existing transformation endpoints — no restart required.

**Why this priority**: This is the centrepiece of the feature — a live demonstration that TLio's engine is genuinely extensible at runtime.

**Independent Test**: Start the container, call a Math function → confirm failure. Drop `TLio.Extensions.Math.nupkg` into the plugins folder → call the same function → confirm correct result. Remove the package → call again → confirm failure.

**Acceptance Scenarios**:

1. **Given** the container is running with no plugins loaded, **When** a client calls a transformation script that references a Math function, **Then** the API returns a failure response indicating the function is unavailable.
2. **Given** a valid TLio extension `.nupkg` is copied into the mounted plugins folder, **When** the system detects the new file, **Then** within 10 seconds the API accepts and correctly executes scripts using that extension's functions — without restarting the container.
3. **Given** a plugin package was previously loaded and is actively serving requests, **When** the `.nupkg` file is removed from the plugins folder, **Then** scripts using that extension's functions subsequently return a failure response, while all other functionality continues working normally.

---

### User Story 3 - Inspect Loaded Plugins (Priority: P3)

An operator wants to confirm which extension packages are currently active in the running container. They call a discovery endpoint that returns the list of loaded packages and their contributed functions, without having to inspect the filesystem or container logs.

**Why this priority**: Operational visibility — useful for verifying plugin state but not required for the core add/remove flow.

**Independent Test**: Load a plugin, call `GET /plugins`, verify the response lists the loaded extension and its functions.

**Acceptance Scenarios**:

1. **Given** no plugins are loaded, **When** `GET /plugins` is called, **Then** the response lists only the built-in TLio core functions.
2. **Given** a plugin package is loaded, **When** `GET /plugins` is called, **Then** the response includes the extension package name and the functions it contributes.

---

### Edge Cases

- What happens when a malformed or non-TLio `.nupkg` is dropped into the plugins folder? Must be rejected gracefully with a logged warning — no crash.
- What happens when two packages register a function with the same name? The system must log a conflict and follow a deterministic resolution rule (documented in the sample README).
- What happens when a package is dropped while a request using a different package's function is in flight? The in-flight request must complete successfully.
- What happens when the plugins folder is not mounted (container started without a volume)? The container must start normally with only built-in functionality.
- What happens when a package file is still being written (partial copy)? The loader must not crash on an incomplete archive.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All existing sample projects MUST be relocated into a single `samples/` directory at the repository root with no changes to their runtime behaviour.
- **FR-002**: A new Docker-based sample API MUST be added to `samples/` alongside the existing API and CLI samples.
- **FR-003**: The Docker sample API MUST start with only TLio core functionality — no extension packages pre-loaded.
- **FR-004**: The Docker sample API MUST detect when a valid TLio extension `.nupkg` is placed in the configured plugins folder and load it without restarting the container.
- **FR-005**: Functions contributed by a loaded extension MUST become available to transformation script execution within 10 seconds of the package file being fully written to the plugins folder.
- **FR-006**: The Docker sample API MUST detect when a previously loaded `.nupkg` is removed from the plugins folder and unload the extension, making its functions unavailable to subsequent requests within 10 seconds.
- **FR-007**: Invalid or unrecognised files placed in the plugins folder MUST be ignored with a logged warning and MUST NOT cause the container to crash or restart.
- **FR-008**: The API MUST expose an endpoint (`GET /plugins`) that lists currently loaded packages and their contributed functions.
- **FR-009**: A `Dockerfile` and `docker-compose.yml` (with the volume mount pre-configured) MUST be provided so the sample can be started with a single command.
- **FR-010**: The NuGet package watching and hot-loading mechanism MUST use NuPlane (available via the private feedz.io feed at `https://f.feedz.io/nuplane/nuplane/nuget/index.json`) in combination with CShells for shell/feature management.

### Key Entities

- **Plugin Package**: A `.nupkg` file placed in the plugins folder containing one or more TLio extension assemblies that register functions with the engine.
- **Plugins Folder**: A filesystem directory volume-mounted into the Docker container that the running API monitors for additions and removals.
- **Loaded Extension**: A plugin package that has been successfully extracted and whose functions are currently active in the API.
- **Plugin Registry**: The runtime record of which packages are currently loaded and which functions they contribute.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing sample projects build and run correctly from their new location under `samples/` — verified by a clean build.
- **SC-002**: A plugin package dropped into the plugins folder becomes active (its functions callable) within 10 seconds of the file being fully written.
- **SC-003**: A plugin package removed from the plugins folder stops serving function calls within 10 seconds of removal, while all unrelated API calls continue to succeed.
- **SC-004**: The Docker container starts and is ready to accept requests in under 30 seconds on a standard developer machine.
- **SC-005**: Dropping a non-TLio or malformed file into the plugins folder produces a logged warning and no container crash — verified via `docker logs`.
- **SC-006**: `GET /plugins` reflects the current loaded state within 1 second of any plugin being added or removed.
- **SC-007**: A developer with Docker installed can run the full add-plugin → call endpoint → remove-plugin → call endpoint cycle using only the commands documented in the sample README.

## Assumptions

- NuPlane is available via a private feedz.io feed (`https://f.feedz.io/nuplane/nuplane/nuget/index.json`); the user has access. It handles NuGet package watching and hot-loading.
- CShells (`https://github.com/sfmskywalker/CShells`) provides the modular feature host — features implement `IWebShellFeature` to register services and HTTP endpoints per plugin.
- The plugins folder path inside the container defaults to `/plugins` and is configurable via an environment variable.
- The Docker image targets Linux (amd64) and .NET 10.
- Plugin packages are official TLio extension packages (`TLio.Extensions.*`) or packages following the same pattern — not arbitrary third-party assemblies.
- Security isolation between plugins is out of scope; all plugins run in the same process.
- Updating an already-loaded plugin (overwriting the `.nupkg`) is out of scope for v1 — the supported flow is remove then re-add.
- The sample is for demonstration only and does not require production hardening (authentication, rate limiting, HTTPS, etc.).
- The existing `TLio.Sample.Api` and `TLio.Sample.Cli` projects move to `samples/` with path updates only — no functional changes.
