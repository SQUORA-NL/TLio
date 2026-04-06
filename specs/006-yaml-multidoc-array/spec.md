# Feature Specification: YAML Multi-Document as Array Root

**Feature Branch**: `006-yaml-multidoc-array`
**Created**: 2026-04-06
**Status**: Draft

## Overview

YAML supports multi-document files — a single file containing several YAML
documents separated by `---` markers. TLio currently silently discards every
document after the first, making multi-document inputs unusable.

A YAML multi-document file is semantically equivalent to a JSON file whose
root is an array: both are "a collection of items at the top level". This
feature closes the gap by treating a multi-document YAML input as an array
root (`YamlSequenceNode`) so that the same TLio scripts that operate on
JSON arrays work identically on YAML multi-document inputs.

---

## User Scenarios & Testing

### User Story 1 — Run a script against a multi-document YAML input (Priority: P1)

As a user who has a YAML data file with multiple documents (e.g. a list of
records separated by `---`), I want to pass it to TLio and have scripts
address each document as an element of an array, so I can transform
multi-document YAML the same way I transform JSON arrays.

**Why this priority**: Core parity gap — the same logical data structure
behaves differently depending on whether it is expressed as JSON or YAML.
This is the primary motivation for the feature.

**Independent Test**: Given a two-document YAML input and a script that
reads `[0].name`, the script returns the name from the first document.

**Acceptance Scenarios**:

1. **Given** a YAML input with two `---`-separated documents, **When** TLio
   parses it, **Then** the root node is an array containing one element per
   document.

2. **Given** a YAML input with a single document (no `---` separator),
   **When** TLio parses it, **Then** behavior is identical to before this
   change (single root node returned as-is).

3. **Given** a multi-document YAML input and a script that sets a field on
   each element, **When** the script is executed, **Then** each document's
   field is updated and the result serializes as a valid YAML sequence.

---

### User Story 2 — Serialize an array root back to YAML (Priority: P2)

As a user whose transformed result is an array root, I want the serialized
output to be valid YAML so I can write the result to a file or display it.

**Why this priority**: Without correct serialization, the round-trip is
broken. Depends on US1 parsing being correct.

**Independent Test**: A `YamlSequenceNode` root serializes as a valid YAML
document with a sequence at the top level (e.g. `- name: Alice\n- name: Bob`).

**Acceptance Scenarios**:

1. **Given** a `YamlSequenceNode` as the root, **When** `Serialize` is
   called, **Then** the output is valid YAML with a sequence at the root.

2. **Given** the output of a transformed multi-document input, **When**
   written and re-parsed, **Then** the round-trip produces an equivalent
   data structure.

---

### Edge Cases

- What happens when a multi-document YAML file has zero documents? → Return
  an empty array root.
- What happens when one of the documents in a multi-doc file is empty or
  contains only comments? → That document is represented as a null element
  in the resulting array.
- What happens when a single-document YAML file already has a sequence at
  its root? → Behavior is unchanged; parsed as-is (not double-wrapped).

---

## Requirements

### Functional Requirements

- **FR-001**: The parser MUST treat a YAML input with N documents (N > 1)
  as a `YamlSequenceNode` containing N child nodes, one per document root.
- **FR-002**: The parser MUST leave single-document YAML inputs unchanged
  (returns the document's root node directly).
- **FR-003**: A `YamlSequenceNode` at the root MUST serialize to a valid
  YAML document with a top-level sequence.
- **FR-004**: All existing single-document test fixtures MUST continue to
  pass without modification.
- **FR-005**: A multi-document YAML input MUST be addressable using the
  same array path expressions used for JSON array inputs (e.g. `[0]`,
  `[*]`).

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: All existing YAML adapter tests pass without modification.
- **SC-002**: A new fixture set exercises multi-document YAML parse →
  transform → serialize and all scenarios pass.
- **SC-003**: A multi-document YAML input processed by a script that
  already works on an equivalent JSON array produces an identical logical
  result.

---

## Assumptions

- Multi-document YAML is delimited by `---` markers (YamlDotNet already
  handles this; no custom parsing is required).
- Serialization of an array root produces a YAML sequence in a single
  document (not multi-document format); this is consistent with JSON
  behavior and requires no new serialization mode.
- The YAML script format (list of commands in `YamlScriptParser`) is
  unaffected — it already expects a sequence at the root of the script,
  which remains a single-document YAML.
- The fix is isolated to `YamlNodeAdapter.Parse`; no changes to
  `YamlScriptParser`, `YamlPathItemsFetcher`, or `YamlParentTracker` are
  expected.
