<!--
  SYNC IMPACT REPORT
  Version change: [PLACEHOLDER] → 1.0.0
  Modified: all placeholder tokens replaced with TLio-specific content.

  Added sections:
  - Articles I–X (Format Neutrality through Logging is Observability)
  - Per-article compliance grep commands
  - Article VI: file-based fixture triplet requirement (xUnit Theory)
  - Article VIII: porting-guide.md obligation for dropped JLio behaviour
  - Governance section (amendment procedure, versioning policy, compliance review,
    canonical source table)

  Templates updated:
  - .specify/templates/plan-template.md   ✅ Constitution Check section
  - .specify/templates/tasks-template.md  ✅ test-discipline notes + fixture requirement
  - .specify/templates/spec-template.md   — no TLio-specific changes needed
  - .specify/templates/speckit.implement.md  — already TLio-specific (no change)
  - .specify/templates/speckit.plan.md       — already TLio-specific (no change)
  - .specify/templates/speckit.tasks.md      — already TLio-specific (no change)

  Deferred TODOs: none
-->

# TLio Constitution

> Immutable architectural principles that govern every implementation decision in TLio.
> All specifications, plans, and generated code MUST comply with these articles.
> When a principle conflicts with a local convenience, the principle wins.

---

## Article I — Format Neutrality

TLio is a **data-format-agnostic scripting framework**. No project in the solution
(except dedicated adapter assemblies) may reference any concrete data-format library
(Newtonsoft.Json, System.Xml, YamlDotNet, etc.) directly. All format-specific
operations are expressed exclusively through `INodeAdapter<TNode>` and
`IItemsFetcher<TNode>`.

*Adapter assemblies* (`TLio.Json`, `TLio.Xml`, `TLio.Yaml`, …) are the only permitted
exceptions to this rule.

**Compliance check — run before every PR merge:**

```sh
grep -rn "Newtonsoft\|System\.Xml\|YamlDotNet\|JToken\|JObject\|JArray\|JValue\|XElement\|YamlNode" \
  TLio.Core/ TLio.Commands/ TLio.Functions/
```

→ Must return **zero results**. Comments that mention type names as examples are
acceptable; `using` statements and code references are not.

---

## Article II — Dependency Inversion

All cross-cutting behaviour is delivered through interfaces, not concrete classes.
No command, function, or engine component may `new` up an adapter or fetcher.
Everything is injected through `IExecutionContext<TNode>`.

**Compliance check:**

```sh
grep -rn "new.*Adapter\|new.*Fetcher\|new.*ExecutionContext" \
  TLio.Commands/ TLio.Functions/
```

→ Must return **zero results** (test helpers in `TLio.UnitTests/` are exempt).

---

## Article III — Generic-First Design

The generic type parameter `TNode` travels all the way from `ICommand<TNode>` through
to `TLioScript<TNode>` and `ScriptEngine<TNode>`. No boxing to `object`, no casting,
no reflection-based dispatch is permitted in Core or Commands.

**Rule:** Every public type and method in `TLio.Core`, `TLio.Commands`, and
`TLio.Functions` that operates on nodes MUST carry `<TNode>` as a generic parameter.
`object` must not appear as a parameter or return type in command/function signatures.

---

## Article IV — Separation of Process and Execution

A **command** is a pure orchestrator. It defines the *process*:

1. **Find** — select target nodes via `context.ItemsFetcher.SelectNodes(path, data)`
2. **Compute** — resolve the value via `value.GetValue(node, data, context)`
3. **Operate** — mutate nodes via `context.NodeAdapter.<Operation>(node, ...)`

A **function** follows the same pattern to produce a value:

1. **Find** argument values via `context.ItemsFetcher.SelectNodes(...)` or evaluate
   sub-functions
2. **Compute** the result
3. **Produce** the output via `context.NodeAdapter.Create*(...)` or
   `context.NodeAdapter.DeepClone(...)`

**Hard rule:** No method body in `TLio.Commands` or `TLio.Functions` may call any
method directly on a `TNode` variable. Every interaction with a node — reading its
value, checking its type, or modifying it — MUST go through `context.NodeAdapter`
or `context.ItemsFetcher`. A variable of type `TNode` may only be passed as an
argument; it must never be the receiver of a method call.

Commands must not contain format-specific code, branching on `TNode`, or conditional
logic based on the concrete type of `TNode`.

**Correct pattern:**

```csharp
// ✅ CORRECT — command orchestrates through adapters
var nodes = context.ItemsFetcher.SelectNodes(path, data);
context.NodeAdapter.SetProperty(targetNode, propertyName, value);
bool isObj = context.NodeAdapter.IsObject(targetNode);
```

**Forbidden pattern:**

```csharp
// ❌ WRONG — violates Article IV (format-specific + direct method call on TNode)
if (targetNode is JObject obj) obj[propertyName] = value;
targetNode.ToString();
((JValue)targetNode).Value = 42;
```

---

## Article V — Swappable Selection

The path-selection mechanism (`IItemsFetcher<TNode>`) is a first-class extension point.
JsonPath, XPath, dot-notation, or any other path language may be plugged in without
changing any command or function code. The fetcher is always resolved from the context,
never hard-coded.

