# Data Model — 002 Migration from JLio to TLio

**Feature**: Migration from JLio to TLio
**Branch**: `002-migration-from-jlio`
**Date**: 2026-03-24

---

## Core Execution Pipeline

```
TNode (data document)
    │
    ▼
ScriptEngine<TNode>
    │  parses script JSON
    ▼
TLioScript<TNode>
    │  contains ordered list of
    ▼
ICommand<TNode>[]
    │  each receives
    ▼
IExecutionContext<TNode>
    ├── IItemsFetcher<TNode>   (path-based node selection)
    ├── INodeAdapter<TNode>    (format-specific node ops)
    └── IExecutionLogger       (structured log)
    │
    ▼
TLioExecutionResult<TNode>
    ├── Success: bool
    ├── Data: TNode            (mutated in-place)
    └── Logging: LogEntries
```

---

## Entity Catalogue

### TLio.Core — Contracts

| Interface | Responsibility | Replaces (JLio) |
|---|---|---|
| `ICommand<TNode>` | Single script step — Find/Compute/Operate | `ICommand` |
| `IFunction<TNode>` | Value-producing expression | `IFunction` |
| `IFunctionSupportedValue<TNode>` | Value that may be a literal or function call | `IFunctionSupportedValue` |
| `IExecutionContext<TNode>` | Runtime environment injected into every command | `IExecutionContext` |
| `INodeAdapter<TNode>` | Format-specific node manipulation | `JToken` direct calls |
| `IItemsFetcher<TNode>` | Path-based node selection | `IItemsFetcher` |
| `ICommandsProvider<TNode>` | Registry of command factories for script parser | `ICommandsProvider` |
| `IFunctionsProvider<TNode>` | Registry of function factories for script parser | `IFunctionsProvider` |
| `IScriptParser<TNode>` | Deserializes script JSON into `TLioScript<TNode>` | `JLioEngine.Parse()` |

---

### TLio.Core — Models

| Type | Fields / Members | Notes |
|---|---|---|
| `CommandBase<TNode>` | `Path: string`, `Property: string?` | Abstract base; `Execute()` calls `Validate()` then `ExecuteOnPath()` |
| `FunctionBase<TNode>` | `Execute(node, data, context) → FunctionResult<TNode>` | Abstract base |
| `FixedValue<TNode>` | `Value: TNode` | Holds pre-parsed node; Newtonsoft variant may hold `JToken` directly |
| `FunctionSupportedValue<TNode>` | `WrappedFunction: IFunction<TNode>` | Delegates `GetValue()` to the inner function |
| `FunctionResult<TNode>` | `Success: bool`, `Result: TNode?`, `Message: string?` | |
| `TLioScript<TNode>` | `Commands: IList<ICommand<TNode>>` | Immutable after parsing; `Execute()` runs commands sequentially |
| `TLioExecutionResult<TNode>` | `Success: bool`, `Data: TNode`, `Logging: LogEntries` | Aggregates per-command results |
| `Arguments<TNode>` | `Items: IList<IFunctionSupportedValue<TNode>>` | Parsed argument list for functions |
| `SelectedNodes<TNode>` | `Nodes: IList<TNode>`, `Paths: IList<string>` | Output of `IItemsFetcher.SelectNodes()` |
| `ExecutionContext<TNode>` | `ItemsFetcher`, `NodeAdapter`, `Logger` | Concrete default implementation |
| `ExecutionLogger` | `Entries: List<LogEntry>` | Thread-unsafe; one per execution |
| `LogEntry` | `Level`, `Group`, `Message`, `Timestamp` | |

---

### TLio.Commands — Command Implementations

| Command | Key Properties | Behavior |
|---|---|---|
| `Add<TNode>` | `Path`, `Value: IFunctionSupportedValue<TNode>` | Appends to array or adds property if absent |
| `Set<TNode>` | `Path`, `Property?`, `Value` | Sets value at path (creates if absent) |
| `Put<TNode>` | `Path`, `Value` | Sets value only if path exists |
| `Remove<TNode>` | `Path` | Removes node from parent |
| `Copy<TNode>` | `FromPath`, `ToPath`, `DestinationAsArray: bool` | Copies node(s); optionally forces array dest |
| `Move<TNode>` | `FromPath`, `ToPath`, `DestinationAsArray: bool` | Copy + Remove source |
| `IfElse<TNode>` | `Condition: IFunctionSupportedValue<TNode>`, `IfScript`, `ElseScript` | Conditional script execution |
| `Compare<TNode>` | `FirstPath`, `SecondPath`, `ResultPath` | Writes comparison result node |
| `Merge<TNode>` | `Path`, `TargetPath`, `MergeArraysBy?` | Deep-merges source into target |
| `DecisionTable<TNode>` | `Path`, `DecisionTableConfig` | Rule-based value routing |

