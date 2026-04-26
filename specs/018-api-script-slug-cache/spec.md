# Feature Specification: API Script Slug Cache

**Feature Branch**: `018-api-script-slug-cache`  
**Created**: 2026-04-26  
**Status**: Draft  
**Input**: User description: "add to the samples api implementation (docker and api) the ability to register the scripts in a compiled manner in a memory cache with a slug, so there are now endpoints that listen to that slug and can execute that script in the fastest way possible. These endpoints take the full body of the request and paste it into the script as the input data. The result is the result of the script."

## Clarifications

### Session 2026-04-26

- Q: What is the primary mechanism for slug declaration — startup config file, runtime API call, or both equally supported? → A: Both equally — startup config AND runtime API are first-class; neither is primary.
- Q: In the startup configuration file, are scripts embedded inline as text or referenced by file path? → A: Inline — script source text is embedded directly in the config file as a string value.
- Q: What content type does the `POST /run/{slug}` response carry? → A: Script-driven — the server sets the response `Content-Type` based on the format of the script's output, not the input or a fixed type.
- Q: What content type does the `POST /scripts` registration endpoint accept? → A: Any — the server accepts the registration payload as-is regardless of content type (JSON, XML, etc.); if the payload cannot be parsed or the script fails compilation, the server returns the appropriate error code and message.
- Q: What logging is required for slug execution and registration events? → A: Errors only — successful executions are silent; failures (404, 422, parse errors) are logged with slug and reason. All registry mutations (register, replace, delete) are always logged.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Execute a Pre-Registered Script via Slug (Priority: P1)

A developer or system operator sends an HTTP request to a slug-based endpoint. The server looks up the slug in the in-memory cache, finds the pre-compiled script, runs it with the request body as input data, and returns the result immediately — without any per-request parsing or compilation overhead.

**Why this priority**: This is the core value proposition. Everything else (registration, management) only has meaning if fast slug-based execution works correctly. It is P1 because it is the product.

**Independent Test**: Can be fully tested by pre-seeding one script in the cache at startup and sending a `POST /run/{slug}` request with a body, verifying the response equals the expected script output.

**Acceptance Scenarios**:

1. **Given** a script is registered under slug `transform`, **When** a client sends `POST /run/transform` with a JSON body `{"value": 42}`, **Then** the server executes the pre-compiled script with that body as input and returns the script result with HTTP 200.
2. **Given** a slug `unknown-slug` is not registered, **When** a client sends `POST /run/unknown-slug` with any body, **Then** the server returns HTTP 404 with a message indicating the slug is not found.
3. **Given** the script execution produces a runtime error (e.g., missing required field), **When** a client triggers that script, **Then** the server returns HTTP 422 with a descriptive error message, not a 500.

---

### User Story 2 - Register a Script at Runtime (Priority: P2)

An operator registers a new TLio script at runtime by sending a request containing a slug and the script body. The server compiles and validates the script immediately and stores it in the in-memory cache. From that point on, the slug endpoint is active and ready for execution requests.

**Why this priority**: Runtime registration makes the feature dynamic and usable without redeployment. Without it, operators can only use startup-seeded scripts, which limits the feature's utility.

**Independent Test**: Can be tested by calling the registration endpoint with a slug and script, then immediately calling the execution endpoint with that slug, verifying the result is correct.

**Acceptance Scenarios**:

1. **Given** no script is registered under slug `my-script`, **When** a client POSTs to `/scripts` with `{ "slug": "my-script", "script": "..." }`, **Then** the server compiles the script, stores it in cache, and returns HTTP 201 with the slug confirmed.
2. **Given** the provided registration payload is in any format (JSON, XML, etc.) but contains a script with a syntax error, **When** a client tries to register it, **Then** the server returns HTTP 400 with a descriptive compilation error, and the slug is NOT added to the cache.
3a. **Given** the registration payload is in an unrecognizable format that cannot be parsed to extract a slug and script, **When** a client submits it, **Then** the server returns HTTP 400 with a descriptive parse error, and the registry is unchanged.
3. **Given** slug `existing` is already registered, **When** a client registers a new script under `existing`, **Then** the existing entry is replaced with the new compiled script and HTTP 200 is returned.

