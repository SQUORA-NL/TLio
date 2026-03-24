# TLio Quick Start — JLio Migration

**Date**: 2026-03-24

This guide covers the minimum steps to execute a TLio script, port a JLio test, and add a new command or function.

---

## 1. Execute a script (Newtonsoft)

```csharp
using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Json;

// 1. Create engine with all built-in commands + functions
var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

// 2. Create execution context (Newtonsoft adapter)
var context = JsonExecutionContext.CreateDefault();

// 3. Parse data and script
var data   = JToken.Parse(@"{ ""name"": ""world"" }");
var script = @"[{ ""command"": ""set"", ""path"": ""$.name"", ""value"": ""hello"" }]";

// 4. Execute
var result = engine.Execute(script, data, context);

// 5. Check result
if (result.Success)
    Console.WriteLine(data["name"]);   // "hello"
```

---

## 2. Execute a script (System.Text.Json)

```csharp
using System.Text.Json.Nodes;
using TLio.Client;
using TLio.Json.SystemText;

var options = ParseOptions<JsonNode>.CreateDefault();
var engine  = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
var context = SystemTextJsonExecutionContext.CreateDefault();

var data   = JsonNode.Parse(@"{ ""name"": ""world"" }");
var script = @"[{ ""command"": ""set"", ""path"": ""$.name"", ""value"": ""hello"" }]";

var result = engine.Execute(script, data!, context);
```

---

## 3. Port a JLio unit test

See `specs/002-migration-from-jlio/porting-guide.md` for the full substitution table.

**Before (JLio)**:
```csharp
var ctx = ExecutionContext.CreateDefault();
var result = new Set("$.name", new FunctionSupportedValue(new FixedValue(new JValue("hello"), converter)))
    .Execute(data, ctx);
```

**After (TLio — Newtonsoft)**:
```csharp
var ctx = JsonExecutionContext.CreateDefault();
var result = new Set<JToken>("$.name", new FunctionSupportedValue<JToken>(new FixedValue<JToken>(new JValue("hello"))))
    .Execute(data, ctx);
```

The assertion is **unchanged**.

---

## 4. Run ported tests against both adapters

TLio ships a shared test base class for adapter compliance:

```csharp
public class MyCommandTests_Newtonsoft : JsonAdapterComplianceTestBase<JToken>
{
    protected override IExecutionContext<JToken> CreateContext() =>
        JsonExecutionContext.CreateDefault();
}

public class MyCommandTests_SystemText : JsonAdapterComplianceTestBase<JsonNode>
{
    protected override IExecutionContext<JsonNode> CreateContext() =>
        SystemTextJsonExecutionContext.CreateDefault();
}
```

---

## 5. Add a new command

```csharp
// In TLio.Commands
public class MyCommand<TNode> : CommandBase<TNode>
{
    public override string CommandName => "mycommand";

    protected override TLioExecutionResult<TNode> ExecuteOnPath(
        string path, TNode data, IExecutionContext<TNode> context)
    {
        var nodes = context.ItemsFetcher.SelectNodes(path, data);
        foreach (var node in nodes.Nodes)
        {
            // Use context.NodeAdapter for all node operations — never call methods on TNode directly
            var newValue = context.NodeAdapter.CreateString("replaced");
            context.NodeAdapter.Replace(node, newValue);
            context.LogInfo(CommandName, $"Replaced node at {path}");
        }
        return TLioExecutionResult<TNode>.Success(data, context.GetLogEntries());
    }
}
```

**Register** it in your `ParseOptions`:
```csharp
var options = ParseOptions<JToken>.CreateDefault()
    .RegisterCommand<MyCommand<JToken>>();
```

---

## 6. Add a new function

```csharp
// In TLio.Functions (or extension pack)
public class MyFunc<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "myfunc";

    public override FunctionResult<TNode> Execute(
        TNode currentNode, TNode dataContext,
        Arguments<TNode> arguments, IExecutionContext<TNode> context)
    {
        // Evaluate first argument
        var arg = arguments.Items[0].GetValue(currentNode, dataContext, context);
        if (!arg.Success) return FunctionResult<TNode>.Failure("arg failed");

        var result = context.NodeAdapter.CreateString("computed");
        return FunctionResult<TNode>.Success(result);
    }
}
```

---

## 7. Data-driven fixture test pattern

Each fixture directory contains:
```
tests/fixtures/set/basic-string/
├── input.json   { "name": "world" }
├── script.json  [{ "command": "set", "path": "$.name", "value": "hello" }]
└── result.json  { "name": "hello" }
```

xUnit Theory runner (shared):
```csharp
[Theory]
[MemberData(nameof(DiscoverFixtures), "set")]
public void FixturePasses(string fixturePath)
{
    var input  = JToken.Parse(File.ReadAllText(Path.Combine(fixturePath, "input.json")));
    var script = File.ReadAllText(Path.Combine(fixturePath, "script.json"));
    var expected = JToken.Parse(File.ReadAllText(Path.Combine(fixturePath, "result.json")));

    var result = engine.Execute(script, input, context);

    Assert.True(result.Success);
    Assert.True(JToken.DeepEquals(expected, result.Data));
}
```
