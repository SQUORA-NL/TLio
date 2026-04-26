# Feature Specification: Universal Format Converter via Intermediate Model

**Feature Branch**: `017-im-format-converter`  
**Created**: 2026-04-25  
**Status**: Draft  
**Input**: User description: "i want a new poject that has the following focus point: being able to convert from and to any of the types. like json to xml, xml to yml and back . this is done by having a intermediate model to describe all structure in a generic way, this is called the intermediateModel (IM) . in this whay we only have to implement 2 functions FromIM and ToIM. this reduces the items we need to implemnt and still have future proof handling of new types. keep in mind all the definitions like xml has atttributes while other don't and there might be more, so the IM should accomodate all. look in to industry standards (json,xml,Edi ...) and design the IM and implment the current t ypes for both. Keep in mind, there should not be any dependeny on any technology like newtonsoft, only dotnet core elements"

## Clarifications

### Session 2026-04-25

- Q: What type does the convert function receive and return — serialized string or in-memory IM node? → A: The functions accept and return **in-memory IM nodes**; the caller handles serialization/deserialization. `ToIM` parses a format string into an IM node; `FromIM` serializes an IM node to a format string. Both are individually callable script functions.
- Q: Are built-in adapters pre-wired (zero setup) or must the caller register them? → A: Callers **must register** adapters before use. Adapters are not pre-wired because (a) implementations are swappable and (b) some format libraries carry licensing requirements that require the caller to explicitly opt in.
- Q: What is the registration scope — instance-scoped, static/global, or both? → A: Follow TLio's existing command/function registration pattern — adapter registration uses the same mechanism and scope as TLio commands so the system is consistent and familiar to TLio users.
- Q: Where does format conversion connect to TLio script execution? → A: **Segmented pipeline model** — the script contains a `convert` command that signals a format boundary. The script runner splits the script into sections at each `convert` command; each section runs with its own strongly-typed `TNode` context (JSON section uses JSON TNode, XML section uses XML TNode, etc.). The IM is the internal transport mechanism between sections — script authors never interact with it directly.
- Q: When the `convert` command runs, does it convert the entire working document or just a path-selected subtree? → A: **Entire working document (Option A)** — `convert` replaces the full working root with the converted result; the next section starts with the complete document in the new format.
- Q: Where do format-specific settings (e.g. XML text-content property name, attribute prefix, type inference) live — registration-time, per `convert` command, or both? → A: **Per `convert` command (Option B)** — all settings are passed inline on the `convert` command itself. Each setting has a documented default so the command stays concise when standard conventions suffice.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Convert Between Any Two Formats (Priority: P1)

A developer has structured data in one format (e.g., JSON from an API) and needs it in another format (e.g., XML for a legacy system). Within a TLio script they call `ToIM` with a format identifier and the source string to obtain an IM node, then call `FromIM` with a target format identifier and the IM node to produce the output string. Each function is self-contained — no prior setup or command sequence is required.

**Why this priority**: This is the core value proposition. Every other story depends on this working correctly.

**Independent Test**: Can be fully tested by calling `ToIM("json", jsonString)` then `FromIM("xml", imNode)` and verifying the output is valid, well-formed XML containing all the same data.

**Acceptance Scenarios**:

1. **Given** a valid JSON document, **When** the user requests conversion to XML, **Then** the output is valid, well-formed XML that represents the same data structure.
2. **Given** a valid XML document with attributes, **When** the user requests conversion to YAML, **Then** the output is valid YAML that preserves element values; XML attributes are carried over as metadata nodes using a documented convention.
3. **Given** a valid YAML document, **When** the user requests conversion to JSON, **Then** the output is valid JSON representing the same data.
4. **Given** a document with nested structures of arbitrary depth, **When** converted to another format, **Then** all nesting levels are preserved in the output.
5. **Given** an unsupported source or target format identifier, **When** conversion is requested, **Then** a clear, descriptive error is returned instead of a partial or silent failure.

---

### User Story 2 - Round-Trip Fidelity (Priority: P2)

A developer converts a document from format A to format B and then back to format A. They expect the final result to be semantically equivalent to the original — all values, types, and structure preserved.

**Why this priority**: Without round-trip fidelity the converter cannot be trusted in integration pipelines where data must survive multiple transformations.

