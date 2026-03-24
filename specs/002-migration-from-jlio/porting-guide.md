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
