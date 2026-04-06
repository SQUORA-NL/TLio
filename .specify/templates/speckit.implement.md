# /speckit.implement — Implement tasks from a TLio tasks.md

## Purpose
Execute one or more pending tasks from `specs/<NNN>-<slug>/tasks.md`, writing
production code and tests that satisfy the task's acceptance criterion.

## Instructions for AI

1. **Read** `specs/constitution.md`, `spec.md`, `plan.md`, and `tasks.md` before writing.
2. **Pick the next unchecked task** (or the task explicitly requested).
3. **Write the test first** — confirm the test fails (Red) before writing implementation.
4. **Implement** to make the test pass (Green).
5. **Mark the task `[x]`** in `tasks.md` after the test passes.
6. **Constitutional compliance check** before marking done (see checklist below).

---

## ⚠ Article IV — The Single Most Important Rule

> **A variable of type `TNode` is opaque. You may only pass it to an interface method.
> You may never call a method on it directly.**

Correct — command orchestrates through adapters:
```csharp
// ✅ CORRECT
var nodes = context.ItemsFetcher.SelectNodes(path, data);
context.NodeAdapter.SetProperty(targetNode, propertyName, value);
bool isObj = context.NodeAdapter.IsObject(targetNode);
```

Wrong — command reaches into the node directly:
```csharp
// ❌ WRONG — JToken-specific, violates Article IV
if (targetNode is JObject obj) obj[propertyName] = value;
targetNode.ToString();
((JValue)targetNode).Value = 42;
```

The same rule applies to functions. Every node read, type-check, and mutation goes
through `context.NodeAdapter` or `context.ItemsFetcher`. The command/function is
purely an orchestrator — it never knows what `TNode` actually is.

---

## Constitutional compliance checklist (run before marking any task done)

```
grep -rn "JToken\|JObject\|JArray\|JValue\|XElement\|YamlNode" TLio.Core/ TLio.Commands/ TLio.Functions/
```
→ Must return **zero results** (comments mentioning type names as examples are acceptable).

- [ ] No `TNode` variable is the receiver of a method call (only passed as argument)
- [ ] No format-specific types in `using` statements in Core/Commands/Functions
- [ ] No `new ConcreteAdapter()` in command or function code
- [ ] Every `Execute` method calls `ResetSuccess()` first
- [ ] Every `Execute` method logs at least one entry

---

## Code style rules (TLio-specific)

- Use `required` init properties on `ExecutionContext<TNode>` subclasses.
- Commands must call `ResetSuccess()` at the start of `Execute`.
- Log at least one `LogInfo` entry on successful execution.
- Log warnings (not errors) for gracefully skipped operations (e.g. "node not found").
- Log errors only for unexpected conditions that cause execution to fail.
- Validation messages must be human-readable: include the command name and the
  problematic property.

## File locations

| What | Where |
|---|---|
| Core contracts | `TLio.Core/Contracts/` |
| Core models | `TLio.Core/Models/` |
| Commands | `TLio.Commands/<CommandName>.cs` |
| Functions | `TLio.Functions/<FunctionName>.cs` |
| JSON adapter | `TLio.Json/` |
| XML adapter | `TLio.Xml/` |
| YAML adapter | `TLio.Yaml/` |
| Tests — core/commands/engine | `TLio.UnitTests/<Category>Tests/` |
| Tests — JSON adapter (Newtonsoft) | `TLio.Json.Tests/` |
| Tests — JSON adapter (System.Text) | `TLio.Json.SystemText.Tests/` |
| Tests — functions | `TLio.Functions.Tests/` |
| Tests — XML adapter | `TLio.Xml.Tests/` |
| Tests — YAML adapter | `TLio.Yaml.Tests/` |
