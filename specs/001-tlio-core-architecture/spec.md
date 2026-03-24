# Spec 001 — TLio Core Architecture

**Branch:** `feat/001-tlio-core-architecture`
**Status:** Draft

---

## Overview

TLio is a successor to JLio that extends the concept of script-driven data
transformation beyond JSON to support XML, YAML, and any future structured data
format. The core insight is that the *logical* operations (set a value, add a
property, remove a node, copy/move structure) are format-independent, while the
*mechanical* operations (how to select nodes, how to read/write a value) are
format-specific.

This specification defines what the framework must do from a user and consumer
perspective.

---

## User Stories

### US-01 — Execute a script against a JSON document

As a developer consuming TLio,
I want to run a TLio script against a JSON document
so that I can transform JSON data the same way I did with JLio, without rewriting scripts.

**Acceptance criteria:**
- A TLio script that was valid JLio syntax executes correctly against a JSON document.
- The result is the expected transformed JSON.
- Execution errors are captured in the execution log, not thrown as exceptions.

---

### US-02 — Execute the same script against an XML document

As a developer consuming TLio,
I want to run a TLio script against an XML document
so that I can apply the same transformation logic to XML data without writing a
different scripting engine.

**Acceptance criteria:**
- A script that sets, adds, removes, copies, and moves nodes executes against an XElement root.
- The result is the expected transformed XML.
- The script text itself does not contain any XML-specific syntax unless the path expression does.

---

### US-03 — Execute the same script against a YAML document

As a developer consuming TLio,
I want to run a TLio script against a YAML document,
so that YAML data can be transformed using the same command vocabulary.

**Acceptance criteria:**
- Set, Add, Remove, Copy, Move commands function correctly against a parsed YamlNode tree.
- Round-trip (parse → transform → serialise) produces valid YAML.

---

### US-04 — Swap the path-selection strategy at runtime

As an advanced user,
I want to provide my own IItemsFetcher implementation when creating an execution context,
so that I can use a custom path language or extend the built-in one (e.g. add indirect-reference support).

**Acceptance criteria:**
- Providing a custom IItemsFetcher to ExecutionContext replaces the built-in one.
- All commands use only the injected fetcher; no fallback to a hard-coded implementation exists.

---

### US-05 — Register custom commands

As an extension author,
I want to register my own ICommand<TNode> implementations via ICommandsProviderRegistrar,
so that the scripting engine recognises my commands by name and can execute them.

**Acceptance criteria:**
- A custom command registered by name is resolvable by the ScriptEngine.
- The custom command receives the correct IExecutionContext<TNode> at runtime.
- The registration does not require modifying TLio.Core or TLio.Commands source code.

---

### US-06 — Use functions as values in commands

As a script author,
I want to use function calls (e.g. `concat($.first, " ", $.last)`) as the value
argument in Set/Add/Put commands,
so that computed values can be written back into the document.

**Acceptance criteria:**
- A function call in a script produces the correct computed TNode value.
- The result is inserted/set at the target path.
- Functions can be nested (the argument of one function can itself be a function call).

---

### US-07 — Validate a script before execution

As a developer,
I want to call Validate() on a TLioScript before executing it,
so that I can surface configuration errors (missing paths, missing values) early.

**Acceptance criteria:**
- ValidateCommandInstance() returns a ValidationResult with human-readable messages for each error.
- A script with no errors returns IsValid = true.
- Validation does not modify the data document.

---

## Non-Functional Requirements

- **No format coupling in Core or Commands.** TLio.Core and TLio.Commands must have
  zero references to Newtonsoft.Json, System.Xml, or YamlDotNet.
- **Net10.0 target.** All projects target `net10.0`.
- **Nullable reference types enabled** in all projects.
- **All execution errors logged, never thrown.** Commands must not propagate exceptions
  for expected conditions (wrong type, missing path).

---

## Out of Scope for This Spec

- Script serialisation format (JSON-based script text parsing is a separate spec).
- Decision tables, IfElse, ETL extensions — these are separate specs.
- Performance optimisation — not a concern for the initial implementation.