---

### User Story 3 - List and Delete Registered Scripts (Priority: P3)

An operator can view all currently registered slugs (and their associated script source) and can delete a slug registration to remove it from the cache.

**Why this priority**: Operational visibility and lifecycle management. Low urgency for the MVP but important for long-running deployments where the cache must be inspectable and maintainable.

**Independent Test**: Can be tested independently by registering two slugs, calling `GET /scripts` and verifying both appear, then calling `DELETE /scripts/{slug}` and verifying the slug no longer executes.

**Acceptance Scenarios**:

1. **Given** slugs `a` and `b` are registered, **When** a client calls `GET /scripts`, **Then** both slugs (with their script source) are returned.
2. **Given** slug `obsolete` is registered, **When** a client calls `DELETE /scripts/obsolete`, **Then** the slug is removed from cache and returns HTTP 204; subsequent execution requests for `obsolete` return 404.
3. **Given** no slugs are registered, **When** a client calls `GET /scripts`, **Then** an empty list is returned with HTTP 200.

---

### User Story 4 - Seed Scripts at Startup via Configuration (Priority: P2)

An operator can define scripts in a startup configuration file. When the API starts, those scripts are automatically compiled and loaded into the cache, making them available immediately without any registration calls.

**Why this priority**: Co-equal first-class registration path alongside the runtime API (clarified 2026-04-26). Essential for containerized and Docker deployments where scripts are baked into the image or mounted via volume, eliminating the need for post-start registration calls.

**Independent Test**: Can be tested by providing a startup configuration with one or more scripts and verifying those slugs are executable immediately after the server starts, without any registration call.

**Acceptance Scenarios**:

1. **Given** a configuration file lists slug `startup-script` with a valid script, **When** the server starts, **Then** `POST /run/startup-script` is ready to handle requests immediately.
2. **Given** a configuration file contains a script with a syntax error, **When** the server starts, **Then** the server logs a clear error for that entry and continues starting (the invalid script is skipped, all valid ones are loaded).

---

### Edge Cases

