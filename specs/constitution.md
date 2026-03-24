# TLio Constitution
> Immutable architectural principles that govern every implementation decision in TLio.
> All specifications, plans, and generated code must comply with these articles.

---

## Article I — Format Neutrality

TLio is a **data-format-agnostic scripting framework**. No project in the solution
(except dedicated adapter assemblies) may reference any concrete data-format library
(Newtonsoft.Json, System.Xml, YamlDotNet, etc.) directly. All format-specific
operations are expressed exclusively through `INodeAdapter<TNode>` and
`IItemsFetcher<TNode>`.

*Adapter assemblies* (`TLio.Json`, `TLio.Xml`, `TLio.Yaml`, …) are the only permitted
exceptions to this rule.

---

## Article II — Dependency Inversion

All cross-cutting behaviour is delivered through interfaces, not concrete classes.
No command, function, or engine component may `new` up an adapter or fetcher.
Everything is injected through `IExecutionContext<TNode>`.

---

## Article III — Generic-First Design

The generic type parameter `TNode` travels all the way from `ICommand<TNode>` through
to `TLioScript<TNode>` and `ScriptEngine<TNode>`. No boxing to `object`, no casting, no
reflection-based dispatch is permitted in Core or Commands.

---

## Article IV — Separation of Process and Execution

A **command** is a pure orchestrator. It defines the *process*:

1. **Find** — select target nodes via `context.ItemsFetcher.SelectNodes(path, data)`
2. **Compute** — resolve the value via `value.GetValue(node, data, context)`
3. **Operate** — mutate nodes via `context.NodeAdapter.<Operation>(node, ...)`

A **function** follows the same pattern to produce a value:

1. **Find** argument values via `context.ItemsFetcher.SelectNodes(...)` or evaluate sub-functions
2. **Compute** the result
3. **Produce** the output node via `context.NodeAdapter.Create*(...)` or `context.NodeAdapter.DeepClone(...)`

**Hard rule:** No method body in `TLio.Commands` or `TLio.Functions` may call any
method directly on a `TNode` variable. Every interaction with a node — including
reading its value, checking its type, or modifying it — must go through
`context.NodeAdapter` or `context.ItemsFetcher`. A variable of type `TNode` may
only be passed as an argument; it must never be the receiver of a method call.

Commands must not contain any format-specific code, branching on `TNode`, or conditional
logic based on the type of `TNode`.

---

## Article V — Swappable Selection

The path-selection mechanism (`IItemsFetcher<TNode>`) is a first-class extension point.
JsonPath, XPath, dot-notation, or any other path language may be plugged in without
changing any command or function code. The fetcher is always resolved from the context,
never hard-coded.

---

## Article VI — Test-First Imperative

No implementation code is written before:
1. A specification exists in `specs/<feature>/spec.md`
2. The corresponding `plan.md` and `tasks.md` are in place
3. At least a failing unit test exists for the feature

---

## Article VII — Simplicity Gate

Before adding a new project or layer, the following question must be answered in the
relevant `plan.md`:

> "Could this be implemented with fewer projects and still satisfy Article I–V?"

If yes, the simpler option is preferred.

---

## Article VIII — Backward Migration Path

TLio provides a JSON adapter (`TLio.Json`) that is **behaviourally equivalent** to
JLio's existing commands and functions. This enables incremental migration: consumers
can swap the JLio scripting engine for TLio without changing their scripts, then
optionally migrate to XML/YAML independently.

---

## Article IX — No Leaking Internals

Public APIs in `TLio.Core` must expose only `TNode`-parameterised types.
`JToken`, `XElement`, `YamlNode`, and their equivalents must never appear in any
`TLio.Core` or `TLio.Commands` namespace, even as generic constraints.

---

## Article X — Logging is Observability

Every command execution logs at least one `Info` entry on success. All warnings and
errors are surfaced through `IExecutionLogger` — never through exceptions for expected
conditions (missing path, wrong type, etc.).
