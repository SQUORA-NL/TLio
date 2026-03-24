# Plan 002 — Migration from JLio to TLio

**Branch**: `002-migration-from-jlio` | **Date**: 2026-03-24 | **Spec**: [spec.md](./spec.md)
**Status:** Draft

---

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: Newtonsoft.Json, System.Text.Json, JsonPath.Net (json-everything), xUnit
**Storage**: N/A
**Testing**: xUnit Theory (data-driven fixture triplets)
**Target Platform**: .NET 10 class library (multi-project solution)
**Project Type**: library
**Performance Goals**: Match JLio behavior identically — no measurable regression
**Constraints**: Zero format-specific code in TLio.Core or TLio.Commands; all adapter code confined to TLio.Json / TLio.Json.SystemText
**Scale/Scope**: 10 commands, 60+ functions, 2 JSON adapters, 73+ ported test files

---

## Constitution Check

*Re-evaluated 2026-03-24 — PASS on all articles.*

| Article | Status | Notes |
|---------|--------|-------|
| I — Format Neutrality | ✓ | All format-specific ops via `INodeAdapter<TNode>` / `IItemsFetcher<TNode>` |
| II — Dependency Inversion | ✓ | Everything injected through `IExecutionContext<TNode>` |
| III — Generic-First | ✓ | `TNode` parameter propagates through all Core/Commands types |
| IV — Separation of Process/Execution | ✓ | Commands follow Find → Compute → Operate pattern exclusively |
| V — Swappable Selection | ✓ | `IItemsFetcher<TNode>` + `IJsonPathProvider<TNode>` both pluggable |
| VI — Test-First | ✓ | spec.md + plan.md + tasks.md in place; fixture triplets required before code |
| VII — Simplicity Gate | ✓ | 7 projects justified (see Complexity Tracking) |
| VIII — Backward Migration Path | ✓ | `TLio.Json` is behaviourally equivalent to JLio |
| IX — No Leaking Internals | ✓ | `JToken` never appears in Core or Commands namespace |
| X — Logging is Observability | ✓ | All warnings/errors via `IExecutionLogger`; never via exceptions for expected conditions |

---

## Phase -1: Constitutional Gates

### Simplicity Gate
Adding two projects (`TLio.Json` already existed; `TLio.Json.SystemText` is new).
No additional layers required. Commands and functions live in the same projects as
the base scaffold — no new abstractions beyond what Article I–V already mandate.

### Anti-Abstraction Gate
All format-specific code is confined to the adapter projects. Neither `TLio.Commands`
nor `TLio.Functions` imports any JSON library. The migration of JLio's
`PropertyChangeCommand` and `CopyMove` base classes uses only `INodeAdapter<TNode>`
operations.

### Integration-First Gate
Happy-path tests use real `JToken` nodes via `JsonExecutionContext.CreateDefault()`.
No mocks are introduced for the Newtonsoft adapter.

---

## Architecture Overview — The Migration Mapping

### How JLio components map to TLio components

```
JLio                              TLio
──────────────────────────────────────────────────────────────────
ICommand                     →    ICommand<TNode>
JToken dataContext           →    TNode dataContext
IExecutionContext             →    IExecutionContext<TNode>
IItemsFetcher                →    IItemsFetcher<TNode>
  .SelectTokens()            →      .SelectNodes()
  .NavigateToParent()        →      .GetParent()  +  INodeAdapter.GetParentNode()
  .ResolveRelativePath()     →      .ResolveRelativePath()
  .ProcessIndirectPath()     →      .ProcessIndirectPath()
  (JsonMethods.CheckOrCreate)→      .EnsurePath()
  .GetIntellisense()         →      .GetIntellisense()
  .SplitPath()               →      .SplitParentAndLeaf()

JToken.Type checks           →    INodeAdapter.IsObject/IsArray/IsPrimitive/IsNull
JToken property access       →    INodeAdapter.GetProperty/SetProperty/RemoveProperty
JToken array access          →    INodeAdapter.AppendToArray/GetArrayElement/...
JToken.Replace()             →    INodeAdapter.Replace()
JsonMethods.RemoveItem()     →    INodeAdapter.RemoveFromParent()
JToken deep merge            →    INodeAdapter.DeepMergeInto()
JToken deep clone            →    INodeAdapter.DeepClone()
JToken.DeepEquals()          →    INodeAdapter.DeepEquals()
JToken value coercion        →    INodeAdapter.TryGetBoolean/TryGetDouble/TryGetString()

PropertyChangeCommand        →    PropertyChangeCommand<TNode>  (TLio.Commands.Logic)
CopyMove base class          →    CopyMoveBase<TNode>            (TLio.Commands.Logic)
JLioScript                   →    TLioScript<TNode>
JLioExecutionResult          →    TLioExecutionResult<TNode>
JLioFunctionResult           →    FunctionResult<TNode>
SelectedTokens               →    SelectedNodes<TNode>
FunctionSupportedValue       →    FunctionSupportedValue<TNode>  (to be created)
FixedValue                   →    FixedValue<TNode>
Arguments                    →    Arguments<TNode>

FunctionConverter            →    FunctionConverter<TNode>       (TLio.Client)
CommandConverter             →    CommandConverter<TNode>         (TLio.Client)
ParseOptions                 →    ParseOptions<TNode>             (TLio.Client)
JLioEngine                   →    ScriptEngine<TNode>
```

