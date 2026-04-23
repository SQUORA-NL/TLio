# Research: Deepen Test Coverage

## Decision: Logging Assertion Pattern

**Decision**: Use `context.GetLogEntries().Any(e => e.Level == LogLevel.X && e.Message.Contains("..."))` — this is the established pattern in `AddTests.cs`, `CopyMoveBaseTests.cs`, `CopyMoveTests.cs`, `DecisionTableTests.cs`, etc.

**Rationale**: `IExecutionContext<TNode>.GetLogEntries()` returns `LogEntries` (a list of `LogEntry` with `.Level` and `.Message`). No mock infrastructure is needed — the built-in `ExecutionLogger` accumulates entries in-memory. Every test just calls `JsonExecutionContext.CreateDefault()` and inspects entries after execution.

**Alternatives considered**: Microsoft.Extensions.Logging mock frameworks — rejected because the project already has a self-contained `IExecutionLogger` that records to memory.

---

## Decision: Function Result Access Pattern

**Decision**: Use `result.Data[0].ToObject<T>()` or `result.Data.First!.Value<T>()` to access function results.

**Rationale**: `FunctionResult<TNode>.Data` is `SelectedNodes<TNode>` (a `List<TNode>`), not `TNode` directly. Calling `.Value<T>()` on the list works via extension method when indexing, but `result.Data.First!.Value<bool>()` is the established pattern seen in `PredicateTests.cs`. For non-bool types, `result.Data[0].ToObject<T>()` is correct per `TimeDate_ExtendedTests.cs`.

**Alternatives considered**: Direct `result.Data!.Value<T>()` — incorrect, treats the list as JToken and fails at runtime.

---

## Decision: Text Functions to Cover (22 unique implementations)

**Decision**: The 22 unique function *implementations* (ignoring camelCase aliases) are: concat, length, substring, toupper, tolower, trim, trimstart, trimend, startswith, endswith, contains, replace, split, join, indexof, format, parse, padleft, padright, newguid, isempty, toString.

**Rationale**: `RegisterTextPack` registers 26 names but only 22 unique implementations (4 are camelCase aliases for toupper/tolower/trimstart/trimend). Tests target the implementation classes.

**Alternatives considered**: Testing aliases separately — unnecessary since they resolve to the same class.

---

## Decision: ToCsv Test Location

**Decision**: Add dedicated `ToCsvTests.cs` to `TLio.Functions.Tests/FunctionsTests/ETLTests/` alongside `ETL_Tests.cs`, covering additional scenarios not already present (null fields, empty array, custom delimiter, missing-field rows).

**Rationale**: `ToCsv<TNode>` is in `TLio.Extensions.ETL`. Some tests already exist in `ETL_Tests.cs` and `FlattenRestoreTests.cs` but edge cases are uncovered. A focused file with fixture-style inline tests (inline `[TestCase]` is acceptable per Article VI for unit-level edge cases) keeps tests organized.

**Alternatives considered**: Fixture triplet files — viable for full-script tests but ToCsv edge cases are unit-level (direct command instantiation), so inline tests are appropriate per Article VI.

---

## Decision: Test Projects for Each Coverage Area

| Coverage Area | Test Project | Rationale |
|---|---|---|
| Text functions (22) | `TLio.Functions.Tests` | Extension pack tests go here |
| Logging assertions | `TLio.Functions.Tests` (functions), `TLio.UnitTests` (commands) | Each where the behavior lives |
| ToCsv command | `TLio.Functions.Tests/ETLTests/` | ETL extension pack tests |
| Math edge cases | `TLio.Functions.Tests/MathTests/` | Already has math test folder |
| Built-in error paths | `TLio.Functions.Tests/FunctionsTests/` | Built-in functions live here |

---

## Decision: No New Infrastructure Required

**Decision**: No new test helpers, mocks, base classes, or NuGet packages needed.

**Rationale**: All patterns (context creation, result inspection, log capture) are already established. Tests use `JsonExecutionContext.CreateDefault()`, `FunctionResult<TNode>`, and `context.GetLogEntries()` — the same API surface used across the test suite.