**Internal base classes** (in `TLio.Commands.Logic`):

| Type | Purpose |
|---|---|
| `PropertyChangeCommand<TNode>` | Shared `Path`/`Property` logic for Add/Set/Put |
| `CopyMoveBase<TNode>` | Shared logic for Copy/Move including array-alignment |

---

### TLio.Functions — Function Implementations

#### Core functions (TLio.Functions)

| Function | Signature | Description |
|---|---|---|
| `Fetch<TNode>` | `=fetch($.path)` | Retrieves node at path |
| `Indirect<TNode>` | `=indirect($.pathToPath)` | Resolves path stored at another path |
| `Promote<TNode>` | `=promote($.path)` | Promotes array child to parent scope |
| `Partial<TNode>` | `=partial($.path, n)` | Returns nth element |
| `ScriptPath<TNode>` | `=scriptpath()` | Returns the current script path expression |
| `Datetime<TNode>` | `=datetime(format?)` | Returns current date/time |

#### Extension functions (future projects)

| Pack | Count | Examples |
|---|---|---|
| `TLio.Extensions.Math` | 25+ | `=sum(...)`, `=avg(...)`, `=round(...)` |
| `TLio.Extensions.Text` | 25+ | `=concat(...)`, `=substring(...)`, `=trim(...)` |
| `TLio.Extensions.TimeDate` | 5 | `=addDays(...)`, `=formatDate(...)` |
| `TLio.Extensions.ETL` | 4 commands | `Flatten`, `Restore`, `Resolve`, `ToCsv` |

---

### TLio.Client — Script Engine & Registration

| Type | Responsibility |
|---|---|
| `ScriptEngine<TNode>` | Parses script JSON + executes `TLioScript<TNode>` |
| `CommandsProvider<TNode>` | Default `ICommandsProvider<TNode>` — maps command name strings to factories |
| `FunctionsProvider<TNode>` | Default `IFunctionsProvider<TNode>` — maps function name strings to factories |
| `ParseOptions<TNode>` | Convenience fluent builder: `CreateDefault()`, `RegisterCommand<C>()`, `RegisterFunction<F>()` |
| `FunctionConverter<TNode>` | JSON converter — parses `=functionName(args)` strings into `IFunction<TNode>` |
| `CommandConverter<TNode>` | JSON converter — discriminator-based command deserialization using `"command"` field |

---

### TLio.Json — Newtonsoft Adapter

| Type | Implements | Key notes |
|---|---|---|
| `JsonNodeAdapter` | `INodeAdapter<JToken>` | All `JToken` manipulation; `GetParentNode` skips `JProperty` wrappers |
| `JsonPathItemsFetcher` | `IItemsFetcher<JToken>` | Uses `JToken.SelectTokens()` (Jayway JsonPath) |
| `JsonExecutionContext` | — | Factory: `CreateDefault()` wires `JsonNodeAdapter` + `JsonPathItemsFetcher` |
| `JsonSplittedPath` | — | Internal: splits `$.a.b.c` into `($.a.b, c)` |

---

### TLio.Json.SystemText — System.Text.Json Adapter

| Type | Implements | Key notes |
|---|---|---|
| `SystemTextJsonNodeAdapter` | `INodeAdapter<JsonNode>` | `JsonNode` manipulation |
| `SystemTextJsonPathItemsFetcher` | `IItemsFetcher<JsonNode>` | Uses `JsonPath.Net`; workarounds for RFC 9535 vs Jayway diffs |
| `SystemTextJsonExecutionContext` | — | Factory: `CreateDefault()` wires the above |
| `IJsonPathProvider<JsonNode>` | — | Abstraction for JsonPath evaluation; default backed by `JsonPath.Net` |

---

## State Transitions

### Command execution lifecycle

```
Parsed → Validated → Executing → Success
                  ↓           ↓
             ValidationError  RuntimeError (logged, not thrown)
```

All errors are surfaced via `IExecutionLogger` (warning or error level), never as exceptions for expected conditions (missing path, wrong type, unmatched path expression).

---

## Validation Rules

| Rule | Scope |
|---|---|
| `Path` must be non-null and non-empty | All `PropertyChangeCommand<TNode>` subclasses |
| `FromPath` and `ToPath` must both be provided | `Copy<TNode>`, `Move<TNode>` |
| Function argument count must match declared arity | `FunctionBase<TNode>.Validate()` |
| Unknown command name → `NotFoundCommand` warning | `CommandConverter<TNode>` |
| Unknown function name → warning + null result | `FunctionConverter<TNode>` |
