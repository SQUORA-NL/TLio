# Public API Contract: Parse-Once Script Reuse and STJ Optimization

**Branch**: `013-parse-once-stj-optimize` | **Date**: 2026-04-22

---

## Added to `ICommand<TNode>` (TLio.Core)

```csharp
// Default interface method — non-breaking addition
ICommand<TNode> Clone()
```

**Semantics**: Returns an independent copy of this command with its own mutable execution state. The clone shares read-only configuration data with the original (path expressions, values, config). Throws `NotSupportedException` if not overridden and not derived from `CommandBase<TNode>`.

**Breaking change risk**: None. Default interface methods in C# do not break existing implementations.

---

## Added to `CommandBase<TNode>` (TLio.Core)

```csharp
public override ICommand<TNode> Clone()
    => (ICommand<TNode>)MemberwiseClone();
```

All concrete commands that derive from `CommandBase<TNode>` inherit this for free.

---

## New class `CompiledScript<TNode>` (TLio.Client)

```csharp
public sealed class CompiledScript<TNode>
{
    // Produced via ScriptEngine<TNode>.Compile(...)
    // Constructor is internal.

    /// Returns a new TLioScript<TNode> with every command independently cloned.
    /// Safe to call concurrently from multiple threads.
    public TLioScript<TNode> CreateExecutable();

    /// Convenience: clone + execute in one call.
    public TLioExecutionResult<TNode> Execute(TNode data, IExecutionContext<TNode> context);
}
```

---

## Added to `ScriptEngine<TNode>` (TLio.Client)

```csharp
/// Parse scriptText once and return a reusable compiled handle.
public CompiledScript<TNode> Compile(string scriptText, INodeAdapter<TNode> adapter);

/// Convenience overload using context.NodeAdapter.
public CompiledScript<TNode> Compile(string scriptText, IExecutionContext<TNode> context);
```

**Existing signatures unchanged**:
```csharp
// Still works, still re-parses on each call (behaviour preserved).
public TLioExecutionResult<TNode> Execute(string scriptText, TNode data, IExecutionContext<TNode> context);
public TLioExecutionResult<TNode> Execute(TLioScript<TNode> script, TNode data, IExecutionContext<TNode> context);
```

---

## Typical usage pattern

```csharp
// Startup — once
var engine   = new ScriptEngine<JsonNode>(commandsProvider, functionsProvider);
var compiled = engine.Compile(scriptJson, context.NodeAdapter);

// Per request — concurrent-safe
Parallel.ForEach(items, item =>
{
    var ctx    = SystemTextJsonExecutionContext.CreateDefault();
    var result = compiled.Execute(item, ctx);
    // use result
});
```

---

## `SystemTextJsonPathItemsFetcher` — internal change, no API surface change

The fetcher now implements `IDisposable`. No change to `IItemsFetcher<TNode>` interface.

```csharp
// SystemTextJsonPathItemsFetcher now implements IDisposable
public void Dispose();
```

Callers who construct `ExecutionContext<JsonNode>` directly may wrap it in `using` to dispose the cached document eagerly. Callers who use `SystemTextJsonExecutionContext.CreateDefault()` and let the context go out of scope are correct — GC will collect the document.
