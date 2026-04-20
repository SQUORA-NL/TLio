# TLio Porting Guide — JLio → TLio (Newtonsoft)

This is the exact substitution table for porting JLio unit tests to TLio.
**No assertion may change when applying these substitutions.**

---

## Type substitutions

| JLio | TLio |
|---|---|
| `using JLio.Core.Models;` | `using TLio.Core.Models;` |
| `using JLio.Core.Contracts;` | `using TLio.Core.Contracts;` |
| `using JLio.Client;` | `using TLio.Client;` `using TLio.Json;` |
| `using JLio.Commands;` | `using TLio.Commands;` |
| `using JLio.Functions;` | `using TLio.Functions;` |
| `JLioScript` | `TLioScript<JToken>` |
| `JLioExecutionResult` | `TLioExecutionResult<JToken>` |
| `JLioFunctionResult` | `FunctionResult<JToken>` |
| `SelectedTokens` | `SelectedNodes<JToken>` |
| `ICommand` | `ICommand<JToken>` |
| `IFunction` | `IFunction<JToken>` |
| `IFunctionSupportedValue` | `IFunctionSupportedValue<JToken>` |
| `FunctionSupportedValue` | `FunctionSupportedValue<JToken>` |
| `FixedValue` | `FixedValue<JToken>` |
| `Arguments` | `Arguments<JToken>` |
| `CommandBase` | `CommandBase<JToken>` |
| `FunctionBase` | `FunctionBase<JToken>` |
| `ValidationResult` | `ValidationResult` *(unchanged)* |

---

## Execution context

| JLio | TLio |
|---|---|
| `ExecutionContext.CreateDefault()` | `JsonExecutionContext.CreateDefault()` |
| `IExecutionContext` | `IExecutionContext<JToken>` |
| `IItemsFetcher` | `IItemsFetcher<JToken>` |
| `new ExecutionContext { ItemsFetcher = ..., Logger = ... }` | `new ExecutionContext<JToken> { ItemsFetcher = ..., NodeAdapter = ..., Logger = ... }` |

---

## Script engine

| JLio | TLio |
|---|---|
| `JLioEngine` | `ScriptEngine<JToken>` |
| `ParseOptions.CreateDefault()` | `ParseOptions<JToken>.CreateDefault()` |
| `new JLioEngine(parseOptions, execContext)` | `new ScriptEngine<JToken>(parseOptions.CommandsProvider, parseOptions.FunctionsProvider)` |
| `engine.Parse(scriptText)` | `engine.Parse(scriptText, converter)` |
| `engine.ParseAndExecute(scriptText, data)` | `engine.Execute(scriptText, data, context)` |

---

## Commands

| JLio | TLio |
|---|---|
| `new Set("$.path", value)` | `new Set<JToken>("$.path", value)` |
| `new Set("$.path", "property", value)` | `new Set<JToken>("$.path", "property", value)` |
| `new Add("$.path", value)` | `new Add<JToken>("$.path", value)` |
| `new Put("$.path", value)` | `new Put<JToken>("$.path", value)` |
| `new Remove("$.path")` | `new Remove<JToken>("$.path")` |
| `new Copy { FromPath = "...", ToPath = "..." }` | `new Copy<JToken> { FromPath = "...", ToPath = "..." }` |
| `new Move { FromPath = "...", ToPath = "..." }` | `new Move<JToken> { FromPath = "...", ToPath = "..." }` |
| `new IfElse { Condition = ..., IfScript = ..., ElseScript = ... }` | `new IfElse<JToken> { ... }` |
| `new Compare { FirstPath = ..., SecondPath = ..., ResultPath = ... }` | `new Compare<JToken> { ... }` |
| `new Merge { Path = ..., TargetPath = ... }` | `new Merge<JToken> { ... }` |
| `new DecisionTable { Path = ..., DecisionTableConfig = ... }` | `new DecisionTable<JToken> { ... }` |

---

## Functions

| JLio | TLio |
|---|---|
| `new Fetch()` | `new Fetch<JToken>()` |
| `new Indirect()` | `new Indirect<JToken>()` |
| `new Promote()` | `new Promote<JToken>()` |
| `new Partial()` | `new Partial<JToken>()` |
| `new ScriptPath()` | `new ScriptPath<JToken>()` |
| `new Datetime()` | `new Datetime<JToken>()` |

Math / Text / TimeDate functions follow the same `<JToken>` generic suffix pattern.

