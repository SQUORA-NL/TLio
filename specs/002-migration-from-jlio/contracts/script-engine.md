# Contract: TLio Script Engine & Registration API

**Package**: `TLio.Client`
**Date**: 2026-03-24

The client-facing API for parsing and executing TLio scripts.

---

## ScriptEngine\<TNode\>

Entry point for script execution.

```csharp
public class ScriptEngine<TNode>
{
    public ScriptEngine(ICommandsProvider<TNode> commandsProvider,
                        IFunctionsProvider<TNode> functionsProvider);

    // Parse a JSON script string and execute against data
    public TLioExecutionResult<TNode> Execute(string scriptText, TNode data,
                                              IExecutionContext<TNode> context);

    // Execute a pre-parsed script object directly
    public TLioExecutionResult<TNode> Execute(TLioScript<TNode> script, TNode data,
                                              IExecutionContext<TNode> context);
}
```

---

## ParseOptions\<TNode\>

Convenience builder that wires built-in commands and functions.

```csharp
public class ParseOptions<TNode>
{
    public ICommandsProvider<TNode> CommandsProvider { get; }
    public IFunctionsProvider<TNode> FunctionsProvider { get; }

    public static ParseOptions<TNode> CreateDefault();

    public ParseOptions<TNode> RegisterCommand<TCommand>()
        where TCommand : ICommand<TNode>, new();

    public ParseOptions<TNode> RegisterFunction<TFunction>()
        where TFunction : IFunction<TNode>, new();
}
```

**Typical usage**:
```csharp
var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var context = JsonExecutionContext.CreateDefault();
var result  = engine.Execute(scriptJson, data, context);
```

---

## JSON Script Format

Scripts are always JSON arrays of command objects regardless of data format:

```json
[
  { "command": "set",    "path": "$.name",    "value": "Alice" },
  { "command": "add",    "path": "$.tags",    "value": "admin" },
  { "command": "remove", "path": "$.temp" },
  { "command": "copy",   "fromPath": "$.source", "toPath": "$.dest" },
  { "command": "set",    "path": "$.total",   "value": "=sum($.items[*].price)" }
]
```

**Discriminator field**: `"command"` — maps to registered `ICommand<TNode>` factory.

**Function expressions**: String values starting with `=` are parsed as function calls, e.g. `"=sum($.items[*].price)"`.

---

## TLioScript\<TNode\> / TLioExecutionResult\<TNode\>

```csharp
public class TLioScript<TNode>
{
    public IList<ICommand<TNode>> Commands { get; }
    public TLioExecutionResult<TNode> Execute(TNode data, IExecutionContext<TNode> context);
}

public class TLioExecutionResult<TNode>
{
    public bool Success { get; }
    public TNode Data { get; }          // data mutated in-place
    public LogEntries Logging { get; }
}
```

---

## Adapter Factories

### TLio.Json (Newtonsoft)

```csharp
// Returns IExecutionContext<JToken> with JsonNodeAdapter + JsonPathItemsFetcher
IExecutionContext<JToken> context = JsonExecutionContext.CreateDefault();
```

### TLio.Json.SystemText (System.Text.Json)

```csharp
// Returns IExecutionContext<JsonNode> with SystemTextJsonNodeAdapter + SystemTextJsonPathItemsFetcher
IExecutionContext<JsonNode> context = SystemTextJsonExecutionContext.CreateDefault();
```
