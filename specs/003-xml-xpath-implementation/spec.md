# Feature Specification: Native XPath Adapter + Adapter-Scoped Test Projects

**Feature Branch**: `003-xml-xpath-implementation`
**Created**: 2026-03-28
**Status**: Implemented
**Input**: Derived from plan.md and research.md post-implementation

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Choose XPath path style for XML scripts (Priority: P1)

A developer writing TLio scripts against XML documents wants to use genuine XPath
expressions (`address/city`, `//name`, `items/item[@id='1']`) rather than the existing
slash-path notation. They want to select the path style once at context creation time
and have all commands honour it transparently.

**Why this priority**: The existing slash-path notation is a non-standard workaround.
Native XPath unlocks recursive descent (`//`), predicates, and positional selectors
that slash-path cannot express. This is the core deliverable.

**Independent Test**: Can be fully tested by creating an `XmlExecutionContext.CreateWithNativeXPath()`
context, running a `<set path="//city">new</set>` script against an XML document, and
verifying the result — independently of test-project reorganisation.

**Acceptance Scenarios**:

1. **Given** an XML document with nested elements, **When** a script uses `<set path="address/city">Paris</set>` with `CreateWithNativeXPath()`, **Then** the `city` element under `address` is updated.
2. **Given** an XML document with elements at any depth, **When** a script uses `<remove path="//obsolete"/>`, **Then** all elements named `obsolete` at any depth are removed.
3. **Given** an XML document with a sequence, **When** a script uses `<set path="items/item[1]">first</set>`, **Then** the first `item` is updated.
4. **Given** a caller using `CreateWithSlashPaths()`, **When** they run existing scripts with `/name` notation, **Then** behaviour is identical to before the change (backward compatibility preserved).
5. **Given** a caller using `CreateDefault()`, **When** they run their scripts, **Then** they get slash-path behaviour (alias unchanged).

---

### User Story 2 — XML tests live in their own project (Priority: P2)

A developer maintaining TLio wants XML adapter tests to live in `TLio.Xml.Tests`
so that the central `TLio.UnitTests` project does not depend on `System.Xml` or
`TLio.Xml`. Format-specific tests are co-located with their adapter.

**Why this priority**: Removing format dependencies from the central test project enforces
Article I (Format Neutrality) even in test code. It also shortens CI runs for core-only changes.

**Independent Test**: Can be fully tested by verifying `dotnet test TLio.Xml.Tests` passes
and `grep -rn "System\.Xml" TLio.UnitTests/` returns zero results.

**Acceptance Scenarios**:

1. **Given** the solution is built, **When** `dotnet test TLio.Xml.Tests` is run, **Then** all SlashPath and NativeXPath fixture tests pass.
2. **Given** `TLio.UnitTests`, **When** its project file is inspected, **Then** there is no `<ProjectReference>` to `TLio.Xml` or `TLio.Yaml`, and no XML/YAML fixture content.
3. **Given** the XML fixture set (Set, Add, Put, Remove, Copy, Move), **When** moved to `TLio.Xml.Tests`, **Then** no fixture is lost — test count is conserved.

---

### User Story 3 — YAML tests live in their own project (Priority: P3)

A developer maintaining TLio wants YAML adapter tests to live in `TLio.Yaml.Tests`
for the same reasons as the XML separation above.

**Why this priority**: Mirrors User Story 2 for YAML; independently testable and
can be deferred without blocking US1 or US2.

**Independent Test**: Can be fully tested by verifying `dotnet test TLio.Yaml.Tests` passes
and `grep -rn "YamlDotNet" TLio.UnitTests/` returns zero results.

**Acceptance Scenarios**:

1. **Given** the solution is built, **When** `dotnet test TLio.Yaml.Tests` is run, **Then** all YAML fixture tests pass.
2. **Given** `TLio.UnitTests`, **When** its project file is inspected, **Then** there is no `<ProjectReference>` to `TLio.Yaml` and no YAML fixture content.

---

### Edge Cases

