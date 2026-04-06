# Data Model: JLio API Parity

**Branch**: `008-jlio-api-parity` | **Date**: 2026-04-06

---

## Modified Entities (existing, extended)

### Compare`<TNode>` — `TLio.Commands.Advanced`

| Property | Type | Status | Notes |
|---|---|---|---|
| `FirstPath` | `string?` | existing | Canonical TLio name |
| `SecondPath` | `string?` | existing | Canonical TLio name |
| `ResultPath` | `string?` | existing | Same in both JLio and TLio |
| `FromPath` | `string?` (write-only) | **new** | Alias: sets `FirstPath` |
| `ToPath` | `string?` (write-only) | **new** | Alias: sets `SecondPath` |

### Merge`<TNode>` — `TLio.Commands.Advanced`

| Property | Type | Status | Notes |
|---|---|---|---|
| `Path` | `string?` | existing | Canonical TLio source path |
| `TargetPath` | `string?` | existing | Canonical TLio target path |
| `ArrayMergeMode` | `ArrayMergeMode` | existing | Unchanged |
| `FromPath` | `string?` (write-only) | **new** | Alias: sets `Path` |
| `ToPath` | `string?` (write-only) | **new** | Alias: sets `TargetPath` |

### CommandConverter`<TNode>` — `TLio.Client`

| Change | Notes |
|---|---|
| `ParseCommand`: `"decisionTable"` key → `Config` property | Special-case before general reflection loop |
| `ConvertJsonValue`: POCO fallback | `System.Text.Json.JsonSerializer.Deserialize` for non-generic class types |
| `ConvertJsonValue`: `List<ResolveSetting<TNode>>` | Custom parsing using `FunctionConverter<TNode>` for `Value` fields |

### Fetch`<TNode>` — `TLio.Functions`

| Change | Notes |
|---|---|
| Optional `Arguments[1]` (default value) | When path resolves to nothing AND `Arguments.Count >= 2`, return second argument |

### Promote`<TNode>` — `TLio.Functions`

| Change | Notes |
|---|---|
| Optional `Arguments[1]` (property name) | When present, use as wrapping key instead of `GetParentPropertyName()` |

### ParseOptions`<TNode>` — `TLio.Client`

| Change | Notes |
|---|---|
| Register `"path"` → `ScriptPath<TNode>` | Second registration; both `"scriptpath"` and `"path"` active |
| Register `"newGuid"` → `NewGuid<TNode>` | New function |

---

## New Entities

### NewGuid`<TNode>` — `TLio.Functions`

```
FunctionName: "newGuid"
Arguments:    none
Returns:      string node containing a new random UUID (Guid.NewGuid().ToString())
```

### TLioScriptExtensions — `TLio.Client`

Extension methods on `TLioScript<TNode>`. Each entry-point method returns an intermediate builder.

| Method | Returns | Creates command |
|---|---|---|
| `.Add(TNode value)` | `ValueOnPathBuilder<TNode>` | `Add<TNode>` on `.OnPath(path)` |
| `.Set(TNode value)` | `ValueOnPathBuilder<TNode>` | `Set<TNode>` on `.OnPath(path)` |
| `.Put(TNode value)` | `ValueOnPathBuilder<TNode>` | `Put<TNode>` on `.OnPath(path)` |
| `.Remove()` | `RemoveOnPathBuilder<TNode>` | `Remove<TNode>` on `.OnPath(path)` |
| `.Copy()` | `CopyMoveFromBuilder<TNode>` | `Copy<TNode>` on `.From(p).To(p)` |
| `.Move()` | `CopyMoveFromBuilder<TNode>` | `Move<TNode>` on `.From(p).To(p)` |
| `.Compare()` | `CompareFromBuilder<TNode>` | `Compare<TNode>` on `.From(p).To(p).Result(p)` |
| `.Merge()` | `MergeFromBuilder<TNode>` | `Merge<TNode>` on `.From(p).To(p)` |
| `.IfElse(condition)` | `IfElseIfBuilder<TNode>` | `IfElse<TNode>` on `.If(script).Else(script)` |

### Intermediate Builder Types — `TLio.Client`