**Rule:** No command or function may construct a path string internally (e.g., hardcode
`"$."` or `"/"`). Paths are always supplied by callers, never built inside commands.

---

## Article VI — Test-First Imperative

No implementation code is written before:

1. A specification exists in `specs/<feature>/spec.md`
2. The corresponding `plan.md` and `tasks.md` are in place
3. At least a failing unit test exists for the feature

**File-based fixture triplets (MANDATORY for all command and function tests):**

Every test that exercises a full script execution MUST use file-based fixture triplets:

```
TLio.UnitTests/
  <Category>Tests/
    Fixtures/
      <ScenarioName>/
        input.json    ← starting data document
        script.json   ← TLioScript to execute (serialised command/function)
        result.json   ← expected output document after execution
```

Tests are driven as xUnit `Theory` with `[MemberData]` or `[ClassData]` that loads
all triplets from the fixture folder. This pattern makes test cases:

- **Format-agnostic** — the same triplet can be replayed against any adapter.
- **Editable without recompiling** — add a new scenario by dropping files.
- **Reviewable** — diffs show data changes, not C# string escaping.

Inline `[TestCase]` / hardcoded data MAY be used only for unit-level edge cases
(e.g., validation logic with null/empty paths, argument parsing). Any test that
exercises a full script execution MUST use the file-based triplet pattern.

**Existing inline tests** that predate this rule MUST be tracked in `tasks.md` under
a dedicated "Refactor inline tests → fixture triplets" task and completed before the
feature is considered done.

---

## Article VII — Simplicity Gate

Before adding a new project or layer, the following question MUST be answered in the
relevant `plan.md`:

> "Could this be implemented with fewer projects and still satisfy Articles I–V?"

If yes, the simpler option is preferred. Unjustified complexity is a constitution
violation and must be called out in code review.

---

## Article VIII — Backward Migration Path

TLio provides a JSON adapter (`TLio.Json`) that is **behaviourally equivalent** to
JLio's existing commands and functions. This enables incremental migration: consumers
can swap the JLio scripting engine for TLio without changing their scripts, then
optionally migrate to XML/YAML independently.

All JLio behaviour that is intentionally changed or dropped MUST be documented in
`specs/002-migration-from-jlio/porting-guide.md` before the change is merged.

---

## Article IX — No Leaking Internals

Public APIs in `TLio.Core` must expose only `TNode`-parameterised types.
`JToken`, `XElement`, `YamlNode`, and their equivalents must never appear in any
`TLio.Core` or `TLio.Commands` namespace, even as generic constraints.

**Compliance check:**

```sh
grep -rn "JToken\|XElement\|YamlNode" \
  TLio.Core/Contracts/ TLio.Core/Models/ TLio.Commands/
```

→ Must return **zero results**.

---

## Article X — Logging is Observability

Every command execution logs at least one `Info` entry on success. All warnings and
errors are surfaced through `IExecutionLogger` — never through exceptions for expected
conditions (missing path, wrong type, etc.).

**Rules:**

- Every `Execute()` method MUST call `context.LogInfo(...)` on the success path.
- Graceful skips (node not found, type mismatch) MUST call `context.LogWarning(...)`.
- Unexpected failures that abort execution MUST call `context.LogError(...)`.
- Throwing exceptions for expected domain conditions (e.g., "path not found") is a
  constitution violation.

---

## Governance

### Amendment Procedure

1. Open a new spec in `specs/<NNN>-constitution-amendment/` describing the proposed
   change, rationale, and impact on existing features.
2. Update `specs/constitution.md` (the canonical source) first.
3. Propagate changes to `.specify/memory/constitution.md` and all affected templates
   using the `/speckit.constitution` command.
4. Increment the version number and update the `Last Amended` date in both files.
5. All in-flight `tasks.md` files MUST be reviewed for conflicts before the amendment
   is merged.

### Versioning Policy

Semantic versioning (`MAJOR.MINOR.PATCH`):

- **MAJOR** — backward-incompatible principle removal or redefinition (e.g., removing
  an article, changing "MUST" to "SHOULD", redefining a term).
- **MINOR** — new article added, or materially expanded guidance that introduces new
  obligations on existing code.
- **PATCH** — clarifications, wording improvements, added examples, typo fixes with
  no new obligations.

### Compliance Review

- Every PR that touches `TLio.Core/`, `TLio.Commands/`, or `TLio.Functions/` MUST
  include a constitutional compliance check (checklist in
  `.specify/templates/speckit.implement.md`).
- The compliance grep commands in each article MUST return zero results before a
  PR is merged.
- Violations found after merge are treated as bugs with the same priority as
  functional regressions.

### Canonical Sources

| Artifact | Path |
|---|---|
| Canonical constitution | `specs/constitution.md` |
| AI-readable memory constitution | `.specify/memory/constitution.md` |
| Runtime implementation guardrail | `.specify/templates/speckit.implement.md` |
| Planning guardrail | `.specify/templates/speckit.plan.md` |
| Task generation guardrail | `.specify/templates/speckit.tasks.md` |

---

**Version**: 1.0.0 | **Ratified**: 2026-03-24 | **Last Amended**: 2026-03-26
