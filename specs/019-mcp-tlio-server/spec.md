# Feature Specification: TLio MCP Server

**Feature Branch**: `019-mcp-tlio-server`  
**Created**: 2026-05-01  
**Status**: Draft  
**Input**: User description: "i need to implement an MCP server, that i can use for different use cases, 1 to inform the agent about individual commands and functions to explain intend, usage and strucure of input and output of the command and function. 2 be able to test a script by proving the script and the input to the mcp server and do the transformation, in this case you have to support all commands and functions in this project. it should return the result but also all details why items failed or didn't change anything, not necessarily faulty but a thing to observe. 3 having a input and a result json/xml/yaml and the request with intent instructions to make a script to make the input into the result. you are allowed to change implementations of the TLIO projects only to add observability but not functionality."

## Clarifications

### Session 2026-05-01

- Q: Should stdout capture be scoped or capture all process output? → A: Only TLio execution-relevant events (command outcomes, path matches, node mutations) are captured; general process stdout is excluded from the trace.
- Q: Is observability optional or mandatory? → A: Mandatory and always enabled by default; can be disabled via configuration for high-performance environments. When disabled, execution still succeeds but no trace is produced.
- Q: Does the MCP server itself use an AI model for script generation? → A: No. The MCP server is fully deterministic. Agents that call the MCP may use AI models; the MCP does not.
- Q: Is rate limiting in scope? → A: Yes. Default maximum is 20 requests per minute; the limit is configurable from outside without redeployment. Authentication remains out of scope.
- Q: What does the server return when the rate limit is exceeded? → A: A structured error response including a retry-after value (seconds until the next request is allowed), enabling the agent to back off precisely without polling.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Discover Commands and Functions (Priority: P1)

An agent or developer wants to understand what TLio commands and functions are available, what they do, what inputs they expect, and what outputs they produce — without needing to read source code.

**Why this priority**: This is the foundational use case. Without knowing what tools are available, an agent cannot construct or debug any script. All other use cases depend on this knowledge.

**Independent Test**: Can be fully tested by calling the "describe command" tool on any known command (e.g., `set`, `copy`, `map`) and verifying that the response contains intent, parameter schema, and output shape. Delivers immediate value as a reference tool even before execution or generation capabilities exist.

**Acceptance Scenarios**:

1. **Given** the MCP server is running, **When** the agent requests a list of all available commands, **Then** the server returns every supported command with its name and a short intent description.
2. **Given** the MCP server is running, **When** the agent requests details for a specific command (e.g., `set`), **Then** the server returns the command's intent, every accepted parameter with type and description, and the shape of the output or side effect.
3. **Given** the MCP server is running, **When** the agent requests details for a built-in function (e.g., `concat`, `format`), **Then** the server returns the function's intent, argument list with types, and return value description.
4. **Given** the agent requests a command or function that does not exist, **When** the server processes the request, **Then** the server returns a clear "not found" response and suggests similar names if any exist.

---

### User Story 2 - Execute a Script and Observe Results (Priority: P2)

An agent or developer has written a TLio script and wants to execute it against a provided input document (JSON, XML, or YAML) and receive the transformed output along with a detailed execution trace explaining what happened to each command — what changed, what was skipped, and why.

**Why this priority**: This is the core diagnostic loop. An agent iterating on a script needs to know not just the final output but why each step succeeded, failed, or produced no change — so it can make targeted corrections without guessing.

**Independent Test**: Can be tested by submitting a simple script and a JSON input document and verifying the response contains the transformed output, a per-command execution record, and at least one observation note when a command matches zero items.

**Acceptance Scenarios**:

1. **Given** a valid TLio script and a JSON input document, **When** the agent submits them to the "execute script" tool, **Then** the server returns the fully transformed output document in the same format as the input.
2. **Given** a script where a command matches no nodes in the input, **When** execution completes, **Then** the server reports that command as a no-op observation (not an error) including the path expression that found zero matches.
3. **Given** a script containing a command that fails (e.g., invalid path, type mismatch), **When** execution completes, **Then** the server reports the failure with the failing command, the reason, and the input state at the point of failure.
4. **Given** a script with multiple commands, **When** execution completes, **Then** the server returns one trace entry per command showing: command name, target path, outcome (success / no-op / failure), matched node count, and a human-readable detail message.
5. **Given** an XML or YAML input document, **When** the agent submits it with the correct format indicator, **Then** execution and tracing behave identically to the JSON case.
6. **Given** observability is disabled via configuration, **When** the agent executes a script, **Then** the server returns the transformed output without a trace (no performance overhead from instrumentation).

