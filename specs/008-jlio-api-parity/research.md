# Research: JLio API Parity

**Branch**: `008-jlio-api-parity` | **Date**: 2026-04-06

---

## US1: Property Name Alignment

### Finding 1 — Compare: `firstPath`/`secondPath` → `fromPath`/`toPath`

`TLio.Commands.Advanced.Compare<TNode>` has C# properties `FirstPath`, `SecondPath`, `ResultPath`.
`CommandConverter` uses `ToPascalCase(camelCaseName)` so JSON key `"firstPath"` → `"FirstPath"` works today.
JLio uses `"fromPath"` / `"toPath"` — these currently silently fail (no property found).

**Decision**: Add write-only alias properties `FromPath` / `ToPath` to `Compare<TNode>` that set `FirstPath` / `SecondPath`.
Precedence rule from spec: when both are present, JLio-aligned name takes precedence — achieved by setting the canonical property from both, and the JLio alias is typically the last-applied (JSON property order). CommandConverter applies properties in document order, so whichever appears last wins. The spec says JLio names take precedence — to enforce this explicitly, the alias setter should always overwrite.

### Finding 2 — Merge: `path`/`targetPath` → `fromPath`/`toPath`

`Merge<TNode>` has `Path` and `TargetPath`. JLio uses `"fromPath"` / `"toPath"`.
Same alias strategy: add `FromPath`/`ToPath` write-only properties setting `Path`/`TargetPath`.

### Finding 3 — DecisionTable: `"config"` → `"decisionTable"` key

`DecisionTable<TNode>` has `Config` property. JLio JSON key is `"decisionTable"`.
`ToPascalCase("decisionTable")` → `"DecisionTable"` — but C# prohibits a property with the same name as its enclosing type.

**Decision**: Handle in `CommandConverter.ParseCommand`. When the command instance is `DecisionTable<TNode>` and the JSON property name (ToPascalCase) is `"DecisionTable"`, redirect to the `"Config"` property. This is a single special-case lookup before the general reflection path.

### Finding 4 — ETL Settings: deserialization broken for all four commands

Confirmed C# property names:
- `Flatten<TNode>`: `FlattenSettings FlattenSettings` — JSON key `"flattenSettings"` → ToPascalCase → `"FlattenSettings"` ✅ name is correct
- `Restore<TNode>`: `RestoreSettings RestoreSettings` — JSON key `"restoreSettings"` → `"RestoreSettings"` ✅
- `ToCsv<TNode>`: `CsvSettings CsvSettings` — JSON key `"csvSettings"` → `"CsvSettings"` ✅
- `Resolve<TNode>`: `List<ResolveSetting<TNode>> ResolveSettings` — JSON key `"resolveSettings"` → `"ResolveSettings"` ✅

**Critical finding**: `CommandConverter.ConvertJsonValue` does not handle `FlattenSettings`, `RestoreSettings`, or `CsvSettings` types — they all fall through to `return null`, so the property is never set, leaving the default. ETL settings have never been configurable via JSON scripts.

**Decision — simple POCOs** (`FlattenSettings`, `RestoreSettings`, `CsvSettings`): Add a POCO fallback at the end of `ConvertJsonValue`: if target type is a non-abstract class with a parameterless constructor, deserialize using `System.Text.Json.JsonSerializer.Deserialize` with `PropertyNameCaseInsensitive = true`. `CommandConverter` already uses `System.Text.Json`, so no new dependency.

**Decision — complex generic type** (`List<ResolveSetting<TNode>>`): `ResolveSetting<TNode>` contains `List<ResolveValue<TNode>>`, and `ResolveValue<TNode>` has `IFunctionSupportedValue<TNode>? Value` — requires `FunctionConverter<TNode>` to parse `=func(...)` expressions. Cannot use `System.Text.Json` generic deserializer. Add explicit handling in `ConvertJsonValue` for `List<ResolveSetting<TNode>>` (type check via `IsGenericType` / `GetGenericTypeDefinition() == typeof(List<>)` with element type `ResolveSetting<>`).

**Conclusion on FR-004–FR-007**: Property key names already match JLio; the real fix is making those properties deserializable. No key aliasing needed.

---

## US2: Missing Core Functions

### Finding 5 — `newGuid`

No `NewGuid` function exists in `TLio.Functions`. Must create. Returns a new `Guid.NewGuid().ToString()` wrapped in `context.NodeAdapter.CreateString(...)`.

### Finding 6 — `fetch` with default value

`Fetch<TNode>` checks `Arguments.Count == 0` then evaluates `Arguments[0]` as a path. When the path resolves to nothing, it returns `FunctionResult.Failed`. Adding a second optional argument: if path returns nothing AND `Arguments.Count >= 2`, evaluate `Arguments[1]` as the default value and return it.

### Finding 7 — `path` alias for `scriptpath`

