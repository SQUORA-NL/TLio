# Feature Specification: AI Reference Documentation for All Current Implementations

**Feature Branch**: `007-create-ai-ref-docs`
**Created**: 2026-04-06
**Status**: Draft

## User Scenarios & Testing

### User Story 1 — Command Reference (Priority: P1)

A script author (human or AI agent) needs to write a TLio script that uses a specific
command. They open the relevant `docs/ai-ref/commands/<Name>.md` file and immediately
learn the command's purpose, its JSON syntax, all available options with types and
defaults, and a working example — without reading source code.

**Why this priority**: Commands are the primary building block of every TLio script.
Misunderstanding a command's options (e.g., `Set` vs `Put` semantics, or `Copy`'s
`DestinationAsArray` flag) causes silent data corruption. This is the highest-value
documentation target.

**Independent Test**: Given only the `docs/ai-ref/commands/` folder, an AI agent
can produce a syntactically and semantically correct TLio script for each of the 10
core commands and 4 ETL extension commands without reading any source code.

**Acceptance Scenarios**:

1. **Given** a request to set a JSON property, **When** the agent reads `Set.md`,
   **Then** it produces `{ "command": "set", "path": "$.x", "property": "y", "value": "z" }`
   correctly distinguishing `path` (selector) from `property` (target key).
2. **Given** a request to copy nodes to an array, **When** the agent reads `Copy.md`,
   **Then** it includes `"destinationAsArray": true` in the script.
3. **Given** a request for conditional branching, **When** the agent reads `IfElse.md`,
   **Then** it produces a script with `condition`, `ifScript`, and `elseScript` fields.
4. **Given** a request to apply a decision table, **When** the agent reads
   `DecisionTable.md`, **Then** it correctly structures `inputs`, `outputs`, `rules`,
   and `strategy` within the `config` object.

---

### User Story 2 — Function Reference (Priority: P1)

A script author needs to embed a function call as a value in a command. They open
`docs/ai-ref/functions/<Name>.md` and learn the call syntax, argument types/count,
and what the function returns.

**Why this priority**: Functions appear as values inside commands; getting the argument
count or syntax wrong causes runtime failures. Priority equals commands since they are
used together.

**Independent Test**: Given only the `docs/ai-ref/functions/` folder, an agent can
produce correct `value` expressions using each of the 6 concrete functions (Datetime,
Fetch, Indirect, Partial, Promote, ScriptPath).

**Acceptance Scenarios**:

1. **Given** a request to set a field to the current UTC date, **When** the agent
   reads `Datetime.md`, **Then** it produces `{ "value": { "=datetime": [] } }` or
   the correct format-argument variant.
2. **Given** a request to use an indirect path, **When** the agent reads `Indirect.md`,
   **Then** it produces a two-step path resolution expression correctly.
3. **Given** a request to promote a nested node, **When** the agent reads `Promote.md`,
   **Then** it wraps the node in a named-key object correctly.

---

### User Story 3 — Adapter & Format Selection (Priority: P2)

A script author needs to choose between JSON (Newtonsoft vs System.Text.Json), XML
(slash-path vs XPath), or YAML. They open `docs/ai-ref/overview.md` and find a single
decision table covering adapter selection, path syntax per format, and JSONPath
feature differences between the two JSON adapters.

**Why this priority**: The adapter/path mismatch is the most common cross-cutting error
(e.g., using `$.` syntax against an XML document). The overview prevents this class
of mistake.

**Independent Test**: Given only `docs/ai-ref/overview.md`, an agent correctly selects
the adapter, execution context factory, and path style for each of the 5 adapter
variants (json-newtonsoft, json-systemtext, xml-slashpath, xml-xpath, yaml).

**Acceptance Scenarios**:

1. **Given** a JSON document with filter-expression paths, **When** the agent reads
   the overview, **Then** it selects `TLio.Json` (Newtonsoft) because `TLio.Json.SystemText`
   does not support script expressions.
2. **Given** an XML document with attribute predicates (`item[@id='1']`), **When** the
   agent reads the overview, **Then** it selects `XmlExecutionContext.CreateWithNativeXPath()`.
3. **Given** a YAML document, **When** the agent reads the overview, **Then** it uses
   dot-notation paths (`$.root.child`) with `YamlExecutionContext.Create()`.