- What happens when the request body is empty? The script receives empty/null input — the outcome depends on the script logic; the server must not crash and must return a script-level error if the script fails.
- What happens when two concurrent requests arrive for the same slug simultaneously? Both execute correctly without corrupting each other's input or output (each execution is independent and stateless).
- What happens when a very large request body is submitted? A configurable maximum body size limit applies; requests exceeding it return HTTP 413.
- What happens when the slug contains special characters (spaces, `/`, `?`)? Only URL-safe alphanumeric slugs (`[a-z0-9-_]`) are accepted; others return HTTP 400.
- What happens when the in-process cache is cleared (restart)? All runtime-registered scripts are lost; only startup-seeded scripts (from config) are reloaded. This is expected behavior.
- What happens when the script produces output in an ambiguous or undetectable format? The server defaults to `application/octet-stream` and returns the raw bytes.
- What happens when the registration payload format is unrecognizable (e.g., a binary blob)? The server returns HTTP 400 with a message describing the parse failure; the registry is unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST maintain an in-memory registry of pre-compiled scripts, each identified by a unique slug string.
- **FR-002**: The system MUST expose a `POST /run/{slug}` endpoint that executes the script registered under `{slug}`, using the raw HTTP request body as the script input data. The response `Content-Type` MUST reflect the format of the script's output (e.g., `application/json`, `application/xml`, `text/plain`) — it is not fixed and is not derived from the request's `Content-Type`.
- **FR-003**: The system MUST compile and validate a script at registration time, not at execution time, so that each execution request incurs zero parsing overhead.
- **FR-004**: The system MUST expose a `POST /scripts` endpoint to register a new script with a slug at runtime; the script is compiled immediately upon registration. The endpoint MUST accept the registration payload in any content type (JSON, XML, YAML, plain text, etc.) and parse it to extract the slug and script source. This is a first-class registration path, co-equal with startup configuration (see FR-009).
- **FR-004a**: The system MUST return HTTP 400 with a descriptive error message when the registration payload cannot be parsed (unrecognizable format or missing required fields), without modifying the registry.
- **FR-005**: The system MUST reject registration of a script that fails compilation, returning HTTP 400 with a descriptive compilation error without modifying the registry.
- **FR-006**: The system MUST allow re-registration of an existing slug, replacing the cached compiled script atomically.
- **FR-007**: The system MUST expose a `GET /scripts` endpoint returning a list of all registered slugs and their original script source text.
- **FR-008**: The system MUST expose a `DELETE /scripts/{slug}` endpoint to remove a slug from the registry.
- **FR-009**: The system MUST support loading scripts from a startup configuration file so they are available immediately on startup without registration calls. Each entry in the file MUST contain a slug and the script source text embedded inline (not a file path reference). This is a first-class registration path, co-equal with the runtime API (see FR-004).
- **FR-010**: The system MUST return HTTP 404 when a request targets an unregistered slug.
- **FR-011**: The system MUST return HTTP 422 with a descriptive message when a registered script fails at runtime due to invalid or missing input data.
- **FR-012**: The system MUST implement this feature in both the `TLio.Sample.Api` project and the `TLio.Sample.DockerPlugin` project.
- **FR-013**: Slugs MUST be validated to contain only URL-safe characters (`[a-z0-9\-_]`); invalid slug formats return HTTP 400.
- **FR-014**: The registry MUST be thread-safe, supporting concurrent reads from many simultaneous execution requests.
- **FR-015**: The system MUST log all registry mutation events (slug registered, replaced, or deleted) including the slug name and timestamp.
- **FR-016**: The system MUST log all execution failures (404 slug not found, 422 runtime error, 400 parse/compilation error) with the slug name and a description of the failure. Successful executions MUST NOT be logged to keep the hot path free of I/O overhead.

### Key Entities

- **ScriptRegistration**: A record associating a slug (unique string identifier) with a pre-compiled script artifact and the original script source text.
- **ScriptRegistry**: The in-memory store that holds all `ScriptRegistration` entries, supporting lookup by slug and thread-safe add/replace/remove/list operations.
- **SlugExecutionRequest**: The HTTP request targeting a slug endpoint — the slug comes from the URL path, and the input data is the raw request body.
- **ScriptExecutionResult**: The output produced by running a registered script against an input — returned as the HTTP response body with a `Content-Type` that matches the script's output format.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Slug-based script execution adds no measurable overhead compared to direct in-process script execution — less than 5ms additional latency per request under normal load.
- **SC-002**: Script registration (compilation + cache storage) completes in under 500ms for typical script sizes.
- **SC-003**: The API handles at least 200 concurrent slug-execution requests without errors or data corruption.
- **SC-004**: An invalid slug (unregistered) returns a 404 response in under 50ms.
- **SC-005**: Scripts seeded via startup configuration are fully available within 3 seconds of the server accepting its first request.
- **SC-006**: All new endpoints are covered by integration tests that verify the full request-to-response cycle including cache lookup and script execution.

## Assumptions

- The request body passed to the script is treated as raw input data in whatever format (JSON, XML, YAML, plain text) the script expects — the server does not enforce a specific content type for execution requests.
- The script source format is the existing TLio script notation used throughout the rest of the project.
- The startup configuration file format is JSON — an array of `{ "slug": "...", "script": "..." }` objects — co-located with the application or provided via a well-known environment variable pointing to a file path. Script source text is embedded inline; file path references are not supported.
- The in-memory registry is process-scoped and ephemeral — no persistence across restarts is required for runtime-registered scripts.
- The `TLio.Sample.DockerPlugin` implementation uses the same slug-cache pattern, integrated into the Docker API host alongside the existing NuPlane plugin infrastructure.
- Maximum request body size defaults to a reasonable cap (e.g., 10 MB) configurable via application settings.
- Authentication and authorization are out of scope for this feature; all endpoints are assumed to be internal or trusted-network facing.
