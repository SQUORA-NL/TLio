# Quickstart: Parse-Once Script Reuse and STJ Optimization

**Branch**: `013-parse-once-stj-optimize` | **Date**: 2026-04-22

---

## Before: one parse per execution (existing)

```csharp
var engine = new ScriptEngine<JsonNode>(commandsProvider, functionsProvider);

foreach (var item in items)
{
    var ctx    = SystemTextJsonExecutionContext.CreateDefault();
    var result = engine.Execute(scriptJson, item, ctx);   // parses every call
}
```

## After: compile once, execute many (new API)

```csharp
// --- Startup (once) ---
var engine   = new ScriptEngine<JsonNode>(commandsProvider, functionsProvider);
var ctx0     = SystemTextJsonExecutionContext.CreateDefault();     // adapter only needed for compile
var compiled = engine.Compile(scriptJson, ctx0);                   // parse happens here, once

// --- Per request (concurrent) ---
Parallel.ForEach(items, item =>
{
    var ctx    = SystemTextJsonExecutionContext.CreateDefault();   // fresh context per execution
    var result = compiled.Execute(item, ctx);                      // clone + run; no re-parse
    // use result.Data
});
```

## Advanced: take explicit control of the executable

```csharp
// Useful for pre-validation or manual lifetime management
var executable = compiled.CreateExecutable();           // TLioScript<TNode>
bool valid     = executable.Validate();
if (valid)
{
    var result = executable.Execute(item, ctx);
}
// executable is discarded after use; do not reuse across concurrent calls
```

## STJ optimization — mutation-aware, transparent

No API change is required. The path-selector cache and document cache activate automatically. The cache rebuilds whenever a command has mutated the node between selections, so every selection sees the correct post-mutation state.

```csharp
// Script: command 1 sets $.name = "updated", command 2 reads $.name
var ctx    = SystemTextJsonExecutionContext.CreateDefault();
var result = compiled.Execute(data, ctx);
// command 2 sees "updated" — cache detected the mutation and rebuilt

// Within a single command: 20 path selections on the same unchanged node
// → JsonDocument.Parse called once, not 20 times
```

---

## Disposal (optional, recommended for high-volume scenarios)

```csharp
using var fetcher = new SystemTextJsonPathItemsFetcher();
var ctx = new ExecutionContext<JsonNode>
{
    ItemsFetcher = fetcher,
    NodeAdapter  = new SystemTextJsonNodeAdapter(),
    Logger       = new ExecutionLogger()
};
var result = engine.Execute(scriptJson, data, ctx);
// fetcher.Dispose() called here — cached JsonDocument returned to pool
```