**Independent Test**: Convert a JSON document to XML and back to JSON; compare original and final documents for structural and value equivalence.

**Acceptance Scenarios**:

1. **Given** a JSON document with strings, numbers, booleans, and nulls, **When** converted to XML and back to JSON, **Then** all scalar types are preserved with correct values.
2. **Given** an XML document with attributes and nested elements, **When** converted to YAML and back to XML, **Then** attributes and element structure are preserved.
3. **Given** a document with arrays/sequences, **When** converted to another format and back, **Then** element order is maintained.

---

### User Story 3 - Extend the System with a New Format (Priority: P3)

A developer wants to add support for a new data format (e.g., TOML, CSV, or EDI). They implement exactly two methods — `ToIM` (parse format into the Intermediate Model) and `FromIM` (serialize the Intermediate Model into the format) — register the adapter, and the new format is immediately available for all conversion paths without modifying any existing code.

**Why this priority**: Extensibility is the key architectural promise. Validating that adding a new format requires only 2 methods proves the design works as intended.

**Independent Test**: Implement a minimal stub adapter for a new format with only `ToIM` and `FromIM`; verify that conversions to/from all existing formats are automatically available.

**Acceptance Scenarios**:

1. **Given** a new format adapter implementing `ToIM` and `FromIM`, **When** registered with the conversion system, **Then** the new format can be used as source or target in any conversion without additional configuration.
2. **Given** an existing set of registered adapters, **When** a new adapter is added, **Then** no existing adapter code requires modification (open/closed principle).

---

### User Story 4 - Format-Specific Metadata Preservation (Priority: P2)

A developer converts an XML document that contains attributes, namespaces, or other format-specific constructs. The converted output retains the intended meaning — either by mapping to equivalent constructs in the target format or by preserving metadata in a defined, consistent way that survives round-trip.

**Why this priority**: XML attributes, namespaces, and similar constructs carry business meaning. Silently dropping them would make the converter unusable for real-world XML workloads.

**Independent Test**: Convert an XML document containing attributes (e.g., `<item id="42">`) to JSON and verify that the attribute value appears in the JSON output in a predictable, documented location.

**Acceptance Scenarios**:

1. **Given** an XML element with attributes, **When** converted to JSON, **Then** each attribute appears as a child property using a documented naming convention (e.g., `@attributeName`).
2. **Given** a JSON document with attribute-convention properties (e.g., `@id`), **When** converted to XML, **Then** those properties become XML attributes on the corresponding element.
3. **Given** an XML document with namespace declarations, **When** converted to YAML, **Then** namespace information is represented in a defined, documented way and is not silently discarded.

---

### User Story 5 - Single Script Pipelines Across Formats Using `convert` Command (Priority: P1)

A developer writes one TLio script that starts with an XML document, uses standard TLio commands to act on it, then adds a `convert` command to switch the working document to JSON, acts on it again with JSON-native commands, then adds another `convert` to YAML and exports. The script runner automatically splits the script into sections at each `convert` boundary and runs each section with the correct strongly-typed context — the developer never calls `ToIM` or `FromIM` explicitly.

**Why this priority**: This is the defining user scenario. The segmented pipeline model makes multi-format scripts seamless — script authors think in their format's native path syntax within each section, and the runner handles all conversion transparently.

**Independent Test**: Write a single TLio script with one `convert` command (XML→JSON); verify that XML-native commands work before the boundary and JSON-native commands work after it, and the final output is valid JSON.

**Acceptance Scenarios**:

1. **Given** an XML input document and a TLio script that contains a `convert` command switching to JSON, **When** the script runs, **Then** all commands before `convert` use XML path syntax and operate on the XML document, and all commands after `convert` use JSON path syntax and operate on the converted JSON document.
2. **Given** a TLio script with two `convert` commands (XML → JSON → YAML), **When** executed once, **Then** the runner produces three sections, each operating in its native format, and the final output is valid YAML.
3. **Given** a `convert` command in a script, **When** it executes, **Then** the entire current working document is converted to the target format and replaces the root — no partial conversion, no residual original-format data.
4. **Given** a script with a `convert` command targeting an unregistered format, **When** executed, **Then** a clear error identifies the unrecognised format at the convert step, not at script-load time.
5. **Given** any combination of supported formats as source and target, **When** a `convert` command switches between them, **Then** all structural information (including XML attributes via the `@` convention) is preserved across the boundary.