- What happens when a NativeXPath path contains a predicate (`item[@id='1']`) as a Copy/Move destination? → `EnsurePath` is a no-op for predicate paths; the command logs a warning and skips creation.
- What happens when `//name` matches zero elements? → `SelectNodes` returns an empty collection; commands silently skip with no error.
- What happens when existing callers use `CreateDefault()`? → Alias for `CreateWithSlashPaths()`; no behaviour change.
- What happens when a path starts with `/` in NativeXPath mode? → Absolute paths are not supported in v1; behaviour is undefined (relative-only is documented).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The library MUST provide `NativeXPathItemsFetcher` as a second `IItemsFetcher<XElement>` implementation that forwards XPath expressions directly to `XElement.XPathSelectElements()` without stripping or rewriting.
- **FR-002**: `NativeXPathItemsFetcher` MUST use `.` as its root path indicator and `/` as its path delimiter.
- **FR-003**: `NativeXPathItemsFetcher.EnsurePath` MUST create missing intermediate and leaf elements for simple `a/b/c` paths, and MUST be a no-op (with logged warning) for paths containing XPath predicates or axes.
- **FR-004**: `NativeXPathItemsFetcher.SplitParentAndLeaf` MUST split at the last `/` that is not inside square brackets, so `items/item[1]` splits to parent=`items`, leaf=`item[1]`.
- **FR-005**: `XmlExecutionContext` MUST expose `CreateWithSlashPaths()` and `CreateWithNativeXPath()` factory methods, and MUST keep `CreateDefault()` as an alias for `CreateWithSlashPaths()`.
- **FR-006**: The existing `XPathItemsFetcher` MUST be renamed `SlashPathItemsFetcher` to make its slash-path convention self-documenting.
- **FR-007**: `TLio.Xml.Tests` MUST be a dedicated NUnit test project referencing only `TLio.Xml` and `TLio.Client`; it MUST contain SlashPath and NativeXPath fixture suites.
- **FR-008**: `TLio.Yaml.Tests` MUST be a dedicated NUnit test project referencing only `TLio.Yaml` and `TLio.Client`; it MUST contain YAML fixture suites.
- **FR-009**: `TLio.UnitTests` MUST NOT reference `TLio.Xml`, `TLio.Yaml`, `System.Xml`, or `YamlDotNet` after migration.
- **FR-010**: All fixture files MUST use the single-file format: XML fixtures as `<fixture><input>…</input><script>…</script><result>…</result></fixture>`; YAML fixtures as three YAML documents separated by `---`.

### Key Entities

- **SlashPathItemsFetcher**: Existing fetcher renamed; strips leading `/` to produce relative XPath. Root indicator `/`.
- **NativeXPathItemsFetcher**: New fetcher; passes XPath through unchanged. Root indicator `.`. Supports `//`, predicates, positional selectors.
- **XmlExecutionContext**: Factory class; exposes `CreateWithSlashPaths()`, `CreateWithNativeXPath()`, `CreateDefault()`.
- **TLio.Xml.Tests**: New test project; sub-namespaces `SlashPath` and `NativeXPath`; fixture files under `Fixtures/XmlSet/`, `Fixtures/XPathSet/`, etc.
- **TLio.Yaml.Tests**: New test project; sub-namespace `Yaml`; fixture files under `Fixtures/YamlSet/`, etc.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing tests pass without modification after the `XPathItemsFetcher` → `SlashPathItemsFetcher` rename (zero regressions).
- **SC-002**: `dotnet test TLio.Xml.Tests` reports all fixture tests passing for both SlashPath and NativeXPath suites.
- **SC-003**: `dotnet test TLio.Yaml.Tests` reports all fixture tests passing.
- **SC-004**: `grep -rn "System\.Xml\|YamlDotNet" TLio.UnitTests/` (source files only) returns zero matches.
- **SC-005**: The NativeXPath suite includes at least one fixture exercising `//` recursive descent that would not be expressible with the slash-path fetcher.
- **SC-006**: The full solution (`dotnet test TLio.sln`) passes with total count equal to the sum of individual project counts.

## Assumptions

- NativeXPath paths are relative-only in v1; absolute paths starting with `/` are not supported (documented in research.md).
- `EnsurePath` for NativeXPath supports only simple `a/b/c` chains; predicate and axis paths are read-only destinations.
- `CreateDefault()` remains as a backward-compatibility alias and will not be removed until a deprecation notice is added in a future spec.
- XML fixture files use a single-file `fixture.xml` format introduced during the 002-migration-from-jlio spec; YAML uses three-document `fixture.yaml`.
- No new production assembly is introduced — `NativeXPathItemsFetcher` lives inside the existing `TLio.Xml` project.