All builders hold a reference to the parent `TLioScript<TNode>` and the partially accumulated state. Terminating methods add the constructed command to the script and return the script.

| Builder | Holds | Terminates with |
|---|---|---|
| `ValueOnPathBuilder<TNode>` | script, value (as `FixedValue<TNode>`), command type | `.OnPath(string) → TLioScript<TNode>` |
| `RemoveOnPathBuilder<TNode>` | script | `.OnPath(string) → TLioScript<TNode>` |
| `CopyMoveFromBuilder<TNode>` | script, command type (copy/move) | `.From(string) → CopyMoveToBuilder<TNode>` |
| `CopyMoveToBuilder<TNode>` | script, command type, fromPath | `.To(string) → TLioScript<TNode>` |
| `CompareFromBuilder<TNode>` | script | `.From(string) → CompareToBuilder<TNode>` |
| `CompareToBuilder<TNode>` | script, fromPath | `.To(string) → CompareResultBuilder<TNode>` |
| `CompareResultBuilder<TNode>` | script, fromPath, toPath | `.Result(string) → TLioScript<TNode>` |
| `MergeFromBuilder<TNode>` | script | `.From(string) → MergeToBuilder<TNode>` |
| `MergeToBuilder<TNode>` | script, fromPath | `.To(string) → TLioScript<TNode>` |
| `IfElseIfBuilder<TNode>` | script, condition | `.If(TLioScript<TNode>) → IfElseElseBuilder<TNode>` |
| `IfElseElseBuilder<TNode>` | script, condition, ifScript | `.Else(TLioScript<TNode>) → TLioScript<TNode>` |

### TLioConvert — `TLio.Client`

Static helper class.

| Method | Signature | Notes |
|---|---|---|
| `Parse` | `TLioScript<TNode> Parse<TNode>(string, ParseOptions<TNode>, INodeAdapter<TNode>)` | Delegates to `CommandConverter<TNode>` |
| `Serialize` | `string Serialize<TNode>(TLioScript<TNode>)` | Reflection-based; camelCases property names; uses `ToScript()` for `IFunctionSupportedValue` |

---

## New Project: TLio.Extensions.Text

Project file: `TLio.Extensions.Text/TLio.Extensions.Text.csproj`

References: `TLio.Core` (only). No format libraries.

### Text Function Classes

All classes extend `FunctionBase<TNode>`. All extract strings via `TryGetString`, operate on `string`, return via `CreateString`.

| Class | FunctionName | Arguments | Returns |
|---|---|---|---|
| `Concat<TNode>` | `"concat"` | 2+ nodes | concatenated string |
| `ToStringFunction<TNode>` | `"toString"` | 1 node | string representation |
| `Parse<TNode>` | `"parse"` | 1 string node | parsed node via `NodeAdapter.Parse` |
| `Format<TNode>` | `"format"` | 2 nodes (template, value) | formatted string |
| `Length<TNode>` | `"length"` | 1 string node | integer string length |
| `Substring<TNode>` | `"substring"` | 2-3 (str, start, [count]) | substring |
| `Replace<TNode>` | `"replace"` | 3 (str, old, new) | replaced string |
| `ToLower<TNode>` | `"toLower"` | 1 string node | lowercased string |
| `ToUpper<TNode>` | `"toUpper"` | 1 string node | uppercased string |
| `Trim<TNode>` | `"trim"` | 1 string node | trimmed string |
| `TrimStart<TNode>` | `"trimStart"` | 1 string node | left-trimmed string |
| `TrimEnd<TNode>` | `"trimEnd"` | 1 string node | right-trimmed string |

### RegisterTextPack — `TLio.Extensions.Text`

```csharp
public static IFunctionsProviderRegistrar<TNode> RegisterTextPack<TNode>(
    this IFunctionsProviderRegistrar<TNode> registrar)
```

Registers all 12 text functions. Parallel to `RegisterETL` on `ICommandsProviderRegistrar`.

---

## State Transitions

None — all changes are additive. No existing command or function changes its runtime behaviour when called through the existing API. Alias properties forward to canonical properties; existing tests pass unchanged.