---

### Edge Cases

- What happens when a JSON array contains mixed types (e.g., strings alongside objects)?
- How does the system handle empty elements, empty objects, and empty arrays?
- What happens when XML has text content mixed with child elements (mixed content)?
- How are null values represented in formats that have no native null type (e.g., XML)?
- What happens when YAML contains anchors and aliases (references)?
- How are numeric strings (values that look like numbers but must stay as strings) handled during type inference?
- What happens when an XML document contains a CDATA section?
- How does the system behave when given a malformed (unparseable) input document?
- What happens when a `convert` command specifies an unrecognised setting key — silent ignore or error?
- What happens when two `convert` commands in the same script use conflicting `textProperty` values (e.g., `#text` then `_text`) — does round-trip fidelity break?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide an Intermediate Model (IM) capable of representing every structural pattern found in JSON, XML, YAML, and EDI — including named properties, ordered sequences, scalar values, format-specific metadata (attributes, namespaces), and mixed content.
- **FR-002**: The IM MUST support named node containers (equivalent to JSON objects, XML elements, YAML mappings, EDI transaction sets).
- **FR-003**: The IM MUST support ordered sequences of nodes (equivalent to JSON arrays, YAML sequences, EDI segment groups).
- **FR-004**: The IM MUST support scalar leaf values with explicit type hints: string, integer, decimal, boolean, and null.
- **FR-005**: The IM MUST support a metadata/annotation collection on any node for format-specific data, including XML attributes (keyed with `@` prefix), XML namespace declarations (keyed with `xmlns:` prefix), and XML processing instructions.
- **FR-006**: The IM MUST support mixed content nodes — nodes that contain both inline text and child element nodes interleaved — as required by XML.
- **FR-007**: The system MUST provide a JSON adapter implementing `ToIM` (JSON → IM) and `FromIM` (IM → JSON) using only .NET built-in libraries (`System.Text.Json`).
- **FR-008**: The system MUST provide an XML adapter implementing `ToIM` (XML → IM) and `FromIM` (IM → XML) using only .NET built-in libraries (`System.Xml`).
- **FR-009**: The system MUST provide a YAML adapter implementing `ToIM` (YAML → IM) and `FromIM` (IM → YAML). A lightweight open-source YAML library is acceptable provided it has no Newtonsoft.Json dependency.
- **FR-010**: The system MUST provide a `convert` command that can appear anywhere in a TLio script to signal a format boundary. The command takes a target format identifier (`"to": "xml"`) and causes the script runner to convert the entire current working document to the specified format before continuing execution.
- **FR-010a**: The `convert` command is a first-class TLio command registered via TLio's existing command registration mechanism — no special runner hooks or host-level wiring are required from the script author.
- **FR-017**: The system MUST provide a `MultiFormatScriptRunner` (or equivalent) that pre-processes a TLio script, splits it into sections at each `convert` command boundary, and executes each section with the appropriate strongly-typed `IExecutionContext<TNode>` for that section's format. The FormatConverter library is the internal transport between sections — the IM is never visible to the script.
- **FR-018**: At each `convert` boundary, the `MultiFormatScriptRunner` MUST: (1) serialise the current working document using the source format's `FromIM` (passing the `convert` command's source settings), (2) convert via FormatConverter through the IM, (3) deserialise using the target format's `ToIM` (passing the `convert` command's target settings), and (4) create a fresh `IExecutionContext<TNode>` for the target format before executing the next section.
- **FR-019**: The `convert` command MUST accept an optional `settings` block containing per-conversion adapter options. Each setting MUST have a documented default value so the command remains concise when standard conventions are used. Settings are scoped to the single conversion step — they do not persist to subsequent `convert` commands in the same script.
- **FR-020**: The following format-specific settings MUST be supported on the `convert` command:
  - **XML (source or target)**: `textProperty` (key used for XML text content; default `"#text"`), `attributePrefix` (prefix for attribute keys; default `"@"`), `namespacePrefix` (prefix for namespace declaration keys; default `"xmlns:"`), `inferTypes` (infer scalar types from text content; default `false`), `cdataAsText` (treat CDATA as plain text without metadata marker; default `false`)
  - **YAML (source or target)**: `inferTypes` (infer scalar types from untagged scalars; default `false`), `flattenAnchors` (dereference anchors inline; default `true`)
  - **JSON (source or target)**: no mandatory settings for v1 (JSON is natively typed; no convention ambiguity)