---

## Key Design Decisions

### Decision 1 — FixedValue and nested function expressions

**Problem:** JLio's `FixedValue` processes nested `=functionName()` strings embedded
in JSON objects and arrays (e.g. `{ "result": "=sum($.values)" }`). This traversal uses
JToken directly.

**TLio solution:** `FixedValue<TNode>` holds a pre-computed `TNode`. The script parser
is responsible for converting the raw JSON from the script into a `TNode` using
`IScriptNodeConverter<TNode>`. The converter traverses the JSON and expands any
`=functionName()` string values into `IFunction<TNode>` instances, wrapping the result
in a `FunctionSupportedValue<TNode>`.

For the Newtonsoft adapter the script value is already a `JToken`, so no conversion is
needed and `FixedValue<JToken>` directly holds the `JToken` from the script.

---

### Decision 2 — Script is always JSON

TLio scripts remain in JSON format regardless of the data format being processed.
This ensures full backward compatibility with JLio scripts and is the simplest path.
XML/YAML scripts could be a future extension (separate spec).

---

### Decision 3 — Two JSON adapters, one behavior

Both `TLio.Json` (Newtonsoft) and `TLio.Json.SystemText` must produce identical
transformation results. The contract is enforced by running the same test cases
against both execution contexts via a shared test base class
`JsonAdapterComplianceTestBase<TNode>`.

**JsonPath compatibility note:** Newtonsoft's `SelectTokens` is based on Jayway JsonPath.
`JsonCons.JsonPath` implements RFC 9535. The two libraries differ in edge cases
(recursive descent on leaf nodes, filter expressions). Where differences exist, the
Newtonsoft behaviour is the authoritative spec. Any `JsonCons` deviation must be
documented and, if observable in tests, worked around in `SystemTextJsonPathItemsFetcher`.

---

### Decision 4 — `FunctionSupportedValue<TNode>` wrapper

JLio has `FunctionSupportedValue` (a class that wraps `IFunction`) and
`IFunctionSupportedValue` (interface). TLio needs the same pattern:
`FunctionSupportedValue<TNode>` wraps any `IFunction<TNode>` and handles logging.

---

### Decision 5 — `ParseOptions<TNode>` and command/function registration