---

## Command property renames (Copy/Move)

JLio's `Copy` and `Move` used `FromPath` / `ToPath` properties, which TLio keeps.
The previous scaffold had `From` / `To` — those have been renamed.

| Old TLio scaffold | Current TLio |
|---|---|
| `new Copy<T> { From = "...", To = "..." }` | `new Copy<T> { FromPath = "...", ToPath = "..." }` |
| `new Move<T> { From = "...", To = "..." }` | `new Move<T> { FromPath = "...", ToPath = "..." }` |

---

## Value types

| JLio | TLio | Notes |
|------|------|-------|
| `new FixedValue(jtoken, converter)` | `new FixedValue<JToken>(jtoken)` | Drop the converter argument |
| `FunctionSupportedValue` wrapping a function | `new FunctionSupportedValue<TNode>(function)` | Unchanged shape |
| Path-as-value (evaluated lazily) | `new PathValue<TNode>(path)` | New in TLio; returns first matching node at evaluation time |

---

## Extension functions

All extension function types follow the same `<JToken>` generic suffix pattern.

### Math (`TLio.Extensions.Math`)

`Sum`, `Avg`, `Count`, `Min`, `Max`, `Median`, `Ceiling`, `Floor`, `Round`, `Sqrt`,
`Pow`, `Abs`, `Subtract`, `Modulo`, `Calculate`, `SumIf`, `SumIfs`, `CountIf`,
`CountIfs`, `AverageIf`, `AverageIfs`, `MinIfs`, `MaxIfs`

### Text (`TLio.Extensions.Text`)

`Concat`, `Length`, `Substring`, `ToUpper`, `ToLower`, `Trim`, `TrimStart`,
`TrimEnd`, `StartsWith`, `EndsWith`, `Contains`, `Replace`, `Split`, `Join`,
`IndexOf`, `Format`, `Parse`, `PadLeft`, `PadRight`, `NewGuid`, `IsEmpty`

### TimeDate (`TLio.Extensions.TimeDate`)

`DateCompare`, `IsDateBetween`, `MinDate`, `MaxDate`, `AvgDate`

### ETL commands (`TLio.Extensions.ETL.Commands`)

`Flatten`, `Restore`, `Resolve`, `ToCsv`

---

## JSON / path adapter (new in TLio)

JLio operated on `JToken` directly. TLio commands and functions are format-agnostic
and operate through two interfaces:

| Concept | Interface | Newtonsoft impl |
|---------|-----------|-----------------|
| Node manipulation | `INodeAdapter<TNode>` | `JsonNodeAdapter` (obtained via `context.NodeAdapter`) |
| Path evaluation | `IItemsFetcher<TNode>` | `JsonPathItemsFetcher` (obtained via `context.ItemsFetcher`) |

Both are wired automatically by `JsonExecutionContext.CreateDefault()`.

**Do not reference `JToken`, `JObject`, `JArray`, or `JProperty` inside commands
or functions.** All node operations must go through `INodeAdapter<TNode>`.

---

## Test assertion modernisation (NUnit 4)

The constraint model is preferred in TLio tests but assertions are **not** changed
when porting (the hard requirement is identical assertions).

| JLio / NUnit classic | NUnit 4 constraint model |
|----------------------|--------------------------|
| `Assert.IsTrue(x)` | `Assert.That(x, Is.True)` |
| `Assert.IsFalse(x)` | `Assert.That(x, Is.False)` |
| `Assert.IsNull(x)` | `Assert.That(x, Is.Null)` |
| `Assert.IsNotNull(x)` | `Assert.That(x, Is.Not.Null)` |
| `Assert.AreEqual(a, b)` | `Assert.That(b, Is.EqualTo(a))` |
| `Assert.AreNotEqual(a, b)` | `Assert.That(b, Is.Not.EqualTo(a))` |
| `CollectionAssert.AreEqual(a, b)` | `Assert.That(b, Is.EqualTo(a))` |

---

## Test file count

| Category | Count |
|----------|-------|
| Ported from JLio | 70 |
| TLio-specific (adapter tests, architecture, fixture infra) | 5 |
| **Total** | **75** |

The 5 TLio-specific files are:
- `AdapterTests/JsonNodeAdapterTests.cs`
- `AdapterTests/JsonPathFetcherTests.cs`
- `CoreTests/ArchitectureTests.cs`
- `Fixtures/FixtureTests.cs`
- `Fixtures/FixtureTheoryLoader.cs`