- **FR-021**: The `IFormatAdapter` interface MUST accept a `ConversionSettings` parameter on both `ToIM` and `FromIM` so that per-command settings flow through to adapter logic. When no settings are provided, documented defaults apply.
- **FR-011**: Callers MUST explicitly register each adapter before use using the same registration mechanism TLio uses for commands and functions. Registration is the mechanism by which callers opt in to a specific implementation and accept its associated licensing terms. Built-in adapter implementations are provided by the library but are not active until registered.
- **FR-011a**: The registration API MUST allow swapping the implementation for any format identifier — a caller may register a different adapter for `"json"` without changing any other part of the system.
- **FR-011b**: Format adapters MUST be discoverable and registerable in the same way as any other TLio command or function extension, so that existing TLio host configuration patterns apply without learning a new registration API.
- **FR-012**: XML attributes MUST be preserved during `ToIM` (stored as metadata) and restored during `FromIM` (written back as XML attributes) using a documented, consistent naming convention.
- **FR-013**: XML namespace declarations MUST be captured in the IM metadata and represented in a documented, consistent way during conversion to other formats.
- **FR-014**: Scalar type information (string, number, boolean, null) MUST be preserved in the IM so downstream `FromIM` adapters can produce type-correct output.
- **FR-015**: The system MUST NOT depend on Newtonsoft.Json or any non-.NET-built-in JSON library; all JSON processing uses `System.Text.Json` exclusively.
- **FR-016**: The system MUST return a clear, structured error when conversion fails due to unsupported format, malformed input, or an unrepresentable construct.

### Key Entities

- **IntermediateNode**: The base unit of the IM tree. Every node has an optional name, a node kind (object, array, scalar, mixed), and an optional metadata collection.
- **ObjectNode**: An `IntermediateNode` holding an ordered collection of named child `IntermediateNode`s. Represents JSON objects, XML elements, YAML mappings, and EDI segments.
- **ArrayNode**: An `IntermediateNode` holding an ordered sequence of `IntermediateNode`s (which may be unnamed). Represents JSON arrays, YAML sequences, and EDI element groups.
- **ScalarNode**: A leaf `IntermediateNode` holding a typed value (string, integer, decimal, boolean, or null).
- **MixedContentNode**: An `IntermediateNode` holding an ordered list of items that may be inline text strings or child `IntermediateNode`s, enabling round-trip of XML mixed content.
- **NodeMetadata**: A key-value collection attached to any `IntermediateNode` for format-specific data. Standard keys include `@attributeName` for XML attributes and `xmlns:prefix` for namespace declarations.
- **IFormatAdapter**: An interface requiring exactly two methods — `IntermediateNode ToIM(string source, ConversionSettings settings)` and `string FromIM(IntermediateNode root, ConversionSettings settings)` — plus a string format identifier property. Both methods accept a `ConversionSettings` instance (never null; empty = use all defaults).
- **FormatConverter**: Exposes `ToIM(formatId, string)` and `FromIM(formatId, IntermediateNode)` as the primary script-callable API. Maintains an internal registry of `IFormatAdapter` instances keyed by format identifier. Adapters must be explicitly registered before use — this is the caller's opt-in to a specific implementation and its licensing terms. Allows replacing the implementation for any format identifier at any time.
- **ConvertCommand**: A TLio command that signals a format boundary. Schema: `{ "command": "convert", "to": "<formatId>", "settings": { ... } }`. The `settings` block is optional; omitting any setting applies its documented default. Registered via TLio's command registration mechanism and intercepted by the `MultiFormatScriptRunner` at execution time. Example with XML target settings: `{ "command": "convert", "to": "xml", "settings": { "textProperty": "#text", "attributePrefix": "@", "inferTypes": false } }`.
- **ConversionSettings**: A key-value settings object carried by the `ConvertCommand`. Contains format-specific options for both the source adapter's `FromIM` and the target adapter's `ToIM` at the conversion boundary. Defaults apply for any omitted key (see FR-020).
- **MultiFormatScriptRunner**: Orchestrates segmented script execution. Pre-processes a TLio script to identify `convert` commands, splits the command list into ordered sections, and for each section creates the appropriate `IExecutionContext<TNode>`, executes all commands in that section, then at each boundary uses the `FormatConverter` to convert the document before handing to the next section's context.
- **ScriptSection**: An internal value type representing one segment between conversion boundaries — holds the list of commands for that segment and the format identifier governing its `TNode` type.
- **FormatConverter.TLio**: A TLio integration package that (a) provides `ConvertCommand`, (b) provides `MultiFormatScriptRunner`, and (c) wires FormatConverter adapters into TLio's registration mechanism. This is the only package that references both `FormatConverter.Core` and `TLio.Core`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Once adapters are registered, a TLio script can switch the working document from one format to another by adding a single `convert` command — one line of script configuration, no host-level conversion code required.
- **SC-002**: Round-trip conversion (format A → format B → format A) preserves all named properties, their values, their types, their ordering, and their nesting for documents containing only constructs common to both formats.
- **SC-003**: Adding a new format adapter requires implementing exactly 2 methods (`ToIM` and `FromIM`) plus registration — zero changes to existing adapters or the core converter are needed.
- **SC-004**: XML attributes survive every supported conversion path; no attribute data is silently dropped.
- **SC-005**: Conversion of a representative 1 MB document completes in under 2 seconds on standard developer hardware.
- **SC-006**: The library has zero runtime dependencies on Newtonsoft.Json or any non-.NET-built-in JSON library, verifiable by inspecting the published package dependency graph.
- **SC-007**: Each of the three initial format adapters (JSON, XML, YAML) is covered by automated tests that validate `ToIM` and `FromIM` independently and in round-trip combinations.
- **SC-008**: A single TLio script containing one or more `convert` commands can take an XML input, apply TLio commands in each format section (XML-native commands before the boundary, JSON/YAML-native commands after), and produce a final document in any target format — verified by an end-to-end integration test with no host-level conversion code outside the script.