TLio's `ScriptEngine<TNode>` uses `ICommandsProvider<TNode>` and
`IFunctionsProvider<TNode>` for registration. A `ParseOptions<TNode>` convenience
class (parallel to JLio's `ParseOptions`) will register all built-in commands and
functions and expose fluent `RegisterCommand<C>()` / `RegisterFunction<F>()` methods.

---

## Phase-by-Phase Breakdown

### Phase 2A — Complete Newtonsoft JsonNodeAdapter

All `INodeAdapter<JToken>` members fully implemented including:
- `RemoveFromParent` (port `JsonMethods.RemoveItemFromTarget`)
- `DeepMergeInto` (port JLio's merge logic from `CopyMove` + `Merge` command)
- `GetParentNode`, `GetParentPropertyName`
- `TryGetBoolean`, `TryGetDouble`, `TryGetString`
- `DeepEquals`

---

### Phase 2B — Complete Newtonsoft JsonPathItemsFetcher

All `IItemsFetcher<JToken>` members fully implemented including:
- `GetParent` with semantic-parent logic (port from JLio's `NavigateToParent`)
- `ResolveRelativePath` with `@` and `<--` support (port from JLio)
- `EnsurePath` (port `JsonMethods.CheckOrCreateParentPath`)
- `SplitParentAndLeaf` (port `JsonSplittedPath` / `JsonPathMethods.SplitPath`)
- `ProcessIndirectPath` (port from JLio's regex-based implementation)
- `GetIntellisense` (port `GetIntellisense` from JLio)

---

### Phase 3 — Commands implementation (Newtonsoft)

Implement all commands through `INodeAdapter<JToken>` + `IItemsFetcher<JToken>`:
- `PropertyChangeCommand<TNode>` — base class (scaffolded, needs full implementation)
- `CopyMoveBase<TNode>` — base class (scaffolded, needs array-alignment logic)
- `Remove<TNode>` — scaffolded, needs `RemoveFromParent`
- `IfElse<TNode>` — new, port from JLio
- `DecisionTable<TNode>` — new, port from JLio (complex — separate sub-tasks)
- `Compare<TNode>` — new, port from JLio
- `Merge<TNode>` — new, port from JLio

---

### Phase 4 — Functions implementation (Newtonsoft)

#### Phase 4A — Core functions (6)
Port: `Fetch`, `Indirect`, `Promote`, `Partial`, `ScriptPath`, `Datetime`

#### Phase 4B — `FunctionSupportedValue<TNode>` and `FixedValue<TNode>`
Port the value-evaluation pipeline including nested `=func()` expansion.

#### Phase 4C — Math extension functions (25+)
Port all functions from `JLio.Extensions.Math` to `TLio.Extensions.Math`.

#### Phase 4D — Text extension functions (25+)
Port all functions from `JLio.Extensions.Text` to `TLio.Extensions.Text`.

#### Phase 4E — TimeDate extension functions (5)
Port from `JLio.Extensions.TimeDate` to `TLio.Extensions.TimeDate`.

#### Phase 4F — ETL extension commands (4)
Port Flatten, Restore, Resolve, ToCsv from `JLio.Extensions.ETL`.

---

### Phase 5 — Script parser (Newtonsoft)

- `FunctionConverter<TNode>` — parse `=func(args)` strings into `IFunction<TNode>`
- `CommandConverter<TNode>` — JSON discriminator-based command deserialization
- `ParseOptions<TNode>` — registration + fluent API
- `ScriptEngine<TNode>.Execute(string)` — full implementation

---

### Phase 6 — Test porting (Newtonsoft)

- Create `TLio.UnitTests` equivalents for all 73 JLio test files
- Substitution table (see `porting-guide.md`):

| JLio | TLio |
|---|---|
| `ExecutionContext.CreateDefault()` | `JsonExecutionContext.CreateDefault()` |
| `JLioScript` | `TLioScript<JToken>` |
| `JLioExecutionResult` | `TLioExecutionResult<JToken>` |
| `JLioEngine` | `ScriptEngine<JToken>` |
| `ParseOptions.CreateDefault()` | `ParseOptions<JToken>.CreateDefault()` |
| `new Set("$.x", value)` | `new Set<JToken>("$.x", value)` |
| `FunctionConverter` | `FunctionConverter<JToken>` |

All assertions remain identical.

---

### Phase 7 — System.Text.Json adapter

Complete all stub implementations in `TLio.Json.SystemText`:
- `SystemTextJsonNodeAdapter` — `Replace`, `RemoveFromParent`, `DeepMergeInto`, `GetParentPropertyName`
- `SystemTextJsonPathItemsFetcher` — all members
- Run all ported tests against `SystemTextJsonExecutionContext.CreateDefault()` and fix any divergences

---

## Complexity Tracking

**Justified exception — `FixedValue<JToken>` for Newtonsoft:**
For the Newtonsoft adapter, `FixedValue<JToken>` may hold a raw `JToken` from the script
and process nested `=func()` strings using `FunctionConverter<JToken>`. This is
technically format-specific but is contained entirely within `TLio.Json` (not Core/Commands).
This mirrors JLio's design and is the simplest path to backward compatibility.

**JsonPath edge cases — System.Text.Json:**
`JsonCons.JsonPath` and Newtonsoft may differ on some edge cases. Documented deviations
are acceptable as long as no ported JLio test asserts on the differing case. Where they do
diverge in a tested path, `SystemTextJsonPathItemsFetcher` must implement a workaround.
