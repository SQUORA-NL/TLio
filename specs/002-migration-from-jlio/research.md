# Research — 002 Migration from JLio to TLio

**Feature**: Migration from JLio to TLio
**Branch**: `002-migration-from-jlio`
**Date**: 2026-03-24
**Status**: Complete — all NEEDS CLARIFICATION resolved

---

## Decision 1 — JsonPath library for System.Text.Json adapter

**Decision**: Use `JsonPath.Net` (part of json-everything suite, Greg Dennis) as the default `IJsonPathProvider<JsonNode>` implementation.

**Rationale**:
- RFC 9535 compliant — the modern JsonPath standard
- Native `System.Text.Json` support — no Newtonsoft dependency
- Actively maintained (2024–2025 releases)
- Supports filter expressions, recursive descent, wildcards — sufficient for all JLio paths
- Behind `IJsonPathProvider<TNode>` interface, so it can be replaced without changing adapter or core code

**Alternatives considered**:
- `JsonCons.JsonPath`: Also System.Text.Json native and RFC 9535, but `JsonPath.Net` has a broader adoption surface and better documentation
- `Newtonsoft.Json` `SelectTokens`: Only works with `JToken`; not available for `JsonNode`
- Custom implementation: Over-engineering for a first iteration

**Known gap**: JsonPath.Net (RFC 9535) and Newtonsoft's JsonPath (Jayway-based) differ in edge cases:
- Recursive descent on leaf nodes: RFC 9535 excludes, Jayway includes
- Some filter expression syntax differences
Where Newtonsoft is the observable authoritative result and a ported JLio test asserts on the differing case, `SystemTextJsonPathItemsFetcher` must work around the divergence.

---

## Decision 2 — Canonical adapter: Newtonsoft

**Decision**: Newtonsoft.Json (`JToken`) is the canonical adapter. System.Text.Json must match Newtonsoft semantics.

**Rationale**: JLio uses Newtonsoft exclusively. All existing tests, scripts, and expected outputs are calibrated to Newtonsoft behaviour. Designating Newtonsoft as canonical avoids rewriting the reference test suite.

**Alternatives considered**: Treating both adapters as peers — rejected because it would require defining an independent "TLio spec" for every edge case that differs between the two libraries, which is scope creep.

---

## Decision 3 — Script format stays JSON regardless of data format

**Decision**: TLio scripts remain in JSON format even when processing XML or YAML data.

**Rationale**:
- Full backward compatibility with JLio scripts — no migration cost for script authors
- Simplest path: the `ScriptEngine<TNode>` always deserialises the script from JSON, then routes commands through the registered adapter for data operations
- Separate spec for XML/YAML scripts if ever needed

**Alternatives considered**: Matching script format to data format — rejected as over-engineering with no benefit for the migration scope.

---

## Decision 4 — IJsonPathProvider abstraction

**Decision**: Introduce `IJsonPathProvider<TNode>` in `TLio.Core` to abstract the JsonPath evaluation mechanism, with `JsonPath.Net`-backed default in `TLio.Json.SystemText`.

**Rationale**:
- Enables substitution of the JsonPath library without changing adapter code (Article V)
- Allows patching RFC 9535 vs Jayway divergences through a custom implementation
- Consumers can register `IJsonPathProvider<JsonNode>` in DI without forking the adapter

**Source**: spec.md clarification 2026-03-24.

---

## Decision 5 — FixedValue<JToken> exception for Newtonsoft

**Decision**: `FixedValue<JToken>` in `TLio.Json` may hold a raw `JToken` from the script and expand nested `=func()` strings using `FunctionConverter<JToken>`. This is technically format-specific but is confined to `TLio.Json`.

**Rationale**: Mirrors JLio's design exactly. The Newtonsoft adapter already has the `JToken` available from JSON script parsing; converting it through an adapter would be extra indirection for no benefit.

**Alternatives considered**: Forcing all `FixedValue<TNode>` through a generic converter — rejected; over-engineering for a single format.

---

## Decision 6 — Test fixture format: triplet (input.json / script.json / result.json)

**Decision**: Each JLio test case is extracted as a self-contained fixture directory with three files:
- `input.json` — data document
- `script.json` — the TLio script
- `result.json` — expected output after execution

**Rationale**:
- Format-agnostic: the same fixture can be run against both adapters
- Self-contained: no need to understand C# test code to understand what's being tested
- Discoverable: a single test runner scans a directory tree and parameterises all fixtures
- Matches the US-06 acceptance criteria in spec.md

**Fixture source**: https://github.com/nexxbiz/JLio (extract from existing xUnit/NUnit tests).

---

## Decision 7 — Extension pack porting priority

**Decision**: Math → Text → TimeDate → ETL → JSchema.

**Rationale**:
- Math, Text, TimeDate are expression packs used inside `=functionName()` values; they have the broadest script coverage
- ETL commands (Flatten, Restore, Resolve, ToCsv) are more specialised
- JSchema depends on a validation library — separate spec concern

**Source**: spec.md clarification 2026-03-24.

---

## Decision 8 — Error contract

**Decision**: TLio must reproduce JLio's exact error behaviour. Any observable deviation (exception vs log entry, error message text, result value) is a bug.

**Rationale**: The migration contract requires identical observable behaviour. JLio's error handling is part of the observable behaviour and is tested by the ported fixture suite.

**Practical implication**: Where JLio silently ignores an error (unknown command → `NotFoundCommand` warning log, unmatched path → no-op), TLio must also ignore it. Where JLio throws, TLio must throw the equivalent.

---

## JLio repository structure (reference)

**Source**: https://github.com/nexxbiz/JLio

Key projects to mirror:
| JLio project | TLio equivalent |
|---|---|
| `JLio.Core` | `TLio.Core` |
| `JLio.Client` | `TLio.Client` |
| `JLio.Functions` | `TLio.Functions` |
| `JLio.Commands` | `TLio.Commands` |
| `JLio.UnitTests` | `TLio.UnitTests` |
| `JLio.Extensions.Math` | `TLio.Extensions.Math` (future) |
| `JLio.Extensions.Text` | `TLio.Extensions.Text` (future) |
| `JLio.Extensions.TimeDate` | `TLio.Extensions.TimeDate` (future) |
| `JLio.Extensions.ETL` | `TLio.Extensions.ETL` (future) |
| `JLio.Extensions.JSchema` | `TLio.Extensions.JSchema` (future) |

Test count reference: 73 test files → all to be ported as fixture triplets.
