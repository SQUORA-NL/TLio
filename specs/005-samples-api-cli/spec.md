# Feature Specification: Sample Projects — API and CLI

**Feature Branch**: `005-samples-api-cli`
**Created**: 2026-03-28
**Status**: Draft
**Input**: User description: "Create a samples directory with a minimal API project (HTTP endpoint per data type that executes a TLio script and returns correct status code and content-type) and a CLI project (accepts input file and script, performs a transformation, outputs result)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - API Sample: Transform Data via HTTP Endpoint (Priority: P1)

A developer integrating TLio into a web service wants a working reference that shows how to expose a transformation endpoint over HTTP. They invoke an endpoint with a data payload of a specific type (JSON, XML, or YAML), the endpoint runs the appropriate TLio script, and responds with the transformed data in the correct format along with the right HTTP status code and content-type header.

**Why this priority**: This is the most visible entry point for developers who will consume TLio from other services. It demonstrates correct status codes (200 on success, 400 on invalid input, 422 on script failure), content-type negotiation, and per-type script execution — all in one runnable sample.

**Independent Test**: Start the API sample, POST a JSON payload to `/transform/json`, verify the response body matches the expected transformation output, status code is 200, and content-type is `application/json`.

**Acceptance Scenarios**:

1. **Given** a valid JSON payload and a registered script, **When** the endpoint receives a POST request, **Then** the response body contains the transformed JSON, the status code is 200, and the content-type is `application/json`.
2. **Given** a valid XML payload and a registered script, **When** the endpoint receives a POST request, **Then** the response body contains the transformed XML, the status code is 200, and the content-type is `application/xml`.
3. **Given** a valid YAML payload and a registered script, **When** the endpoint receives a POST request, **Then** the response body contains the transformed YAML, the status code is 200, and the content-type is `application/yaml` (or `text/yaml`).
4. **Given** a malformed or empty payload, **When** the endpoint receives a POST request, **Then** the response status code is 400 and the body contains a human-readable error message.
5. **Given** a payload that causes the script to fail, **When** the endpoint receives a POST request, **Then** the response status code is 422 and the body contains the script failure detail.

---

### User Story 2 - CLI Sample: Transform a File from the Command Line (Priority: P2)

A developer or DevOps engineer wants to run a TLio transformation from a terminal or automated pipeline. They point the CLI at an input file and a script file, and the CLI executes the transformation and writes the result to standard output (or an output file), returning a non-zero exit code on failure.

**Why this priority**: The CLI pattern is the simplest self-contained demonstration of TLio's power and the most common automation pattern. It can be wired into shell scripts, CI pipelines, or batch jobs without any network stack.

**Independent Test**: Run the CLI with a sample JSON input file and script file; verify the output matches the expected transformed result and the process exits with code 0. Then run it with a broken script; verify exit code is non-zero and an error message is printed.

**Acceptance Scenarios**:

1. **Given** a valid input file and a valid script file, **When** the CLI runs, **Then** the transformed output is written and the process exits with code 0.
2. **Given** a valid input file with XML content and a matching script, **When** the CLI runs, **Then** the output is valid transformed XML and exit code is 0.
3. **Given** an input file path that does not exist, **When** the CLI runs, **Then** an error message is printed to stderr and exit code is non-zero.
4. **Given** a script that produces a transformation failure, **When** the CLI runs, **Then** the failure details are printed to stderr and exit code is non-zero.
5. **Given** no arguments, **When** the CLI runs, **Then** a concise usage/help message is displayed.

---

### Edge Cases

- What happens when the API receives a content-type it does not recognise? → Returns 415 Unsupported Media Type.
- What happens when the script file referenced by the CLI is empty? → Error is reported; exit code is non-zero.
- What happens when the API script produces no output nodes? → Returns 200 with an empty/null result body (transformation succeeded but produced no data).
- What happens when both API and CLI are run against a YAML document with multi-document syntax? → Behaviour is consistent with the YAML adapter's documented capability; unsupported cases return a clear error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST contain a `samples/` top-level directory housing both sample projects.
- **FR-002**: The API sample MUST expose at least one HTTP endpoint per supported data type (JSON, XML, YAML) that accepts a data payload and a script reference.
- **FR-003**: The API sample MUST return the correct HTTP status code: 200 for success, 400 for invalid input, 422 for script execution failure, 415 for unsupported content type.
- **FR-004**: The API sample MUST set the response `Content-Type` header to match the format of the output data (e.g., `application/json`, `application/xml`, `text/yaml`).
- **FR-005**: The API sample MUST include at least one bundled example script per supported data type that can be run out-of-the-box without any configuration.
- **FR-006**: The CLI sample MUST accept an input file path and a script file path as arguments.
- **FR-007**: The CLI sample MUST write the transformation result to standard output (default) or to an optional output file path when specified.
- **FR-008**: The CLI sample MUST exit with code 0 on success and a non-zero code on any failure (file not found, parse error, script failure).
- **FR-009**: The CLI sample MUST print meaningful error messages to standard error when a failure occurs.
- **FR-010**: Both samples MUST include a `README` explaining how to run them and what each example script does.
- **FR-011**: Each sample MUST work with the existing TLio library projects in the solution without requiring external services.

### Key Entities

- **Sample Script**: A TLio script file (JSON array of command objects) that transforms an input document; each sample ships with at least one per supported data type.
- **Input Document**: A data file in one of the supported formats (JSON, XML, YAML) that serves as the transformation source.
- **Transformation Result**: The output document produced by executing a script against an input document; carries the same format as the input unless the script changes it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer unfamiliar with TLio can run both sample projects within 5 minutes of cloning the repository, following only the README instructions.
- **SC-002**: The API sample responds to a valid transformation request in under 500 ms on a development machine for all three data types.
- **SC-003**: The CLI sample processes a 100 KB input file in under 2 seconds on a development machine.
- **SC-004**: Both samples compile and run without errors using `dotnet run` with no additional configuration steps.
- **SC-005**: 100% of the bundled example scripts produce the documented expected output when run against the bundled sample input files.

## Assumptions

- Samples are developer-facing reference implementations, not production-grade services; they have no authentication, no rate limiting, and no persistence.
- The API sample runs on a locally available port (e.g., 5000/5001) and does not require a reverse proxy or external network access.
- Scripts are embedded in the sample project (e.g., as files in a `Scripts/` sub-folder) rather than passed dynamically per request; the endpoint selects the script based on the data type or a route parameter.
- The CLI sample auto-detects the input format from the file extension (.json, .xml, .yaml/.yml); explicit format override is out of scope for v1.
- Both samples reference TLio library projects in the same solution via project references (no NuGet packages required).
- The YAML adapter is sufficiently stable for use in samples; if it is still experimental, only JSON and XML samples are required as a minimum.