`ScriptPath<TNode>` is registered as `"scriptpath"` only. Adding `"path"` registration in `ParseOptions.CreateDefault()` with a new factory `() => new ScriptPath<TNode>()`. Both names must be registered independently (each call to `GetCommand` creates a fresh instance).

### Finding 8 — `promote` with explicit property name

`Promote<TNode>` uses `context.NodeAdapter.GetParentPropertyName(node)` to derive the wrapping key. Adding a second optional argument: if provided, use it as the property name directly instead of auto-deriving.

---

## US3: Fluent Builder API

### Finding 9 — `TLioScript<TNode>` and where fluent API lives

`TLioScript<TNode>` in `TLio.Core` extends `List<ICommand<TNode>>`. It has no fluent builder methods. `TLio.Core` does not reference `TLio.Commands` — commands are registered separately.

**Decision**: Implement fluent API as **extension methods on `TLioScript<TNode>`** defined in `TLio.Client` namespace. Extension methods can return intermediate builder types (`ValueOnPathBuilder<TNode>`, `CopyMoveBuilder<TNode>`, etc.) that also live in `TLio.Client`. This adds no new projects and no changes to `TLio.Core`.

Intermediate builder objects hold a reference to the parent `TLioScript<TNode>` and construct the appropriate command when the chain is terminated (e.g., `.OnPath(path)`, `.To(path)`, `.Result(path)`).

### Finding 10 — `TLioConvert.Parse` needs `INodeAdapter`

`CommandConverter<TNode>` requires an `INodeAdapter<TNode>` (used to parse literal values via `INodeAdapter.Parse`). The spec signature `TLioConvert.Parse<TNode>(string, ParseOptions<TNode>)` omits it.

**Decision**: `TLioConvert.Parse` signature includes `INodeAdapter<TNode>` as third parameter. The spec acceptance scenario (`TLioConvert.Parse<JToken>(scriptText)`) is illustrative — adapter is always needed to parse raw JSON values in script commands. API: `TLioConvert.Parse<TNode>(string scriptJson, ParseOptions<TNode> options, INodeAdapter<TNode> adapter)`.

### Finding 11 — `TLioConvert.Serialize`

Each `ICommand<TNode>` needs to expose its properties for serialization. Current commands have no serialization support. Serialize will use reflection (same PascalCase → camelCase inverse of parsing) to produce a JSON object per command, then wrap in a JSON array.

**Decision**: Implement `TLioConvert.Serialize<TNode>(TLioScript<TNode>)` using reflection — iterate command properties, camelCase the names, serialize known types (`string`, `bool`, `ArrayMergeMode`, `IFunctionSupportedValue` via `ToScript()`). Output: JSON string of the array.

---

## US4: Text Extension Pack

### Finding 12 — Project placement

Article VII question: "Could text functions go in `TLio.Functions`?" Yes, technically. But `TLio.Extensions.ETL` establishes the pattern of optional extension packs. Text manipulation is a separable concern. Consumers needing only core transformation should not be forced to take text functions. Decision: new project `TLio.Extensions.Text`, parallel to `TLio.Extensions.ETL`.

### Finding 13 — All text operations via NodeAdapter

Text functions receive `TNode` arguments, extract strings via `context.NodeAdapter.TryGetString()`, operate on the `string`, return via `context.NodeAdapter.CreateString(result)`. This satisfies Articles I, II, IV. Format-agnostic for all adapters.

### Finding 14 — `parse` function

`parse` converts a JSON/XML/YAML string literal into a node via `context.NodeAdapter.Parse(str)`. This is the inverse of `toString`. For XML/YAML adapters, `Parse` parses the string using the adapter's format — the consumer must provide a valid string for the target format.

---

## US5: AI Reference Format

### Finding 15 — Existing ai-ref files gap analysis

All 26 existing `docs/ai-ref/*.md` files lack: Intent line (one-sentence purpose), Supports functions flag, C# Fluent API section. Property names in Compare.md and Merge.md show old TLio names (`firstPath`/`secondPath`, `path`/`targetPath`) — must be updated to JLio-aligned names.

New files needed: `NewGuid.md`, `Path.md`, and 12 text function files.

---

## Resolved Decisions

| Decision | Rationale |
|----------|-----------|
| Alias properties in Compare/Merge | Simple, no CommandConverter changes needed |
| Special-case `"decisionTable"` in CommandConverter | Cannot add same-named property to a class in C# |
| POCO fallback in ConvertJsonValue via System.Text.Json | No new dependency; handles all simple ETL settings |
| Custom `List<ResolveSetting<TNode>>` parsing | `IFunctionSupportedValue<TNode>` requires FunctionConverter |
| Fluent API as extension methods in TLio.Client | No new project; no changes to TLio.Core |
| TLioConvert.Parse takes INodeAdapter | Adapter always required for literal value parsing |
| TLio.Extensions.Text is a new project | Follows ETL pattern; optional dependency |