## Assumptions

- **Scope**: The initial implementation targets JSON, XML, and YAML. The IM is designed to accommodate EDI constructs, but a full EDI adapter is out of scope for v1.
- **YAML library**: Because .NET has no built-in YAML parser, a lightweight open-source library (e.g., YamlDotNet — already used elsewhere in the repository) is acceptable for the YAML adapter, provided it introduces no Newtonsoft.Json transitive dependency.
- **Attribute convention**: When converting XML attributes to non-XML formats, the `@attributeName` prefix is the default (`attributePrefix` setting default). This aligns with Badgerfish/JsonML standards. The default can be overridden per `convert` command via `settings.attributePrefix`.
- **Text content convention**: XML text content maps to the key `#text` by default (`textProperty` setting default). Overridable per `convert` command.
- **Type inference**: Off by default (`inferTypes: false`) for all formats. Enabled per `convert` command via `settings.inferTypes`. When enabled, XML/YAML untyped text is coerced to integer, decimal, boolean, or null as appropriate.
- **Document root**: Every IM tree has exactly one root `IntermediateNode`. Multi-root inputs (invalid XML) and bare-scalar JSON values are rejected with a clear error.
- **Project structure**: The converter lives in a new, standalone solution/project separate from the existing TLio solution. A `FormatConverter.TLio` integration package bridges the converter to TLio via `ConvertCommand` and `MultiFormatScriptRunner`.
- **Registration pattern**: `ConvertCommand` is registered via TLio's existing command registration mechanism. Adapter registration (which format libraries are active) follows TLio's existing extension registration conventions.
- **IM visibility**: The Intermediate Model is an internal transport mechanism used only inside `MultiFormatScriptRunner` at section boundaries. Script authors never interact with IM nodes or call `ToIM`/`FromIM` directly — only the `convert` command is exposed.
- **Library only**: No CLI tool or HTTP API surface is required for v1; the deliverable is a class library (and optionally a NuGet package).
- **Licensing**: Some format adapter implementations may depend on third-party libraries that carry commercial or attribution licensing requirements. Explicit adapter registration is the mechanism by which callers opt in and take on those licensing obligations. The core IM and `FormatConverter` are license-free.
- **Namespace handling**: XML namespace URIs are preserved in `NodeMetadata`. Target formats with no namespace concept store them as metadata and do not render them unless they also support namespaces.
