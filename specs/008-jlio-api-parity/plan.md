# Implementation Plan: JLio API Parity

**Branch**: `008-jlio-api-parity` | **Date**: 2026-04-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-jlio-api-parity/spec.md`

---

## Summary

Add JLio property name aliases to `Compare`, `Merge`, and `DecisionTable` commands; fix ETL settings deserialization in `CommandConverter`; add `newGuid` function, `path` alias, `fetch` default arg, `promote` name arg; implement fluent builder API as extension methods in `TLio.Client`; create `TLio.Extensions.Text` with 12 string functions; update all `docs/ai-ref/*.md` files to include Intent, Supports-functions, and C# Fluent API sections.

---

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: Newtonsoft.Json (adapter), System.Text.Json (CommandConverter, TLio.Client)  
**Storage**: N/A (scripting framework)  
**Testing**: xUnit with file-based fixture triplets  
**Target Platform**: .NET 10 class library (multi-adapter)  
**Project Type**: Class library  
**Performance Goals**: No regressions; JLio-compatible parsing must add < 1ms overhead  
**Constraints**: All Core/Commands/Functions code must remain format-agnostic (Article I/IX)  
**Scale/Scope**: 5 user stories, ~22 FRs; all changes are additive

---

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing format-specific types? | No — all new code uses `context.NodeAdapter`; `NewGuid` uses `CreateString`; text functions use `TryGetString`/`CreateString`. CommandConverter already uses `System.Text.Json` for parsing (not a format adapter). |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected via `IExecutionContext<TNode>`? | Yes — no `new ConcreteAdapter()` in any new command or function code. |
| Generic-First | III | Does every new public API carry `<TNode>`? | Yes — all new functions, builders, and TLioConvert carry `<TNode>`. |
| Process/Execution separation | IV | Do all node reads/mutations go through `context.NodeAdapter`? | Yes — text functions and NewGuid use only `CreateString`, `TryGetString` via adapter. Alias properties in commands are pure string setters. |
| Swappable Selection | V | Are all path expressions supplied by callers? | Yes — no path strings are hard-coded inside any new or modified command/function. |
| Test-First + Fixture Triplets | VI | Will every full-script-execution test use file-based fixture triplets? | Yes — all command/function tests use `input.json / script.json / result.json` triplets. |
| Simplicity Gate | VII | Could this be done with fewer projects? | Text functions could go in `TLio.Functions`, but `TLio.Extensions.ETL` establishes the extension pack pattern for optional features. Text manipulation is a separable optional concern. Separate project is justified. Fluent builder goes in `TLio.Client` (no new project). |
| Backward Migration Path | VIII | Is all changed/dropped behaviour documented in porting-guide.md? | Yes — FR-008 requires documenting all property aliases in porting-guide.md before merge. All changes are additive; no existing behaviour changed. |
| No Leaking Internals | IX | Do `TLio.Core` public APIs expose only `TNode`-parameterised types? | Yes — no changes to TLio.Core contracts. Builders live in TLio.Client. |
| Logging as Observability | X | Does every `Execute()` path call `LogInfo` on success? | Yes — `NewGuid` and text functions must call `LogInfo`/`LogWarning` per Article X. |
| AI Component Reference | XI | Does every new command/function have an `ai-ref.md`? | Yes — `NewGuid.md`, `Path.md`, and 12 text function files will be created; all existing files updated with Intent/Supports-functions/Fluent-API sections. |

---

## Project Structure

### Documentation (this feature)

```text
specs/008-jlio-api-parity/
├── plan.md              ← this file
├── research.md          ← phase 0 output
├── data-model.md        ← phase 1 output
├── quickstart.md        ← phase 1 output
├── contracts/
│   ├── fluent-api.md    ← fluent builder + TLioConvert API
│   └── text-pack-api.md ← text function pack API
└── tasks.md             ← phase 2 output (speckit.tasks)
```

### Source Code Changes

```text
TLio.Commands/
  Advanced/
    Compare.cs          ← ADD: FromPath / ToPath alias properties
    Merge.cs            ← ADD: FromPath / ToPath alias properties

TLio.Functions/
  Fetch.cs              ← MODIFY: optional default argument (Arguments[1])
  Promote.cs            ← MODIFY: optional name argument (Arguments[1])
  NewGuid.cs            ← NEW: =newGuid() function

TLio.Client/
  CommandConverter.cs   ← MODIFY: decisionTable key → Config; POCO fallback; ResolveSetting parsing
  ParseOptions.cs       ← MODIFY: register "path" alias + "newGuid"
  Fluent/               ← NEW: builder extension methods
    TLioScriptExtensions.cs
    ValueOnPathBuilder.cs
    RemoveOnPathBuilder.cs
    CopyMoveBuilders.cs
    CompareBuilders.cs
    MergeBuilders.cs
    IfElseBuilders.cs
  TLioConvert.cs        ← NEW: Parse + Serialize static helpers

TLio.Extensions.Text/   ← NEW PROJECT
  TLio.Extensions.Text.csproj
  GlobalUsings.cs
  Functions/
    Concat.cs
    ToStringFunction.cs
    ParseFunction.cs
    FormatFunction.cs
    Length.cs
    Substring.cs
    Replace.cs
    ToLower.cs
    ToUpper.cs
    Trim.cs
    TrimStart.cs
    TrimEnd.cs
  RegisterTextPack.cs

docs/ai-ref/
  functions/
    NewGuid.md          ← NEW (FR-021)
    Path.md             ← NEW (FR-021)
    Fetch.md            ← UPDATE (add 2-arg signature)
    Promote.md          ← UPDATE (add 2-arg signature)
    ScriptPath.md       ← UPDATE (Intent, Fluent API)
    [all 20 existing files] ← UPDATE (Intent, Supports-functions, Fluent API)
    Concat.md … TrimEnd.md  ← NEW × 12 (FR-022)

specs/002-migration-from-jlio/
  porting-guide.md      ← ADD: property alias section (FR-008)
```

### Test Projects

| Layer | Test project |
|---|---|
| Compare/Merge alias properties | `TLio.UnitTests/` |
| CommandConverter POCO fallback | `TLio.UnitTests/` |
| CommandConverter decisionTable key | `TLio.UnitTests/` |
| NewGuid, Fetch(default), Promote(name), path alias | `TLio.Functions.Tests/` |
| Fluent builder + TLioConvert | `TLio.UnitTests/` |
| Text functions | `TLio.Functions.Tests/` (new Text category) |
| ETL settings via JSON (integration) | `TLio.Json.Tests/` |

---

## Implementation Notes

### US1 — Property Name Aliases

**Compare**: Add two write-only properties to `Compare<TNode>`:
```csharp
public string? FromPath { set => FirstPath = value; }
public string? ToPath   { set => SecondPath = value; }
```
Precedence: `CommandConverter` applies properties in document order. JLio scripts put `fromPath`/`toPath`; TLio scripts put `firstPath`/`secondPath`. Both work; no additional logic needed for precedence since they write to the same canonical field.

**Merge**: Same pattern:
```csharp
public string? FromPath { set => Path = value; }
public string? ToPath   { set => TargetPath = value; }
```

**DecisionTable**: In `CommandConverter.ParseCommand`, before the general reflection loop, check:
```csharp
if (propName == "DecisionTable" && command is DecisionTable<TNode>)
    propName = "Config";
```
Then continue with the general property lookup for `Config`.

**ETL Settings**: In `ConvertJsonValue`, add a POCO fallback after all existing cases:
```csharp
// Generic POCO fallback for non-generic class types
if (targetType.IsClass && !targetType.IsAbstract && !targetType.IsGenericType)
{
    try
    {
        return JsonSerializer.Deserialize(element.GetRawText(), targetType,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch { return null; }
}
```
This handles `FlattenSettings`, `RestoreSettings`, `CsvSettings`.

**Resolve settings** (`List<ResolveSetting<TNode>>`): Add explicit handling in `ConvertJsonValue`:
```csharp
if (targetType.IsGenericType
    && targetType.GetGenericTypeDefinition() == typeof(List<>)
    && targetType.GetGenericArguments()[0].IsGenericType
    && targetType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(ResolveSetting<>))
{
    return ParseResolveSettings(element);
}
```
`ParseResolveSettings` iterates the JSON array, constructs `ResolveSetting<TNode>`, and uses `ConvertJsonValue(el, typeof(IFunctionSupportedValue<TNode>))` for `Value` fields.

### US2 — Missing Core Functions

**NewGuid**: Simplest function — no arguments, returns `context.NodeAdapter.CreateString(Guid.NewGuid().ToString())`.

**fetch default**: Check `Arguments.Count >= 2` in the "path resolved to nothing" branch:
```csharp
if (nodes.Count == 0)
{
    if (Arguments.Count >= 2)
    {
        var def = Arguments[1].GetValue(currentNode, dataContext, context);
        if (def.Success) return def;
    }
    return FunctionResult<TNode>.Failed(currentNode);
}
```

**path alias**: Single line in `ParseOptions.CreateDefault()`:
```csharp
options.FunctionsProvider.Register("path", () => new ScriptPath<TNode>());
```

**promote name**: Check `Arguments.Count >= 2`:
```csharp
var propertyName = Arguments.Count >= 2
    ? context.NodeAdapter.TryGetString(Arguments[1].GetValue(currentNode, dataContext, context).Data.First!)
    : context.NodeAdapter.GetParentPropertyName(node);
```

### US3 — Fluent Builder

Builders are lightweight structs or classes in `TLio.Client.Fluent` (or `TLio.Client` namespace). Each holds a reference to the parent `TLioScript<TNode>` and creates+adds a command on the terminating method call. No state beyond what is needed to construct the command.

`TLioConvert.Serialize`: Use reflection to iterate `command.GetType().GetProperties()`, camelCase each name, switch on type to serialize. Properties with null or default values are omitted.

### US4 — Text Extension Pack

`TLio.Extensions.Text` only references `TLio.Core`. `TLio.Core` provides `FunctionBase<TNode>` and `INodeAdapter<TNode>`. No format-specific references.

`Format` function: Uses `string.Format("{0}", value)` pattern — template is `Arguments[0]`, value is `Arguments[1]`.

`Length` returns a number node: `context.NodeAdapter.CreateNumber(str.Length)`.

### US5 — AI Reference Updates

Update existing files to add three sections after the existing content:
1. `**Intent**: <one sentence>` — at the very top (after the `#` heading)
2. `**Supports functions**: ✅/❌` — after Options table
3. `## C# Fluent API` — at the end, with one code example

Update `Compare.md` and `Merge.md` Options tables to show JLio-aligned names as primary.

---

## Complexity Tracking

No constitution violations. All changes are additive within existing projects except the new `TLio.Extensions.Text` project, which is justified by the extension pack pattern.
