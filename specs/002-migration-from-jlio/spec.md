# Spec 002 — Migration from JLio to TLio

**Branch:** `feat/002-migration-from-jlio`
**Status:** Draft

---

## Overview

JLio is a mature JSON transformation framework with 10 commands, 60+ functions, rich
JsonPath support, and an extensive test suite. TLio is its successor with a
format-neutral, generic architecture (`ICommand<TNode>`, `INodeAdapter<TNode>`,
`IItemsFetcher<TNode>`).

This spec governs the migration: every JLio capability must be reproduced in TLio
with **identical observable behaviour**, and TLio must additionally support two JSON
backends (Newtonsoft and System.Text.Json).

---

## Hard Requirements

These are non-negotiable and must be upheld in every implementation task.

1. **All JLio unit tests, ported to TLio.UnitTests and run against the Newtonsoft
   adapter, must pass with identical assertions.** No test may be weakened, removed,
   or have its assertion changed to accommodate an implementation difference.

2. **No format-specific code in TLio.Core or TLio.Commands.** Every format-specific
   operation is expressed through `INodeAdapter<TNode>` or `IItemsFetcher<TNode>`.

3. **The Newtonsoft and System.Text.Json adapters must produce identical results** for
   the same script + data input. If a test passes with Newtonsoft it must pass with
   System.Text.Json.

---

## User Stories

### US-01 — All existing JLio commands work via Newtonsoft adapter

As a JLio user migrating to TLio,
I want all 10 JLio commands (Add, Set, Put, Remove, Copy, Move, IfElse, DecisionTable,
Compare, Merge) to behave identically when run through TLio with the Newtonsoft JSON adapter,
so that I can migrate without rewriting scripts or changing expected outputs.

**Acceptance criteria:**
- Every JLio command test, ported to TLio, passes against `TLio.Json.JsonExecutionContext.CreateDefault()`.
- Legacy-syntax path tests (property derived from path leaf) pass.
- New-syntax property tests (explicit Property field) pass.
- Parent-navigation tests (`@.<--` paths) pass.
- `=indirect()` path expression tests pass.
- `DestinationAsArray` Copy/Move tests pass.

---

### US-02 — All existing JLio functions work via Newtonsoft adapter

As a JLio user,
I want all 60+ JLio functions (core + Math + Text + TimeDate extensions) to produce
the same results when run through TLio as they did in JLio,
so that existing scripts with embedded function expressions continue to work.

**Acceptance criteria:**
- All JLio function unit tests, ported to TLio, pass against the Newtonsoft adapter.
- Nested function calls (function as argument to another function) work.
- `=functionName(arg)` string expressions embedded in object/array script values are expanded correctly.
- `FixedValue<JToken>` processes nested `=func()` expressions in JSON objects and arrays.

---

### US-03 — All existing JLio commands work via System.Text.Json adapter

As a consumer targeting the Microsoft stack,
I want the same commands and functions to work via the System.Text.Json adapter,
so that I can avoid a Newtonsoft dependency.

**Acceptance criteria:**
- All ported JLio command tests also pass against `TLio.Json.SystemText.SystemTextJsonExecutionContext.CreateDefault()`.
- All ported JLio function tests pass against the System.Text.Json adapter.
- JsonPath expression coverage is equivalent for all ported JLio test cases: every test that passes against Newtonsoft must also pass against System.Text.Json.
- Edge-case deviations between `JsonCons.JsonPath` (RFC 9535) and Newtonsoft's Jayway-based `SelectTokens` are acceptable **only if** no ported JLio test asserts on the differing behaviour. Every such deviation must be documented in `specs/002-migration-from-jlio/jsonpath-compatibility.md`.

---

### US-04 — Script parsing is backward-compatible

As a JLio user,
I want my existing JSON-format JLio scripts to be parsed and executed by TLio without modification,
so that I do not need to rewrite scripts when migrating.

**Acceptance criteria:**
- A JLio JSON script string containing any combination of built-in commands and functions is correctly parsed by `ScriptEngine<TNode>`.
- The `"command"` discriminator field is respected.
- `"value"` fields containing `=functionName(args)` strings are correctly deserialized into `IFunctionSupportedValue<TNode>`.
- Unknown command names produce a warning log entry (not an exception), matching JLio's NotFoundCommand behaviour.

---

### US-05 — Extension packs can be ported to TLio

As a developer maintaining the Math, Text, TimeDate, and ETL extension packs,
I want to port each extension pack to TLio using the generic `FunctionBase<TNode>` and
`CommandBase<TNode>` base classes,
so that the extensions are format-agnostic and work across all data adapters.

**Acceptance criteria:**
- Each extension pack (Math, Text, TimeDate, ETL) has a TLio equivalent that compiles against TLio.Core only.
- All extension tests, ported to TLio, pass against the Newtonsoft adapter.

> **Out of scope — JSchema:** Porting `JLio.Extensions.JSchema` is deferred to a separate spec.
> JSchema validation introduces a dependency on a JSON Schema library that requires its own
> adapter design. It is not part of this migration spec.

---

### US-06 — JLio tests can be run as TLio tests with minimal change

As a developer maintaining the test suite,
I want to port JLio unit tests to TLio with only the following changes:
  1. Replace `JToken.Parse(...)` with the adapter's `Parse()` method (or keep using `JToken.Parse` for Newtonsoft tests).
  2. Replace `ExecutionContext.CreateDefault()` with the adapter-specific factory.
  3. Replace `JLioScript`/`JLioExecutionResult` with `TLioScript<JToken>`/`TLioExecutionResult<JToken>`.
  4. Replace `JLioEngine` with `ScriptEngine<JToken>`.

No test assertions may be weakened during porting.

**Acceptance criteria:**
- A porting guide document (`specs/002-migration-from-jlio/porting-guide.md`) lists the exact substitution table.
- A sample ported test file is committed as a reference.

---

## Out of Scope for This Spec

- New commands or functions not present in JLio.
- Performance optimization (except where needed to match JLio behaviour).
- Migration of the `JLio.Validation` project — separate spec.
- NuGet package naming and versioning strategy — separate spec.