---

### User Story 3 - Analyze Transformation Gap and Support Script Assembly (Priority: P3)

An agent or developer provides an input document and a desired output document. The MCP server performs a deterministic structural analysis of the difference and returns a structured gap report — the set of node-level changes required to convert the input into the output. The calling agent's model uses this report, combined with the command/function knowledge from use case 1, to assemble and iteratively refine a TLio script via the execution tool (use case 2).

**Why this priority**: This is the highest-leverage use case for automation but depends on discovery (P1) and execution (P2) being solid. The MCP's role is deterministic analysis only; script assembly belongs to the agent's model. This separation keeps the MCP server stateless and predictable.

**Independent Test**: Can be tested end-to-end by providing a simple JSON input, a JSON target output, and an intent description, then verifying the gap report contains the correct structural changes (e.g., field rename, value substitution), and that using those changes the agent can produce the correct script within 3 execute-refine cycles.

**Acceptance Scenarios**:

1. **Given** an input document and a target output document, **When** the agent calls the "analyze transformation" tool, **Then** the server returns a structured gap report listing each node-level change (add field, remove field, rename field, change value, reorder) with the source path, target path, and a plain-language description of the change.
2. **Given** the gap report, **When** the agent's model constructs a TLio script and executes it via the execution tool, **Then** the output matches the target within a maximum of 3 execute-refine cycles.
3. **Given** the agent submits a prior execution trace alongside the gap report (refinement pass), **When** the server processes the request, **Then** it returns an updated gap report highlighting which changes are still unresolved, so the agent can target its next script revision precisely.
4. **Given** the intent description describes a transformation that is structurally impossible given the input and output documents, **When** the server analyzes the request, **Then** it returns a plain-language explanation of the structural contradiction and what is actually different between the two documents.
5. **Given** an optional plain-language intent description alongside the input and output, **When** the server returns the gap report, **Then** the gap report items are annotated with intent context where the intent helps disambiguate multiple possible change interpretations.

---

### Edge Cases

- What happens when the submitted script contains a syntax error before any command executes?
- How does the server handle an empty input document?
- What if the format indicator (JSON/XML/YAML) does not match the actual document content?
- What happens when a command produces a partial success — some nodes updated, some fail?
- What if the desired output in use case 3 is structurally identical to the input (no-op transformation)?
- How are deeply nested or very large documents (approaching 1 MB) handled without blocking the response?
- When a request is rejected due to rate limit being exceeded, the server returns a structured error with a retry-after value (seconds); the agent MUST NOT retry before that duration elapses.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The MCP server MUST expose a tool to list all supported TLio commands with names, intent descriptions, and parameter schemas.
- **FR-002**: The MCP server MUST expose a tool to list all supported TLio built-in and extension functions with names, argument descriptions, and return value descriptions.
- **FR-003**: The MCP server MUST expose a tool to retrieve detailed documentation for a single named command or function, including all parameters with types, optionality, and a usage example.
- **FR-004**: The MCP server MUST expose a tool to execute a TLio script against a provided input document in JSON, XML, or YAML format and return the transformed output in the same format.
- **FR-005**: The execution tool MUST return a structured trace with one record per command, each containing: command name, target path, outcome (success / no-op / failure), matched node count, and a human-readable detail message. The trace MUST capture only TLio execution-relevant events; general process output is excluded.
- **FR-006**: The execution tool MUST distinguish between failures (incorrect behavior) and no-ops (zero matches, not necessarily wrong) and surface both in the trace.
- **FR-007**: The MCP server MUST expose a deterministic "analyze transformation" tool that accepts an input document and a target output document and returns a structured gap report listing all node-level changes required to convert the input into the output.
- **FR-008**: The gap report MUST describe each change with: source path, target path, change type (add / remove / rename / mutate / reorder), and a plain-language description.
- **FR-009**: The "analyze transformation" tool MUST accept an optional prior execution trace as refinement context and return an updated gap report limited to changes that remain unresolved.
- **FR-010**: The "analyze transformation" tool MAY accept an optional plain-language intent description; when provided, gap report items MUST be annotated with intent context where it helps disambiguate the change.
- **FR-011**: All document-handling tools MUST accept an explicit format parameter (json / xml / yaml) to identify document type.
- **FR-012**: The execution trace instrumentation MUST be enabled by default and MUST be disableable via external configuration without code changes. When disabled, execution returns the output only, with no trace and no instrumentation overhead.
- **FR-013**: Changes to TLio core projects to support observability MUST be additive only — no existing behavior may be modified or removed.
- **FR-014**: The MCP server MUST enforce a configurable rate limit. The default limit is 20 requests per minute per client. The limit MUST be adjustable via external configuration without redeployment. When the limit is exceeded, the server MUST return a structured error response containing a retry-after value (in seconds) indicating when the next request will be accepted.
- **FR-015**: The MCP server MUST start as a standalone process accessible via MCP stdio transport.

