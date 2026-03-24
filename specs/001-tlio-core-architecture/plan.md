# Plan 001 — TLio Core Architecture

**Spec:** [spec.md](./spec.md)
**Status:** Draft

---

## Phase -1: Constitutional Gates

### Simplicity Gate (Article VII)
The solution uses 8 projects:

| Project | Purpose |
|---|---|
| `TLio.Core` | Contracts + models — zero external deps |
| `TLio.Commands` | Built-in commands — depends only on Core |
| `TLio.Functions` | Built-in functions — depends only on Core |
| `TLio.Json` | JSON adapter (Newtonsoft.Json) |
| `TLio.Xml` | XML adapter (System.Xml.Linq, built-in) |
| `TLio.Yaml` | YAML adapter (YamlDotNet) |
| `TLio.Client` | ScriptEngine + provider registries |
| `TLio.UnitTests` | NUnit tests |

**Justification for 8 projects:** Each format adapter must be independently consumable
(a consumer using only XML should not pull in a JSON dependency). This cannot be achieved
with fewer projects without violating Article I.

### Anti-Abstraction Gate (Article VIII from constitution, Article IV from spec)
The abstraction introduced (`INodeAdapter<TNode>`, `IItemsFetcher<TNode>`) maps
1-to-1 with the capabilities needed by commands. No wrapper layers or adapter
hierarchies are introduced.

### Integration-First Gate
The first tests (ArchitectureTests.cs) operate against real `JToken` nodes through the
real `JsonNodeAdapter` — no mocks for the happy path.

---

## Architecture Overview

### The Three Layers

```
┌─────────────────────────────────────────────────────────────┐
│  TLio.Client  (ScriptEngine<TNode>, providers)              │
├─────────────────────────────────────────────────────────────┤
│  TLio.Commands / TLio.Functions  (business logic only)      │
├─────────────────────────────────────────────────────────────┤
│  TLio.Core  (contracts + models, zero format deps)          │
└─────────────────────────────────────────────────────────────┘
         ↕ injected via IExecutionContext<TNode>
┌──────────────┬──────────────┬──────────────────────────────┐
│  TLio.Json   │  TLio.Xml    │  TLio.Yaml  (adapters)       │
└──────────────┴──────────────┴──────────────────────────────┘
```

### Key Interfaces and their relationships

```
IExecutionContext<TNode>
  ├── IItemsFetcher<TNode>   ← swappable path language
  ├── INodeAdapter<TNode>    ← swappable node operations
  └── IExecutionLogger

ICommand<TNode>
  └── Execute(TNode, IExecutionContext<TNode>) → TLioExecutionResult<TNode>

IFunction<TNode>
  └── Execute(TNode, TNode, IExecutionContext<TNode>) → FunctionResult<TNode>

IFunctionSupportedValue<TNode>
  └── GetValue(TNode, TNode, IExecutionContext<TNode>) → FunctionResult<TNode>
```

### Data Flow

```
Script text
    ↓  parse (ScriptEngine)
TLioScript<TNode>  (List<ICommand<TNode>>)
    ↓  foreach command
ICommand<TNode>.Execute(dataRoot, context)
    ↓  select targets
IItemsFetcher<TNode>.SelectNodes(path, dataRoot)
    ↓  mutate each target
INodeAdapter<TNode>.SetProperty / Replace / AppendToArray / …
    ↓
TLioExecutionResult<TNode>  (mutated dataRoot + success flag)
```

---

## Technology Choices

| Concern | Choice | Rationale |
|---|---|---|
| Target framework | net10.0 | Latest LTS, modern C# language features |
| JSON node model | `Newtonsoft.Json.Linq.JToken` | Backward compat with JLio; rich JsonPath support |
| XML node model | `System.Xml.Linq.XElement` | In-box, no extra dependency |
| YAML node model | `YamlDotNet.RepresentationModel.YamlNode` | De-facto standard for .NET YAML |
| Test framework | NUnit 4 | Consistent with JLio |
| DI | Manual injection via `ExecutionContext<TNode>` | No DI container dependency in Core |

---

## Phase-by-Phase Breakdown

### Phase 1 — Core contracts (DONE in base form)
**Deliverables:**
- All interfaces in `TLio.Core/Contracts/`
- All models in `TLio.Core/Models/`
- `TLio.Core` compiles with zero warnings

### Phase 2 — JSON adapter
**Deliverables:**
- `JsonNodeAdapter` fully implemented (all INodeAdapter members)
- `JsonPathItemsFetcher` fully implemented (including relative-path and parent-navigation)
- `JsonExecutionContext.CreateDefault()` factory
- Unit tests: all ArchitectureTests pass; JsonAdapterTests cover all node types

### Phase 3 — Commands implementation
**Deliverables:**
- `Set<TNode>`, `Add<TNode>`, `Remove<TNode>`, `Copy<TNode>`, `Move<TNode>`, `Put<TNode>` fully implemented
- Each command delegates all node operations to `INodeAdapter` and `IItemsFetcher`
- Unit tests pass for all commands against JSON data
- Backward-compatibility: JLio Set/Add/Remove/Copy/Move test cases produce identical results

### Phase 4 — Functions implementation
**Deliverables:**
- `FixedValue<TNode>` (done in base form)
- `PathValue<TNode>` — evaluates a path expression as a function argument
- Basic functions: `concat`, `toUpper`, `toLower`, `now`, `typeOf`
- Unit tests for each function

### Phase 5 — Script parser (Client)
**Deliverables:**
- `ScriptEngine<TNode>.Execute(string, TNode, IExecutionContext)` parses and executes a JSON-format script
- Script format is backward-compatible with JLio's JSON script structure
- Unit tests: round-trip parse → execute → serialise for Set, Add, Remove

### Phase 6 — XML adapter
**Deliverables:**
- `XmlNodeAdapter` fully implemented
- `XPathItemsFetcher` fully implemented
- `XmlExecutionContext.CreateDefault()` factory
- Integration test: run a Set + Add + Remove script against an XElement document

### Phase 7 — YAML adapter
**Deliverables:**
- `YamlNodeAdapter` fully implemented
- `YamlPathItemsFetcher` (dot-notation) fully implemented
- `YamlExecutionContext.CreateDefault()` factory
- Integration test: run a Set + Add + Remove script against a YamlMappingNode

---

## Complexity Tracking

No justified exceptions to Articles I–X at this time.
All complexity is necessary to satisfy the format-neutrality requirement.