`AdapterTests/JsonPathMethodsTests.cs` consolidates JLio's `JsonPathMethodsTests`
and `JsonPathMethodsEdgeCasesTests` into a single file (1 TLio file ← 2 JLio
originals).

---

## Example: porting a JLio test

**JLio original:**
```csharp
[SetUp]
public void Setup()
{
    executeOptions = ExecutionContext.CreateDefault();
    data = JToken.Parse("{ \"name\": \"world\" }");
}

[Test]
public void CanSetStringValue()
{
    var valueToSet = new FunctionSupportedValue(new FixedValue(new JValue("hello"), converter));
    var result = new Set("$.name", valueToSet).Execute(data, executeOptions);
    Assert.IsTrue(result.Success);
    Assert.AreEqual("hello", data.SelectToken("$.name")!.Value<string>());
}
```

**TLio equivalent (Newtonsoft):**
```csharp
[SetUp]
public void Setup()
{
    executeOptions = JsonExecutionContext.CreateDefault();
    data = JToken.Parse("{ \"name\": \"world\" }");
}

[Test]
public void CanSetStringValue()
{
    var valueToSet = new FunctionSupportedValue<JToken>(new FixedValue<JToken>(new JValue("hello")));
    var result = new Set<JToken>("$.name", valueToSet).Execute(data, executeOptions);
    Assert.IsTrue(result.Success);
    Assert.AreEqual("hello", data.SelectToken("$.name")!.Value<string>());
}
```

The assertion is **unchanged**. Only the types gained the `<JToken>` suffix and
`ExecutionContext.CreateDefault()` became `JsonExecutionContext.CreateDefault()`.

---

## Property Name Aliases (008)

Feature 008 added JLio-compatible property name aliases so JLio scripts run unchanged on TLio.

### Compare

| JLio name | TLio canonical name | Notes |
|-----------|-------------------|-------|
| `fromPath` | `FirstPath` | Write-only alias; both accepted in JSON scripts |
| `toPath` | `SecondPath` | Write-only alias; both accepted in JSON scripts |

### Merge

| JLio name | TLio canonical name | Notes |
|-----------|-------------------|-------|
| `fromPath` | `Path` | Write-only alias; both accepted in JSON scripts |
| `toPath` | `TargetPath` | Write-only alias; both accepted in JSON scripts |

### DecisionTable

| JLio key | TLio key | Notes |
|----------|---------|-------|
| `"decisionTable"` | `"config"` | Both accepted as the JSON property name for the config object |

### ETL Commands

ETL settings property names already match JLio (`flattenSettings`, `restoreSettings`, `csvSettings`, `resolveSettings`). No aliases required — these were always the correct property names; they were simply not deserialized due to a bug fixed in 008.

### New Functions (008)

| Function | Description |
|----------|-------------|
| `=newGuid()` | Generates a new UUID string |
| `=fetch(path, default)` | Returns default value when path resolves to nothing |
| `=path()` | Alias for `=scriptpath()` |
| `=promote(path, name)` | Wraps node in object with explicit property name |

---

## 009 — Special Character Escaping (escape-special-chars)

### Value escape sequences

TLio 009 introduces escape sequences for script value strings that start with a
trigger character (`@`, `$`, `=`).

| Trigger | Escape | Result |
|---------|--------|--------|
| `@` (path) | `@@value` | Literal string `@value` |
| `$` (path) | `$$value` | Literal string `$value` |
| `=` (function) | `==value` | Literal string `=value` |

**JLio behaviour**: JLio uses `@@` inside quoted strings to represent a literal `@`.
TLio 009 extends this to:
- unquoted values (e.g., `@@admin` as a top-level value, not just inside `'…'`)
- `$$` and `==` as TLio-specific extensions for `$` and `=` respectively

**Migration impact**: Scripts that pass `@@foo` as a value expecting a path `@foo`
will now receive the literal string `@foo` instead. Audit any scripts that use `@@`
in value fields before upgrading.

Inside quoted strings, `@@`, `$$`, and `==` are decoded to `@`, `$`, and `=`
respectively — consistent with the unquoted escape rules.

### Path bracket notation

All adapters now document and test bracket-quoted property names:
`$['server.host']` selects the property literally named `server.host`.
This was already supported in the underlying JSONPath libraries for JSON adapters;
009 adds support to `YamlPathItemsFetcher` and documents the convention for XML.