### Key Entities

- **Command Descriptor**: A TLio command — name, intent, parameter list (name, type, required, description), output description, usage example.
- **Function Descriptor**: A TLio function — name, intent, argument list (name, type, description), return type, usage example.
- **Execution Request**: Input document (text + format), script text, optional flag to suppress trace.
- **Execution Result**: Transformed output document (text + format), list of Command Trace Records (empty when observability disabled).
- **Command Trace Record**: Per-command execution record — command name, path expression, outcome (success / no-op / failure), matched node count, detail message. Contains only TLio execution-relevant data.
- **Gap Report**: Deterministic structural diff between input and target — list of Change Items.
- **Change Item**: Source path, target path, change type (add / remove / rename / mutate / reorder), plain-language description, optional intent annotation, optional resolution status (resolved / unresolved) when used in refinement context.
- **Analysis Request**: Input document (text + format), target output document (text + format), optional intent description, optional prior execution trace (for refinement pass).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An agent with no prior TLio knowledge can correctly identify the right command and function for a given transformation after a single call to the discovery tool.
- **SC-002**: The execution trace allows an agent to pinpoint the exact command and path responsible for an unexpected result without requiring additional debugging calls.
- **SC-003**: An agent achieves the correct transformation result within 3 execute-refine cycles for transformations involving up to 5 distinct structural changes, using the gap report as guidance.
- **SC-004**: No-op commands (zero matches) appear in every execution trace (when observability is enabled), enabling agents to detect mistyped paths or wrong assumptions immediately.
- **SC-005**: All three capabilities (discover, execute, analyze) respond within a time frame that keeps an agent's reasoning loop interactive for typical documents under 1 MB.
- **SC-006**: Adding observability instrumentation to TLio projects causes zero regressions in the existing automated test suite.
- **SC-007**: When observability is disabled, execution performance is not measurably impacted by trace infrastructure.
- **SC-008**: The rate limiter rejects requests exceeding the configured limit with a structured error that includes a retry-after value, enabling the agent to back off for the precise required duration and retry successfully on the first attempt after the window clears.

## Assumptions

- The MCP server is implemented in C# / .NET 10, consistent with the rest of the TLio ecosystem.
- The server communicates over stdio transport (standard MCP convention for local tooling); HTTP transport is a future extension.
- All TLio commands and functions present in the codebase at implementation time are covered; new additions will follow the same pattern.
- The MCP server is fully deterministic; it contains no AI model. Agents that call the MCP may use AI models to generate scripts; the MCP only provides deterministic tools (discovery, execution, structural analysis).
- The observability trace is implemented by instrumenting the existing execution pipeline additively (event hooks or a trace collector); no existing behavior is modified.
- Input documents are assumed to be well-formed; format validation errors are reported as structured failures, not server crashes.
- Rate limiting applies per client (identified by connection context). Authentication is out of scope.
- XML path format defaults to slash-path; native XPath is selectable via an execution parameter.
- The observability enable/disable toggle and rate limit value are exposed via a configuration file or environment variables readable at server startup without code changes.