---

### Edge Cases

- The `DecisionTable` command has deeply nested config — the ai-ref must make the
  `inputs`/`outputs`/`rules`/`strategy`/`defaultResults` structure clear with a
  complete JSON example.
- The ETL commands (`Flatten`, `Restore`, `Resolve`, `ToCsv`) have many settings;
  the ai-ref must list required vs optional options clearly, staying within 150 lines.
- The `Placeholder.cs` file is a stub — no ai-ref.md should be created for it.
- Abstract base classes (`PropertyChangeCommand`, `CopyMoveBase`) are not standalone
  commands — no ai-ref.md for them.

## Requirements

### Functional Requirements

- **FR-001**: `docs/ai-ref/overview.md` MUST exist and contain the adapter selection
  table and JSONPath Newtonsoft vs System.Text.Json comparison table.
- **FR-002**: Each of the 10 core commands MUST have a `docs/ai-ref/commands/<Name>.md`
  file: `Add`, `Copy`, `Move`, `Put`, `Set`, `Remove`, `IfElse`, `DecisionTable`,
  `Compare`, `Merge`.
- **FR-003**: Each of the 4 ETL extension commands MUST have a
  `docs/ai-ref/commands/<Name>.md` file: `Flatten`, `Restore`, `Resolve`, `ToCsv`.
- **FR-004**: Each of the 6 concrete functions MUST have a
  `docs/ai-ref/functions/<Name>.md` file: `Datetime`, `Fetch`, `Indirect`, `Partial`,
  `Promote`, `ScriptPath`.
- **FR-005**: Each of the 5 adapter variants MUST have a
  `docs/ai-ref/adapters/<name>.md` file: `json-newtonsoft`, `json-systemtext`,
  `xml-slashpath`, `xml-xpath`, `yaml`.
- **FR-006**: Every ai-ref.md file MUST contain the mandatory sections defined in
  constitution Article XI: a one-sentence purpose, Syntax, Options table, Formats
  note, and at least one complete JSON example.
- **FR-007**: Every file MUST stay within 150 lines.
- **FR-008**: Option rows MUST state concrete types (`string`, `integer`, `boolean`,
  `TLioValue`, `object`). No "any" or untyped rows permitted.
- **FR-009**: Command ai-ref files MUST cross-reference `docs/ai-ref/overview.md`
  for path syntax rather than duplicating the JSONPath table.

### Key Entities

- **CommandRef**: name, purpose, syntax (JSON snippet), options table, formats note,
  examples.
- **FunctionRef**: name, purpose, call syntax, arguments table (name, type, required,
  default, description), return type, example.
- **AdapterRef**: adapter name, library, execution context factory, path style, path
  examples, notable limitations.
- **Overview**: adapter selection table, JSONPath compatibility table, format-specific
  notes.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% of concrete commands (14) have a corresponding ai-ref.md file
  under `docs/ai-ref/commands/`.
- **SC-002**: 100% of concrete functions (6) have a corresponding ai-ref.md file
  under `docs/ai-ref/functions/`.
- **SC-003**: All 5 adapter variant files exist under `docs/ai-ref/adapters/`.
- **SC-004**: `docs/ai-ref/overview.md` exists with both required tables.
- **SC-005**: No ai-ref.md file exceeds 150 lines.
- **SC-006**: The PowerShell compliance check from constitution Article XI returns
  zero missing files.
- **SC-007**: An AI agent given only the `docs/ai-ref/` folder can produce a
  syntactically valid TLio script for each command without referencing source code
  (manual verification against known fixture inputs).

## Assumptions

- The `Placeholder.cs` function stub is not yet implemented and does not require an
  ai-ref.md file.
- Abstract base classes (`PropertyChangeCommand`, `CopyMoveBase`, `CommandBase`,
  `FunctionBase`) are implementation details and do not require ai-ref.md files.
- Supporting infrastructure classes (`YamlParentTracker`, `YamlScriptParser`) are not
  script-authoring concerns and do not require ai-ref.md files.
- The ETL commands live in `TLio.Extensions.ETL/` but are treated as first-class
  commands for documentation purposes.
- The `TLio.Client` project does not expose script-authoring surface and does not
  require adapter-level documentation.
