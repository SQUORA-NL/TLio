# Research: AI Reference Documentation for TLio

## Command Registration & JSON Name Mapping

**Decision**: Use the registered command string names (from `ParseOptions.CreateDefault()`) as the
canonical names in all ai-ref.md files.

**Rationale**: The `CommandConverter` uses camelCase → PascalCase reflection mapping. The script
discriminator is always the registered string name, not the C# class name.

**Canonical names confirmed** (from `TLio.Client/ParseOptions.cs` and
`TLio.Extensions.ETL/RegisterETLPack.cs`):

| Command | JSON `"command"` value | C# class |
|---------|----------------------|---------|
| Set     | `"set"`              | `Set<TNode>` |
| Add     | `"add"`              | `Add<TNode>` |
| Put     | `"put"`              | `Put<TNode>` |
| Remove  | `"remove"`           | `Remove<TNode>` |
| Copy    | `"copy"`             | `Copy<TNode>` |
| Move    | `"move"`             | `Move<TNode>` |
| IfElse  | `"ifElse"`           | `IfElse<TNode>` |
| Compare | `"compare"`          | `Compare<TNode>` |
| Merge   | `"merge"`            | `Merge<TNode>` |
| DecisionTable | `"decisionTable"` | `DecisionTable<TNode>` |
| Flatten | `"flatten"`          | `Flatten<TNode>` (ETL) |
| Restore | `"restore"`          | `Restore<TNode>` (ETL) |
| Resolve | `"resolve"`          | `Resolve<TNode>` (ETL) |
| ToCsv   | `"tocsv"`            | `ToCsv<TNode>` (ETL) |

## Function Syntax

**Decision**: Document function calls as `=functionName(arg1, arg2)` string values.

**Rationale**: The `FunctionConverter` parses strings starting with `=` as function calls.
Arguments are comma-separated path expressions or literals.

**Canonical names confirmed** (from `TLio.Client/ParseOptions.cs`):

| Function  | Syntax example |
|-----------|----------------|
| fetch     | `"=fetch($.path)"` |
| indirect  | `"=indirect($.pathToPath)"` |
| promote   | `"=promote($.path)"` |
| partial   | `"=partial($.items[*])"` or `"=partial($.items[*], 1)"` |
| scriptpath | `"=scriptpath()"` or `"=scriptpath(@.child)"` |
| datetime  | `"=datetime()"` or `"=datetime(yyyy-MM-dd)"` |

## Script Format

**Decision**: Document JSON array format as the primary script format.

**Rationale**: `ParseOptions` / `CommandConverter` parse JSON arrays. XML and YAML adapters also
support format-native script variants (XML elements, YAML sequences) but JSON array is universal
and format-agnostic.

Format: `[{ "command": "<name>", "<camelCaseProp>": <value>, ... }]`

JSON property names are camelCase versions of the C# PascalCase property names.

## Property `path` vs `property` semantics

**Decision**: Clearly document the two-argument form in `PropertyChangeCommand` subclasses.

**Rationale**: `Set`, `Add`, `Put` support both:
- Single-arg: `"path"` selects the target node directly.
- Two-arg: `"path"` selects a parent node; `"property"` names the child to set.

**Verified from fixtures**: `{ "command": "set", "path": "$[*]", "property": "active", "value": true }`

## Adapter Factory Methods

**Decision**: Use exact factory class and method names from each adapter project.

**Rationale**: AI agents need the correct factory to instantiate context.

| Adapter | Factory class | Factory method |
|---------|--------------|----------------|
| JSON (Newtonsoft) | `JsonExecutionContext` in `TLio.Json` | `Create(data, script, options)` |
| JSON (System.Text) | `SystemTextJsonExecutionContext` in `TLio.Json.SystemText` | `Create(data, script, options)` |
| XML (slash) | `XmlExecutionContext` in `TLio.Xml` | `CreateWithSlashPaths(data, script)` |
| XML (XPath) | `XmlExecutionContext` in `TLio.Xml` | `CreateWithNativeXPath(data, script)` |
| YAML | `YamlExecutionContext` in `TLio.Yaml` | `Create(data, script, options)` |

## ETL Registration

**Decision**: Document ETL commands as a separate extension pack requiring additional registration.

**Rationale**: ETL commands are not in `ParseOptions.CreateDefault()`. Users must call
`options.CommandsProvider.RegisterETL<TNode>()` to enable them.

## File Naming Convention

**Decision**: Use PascalCase filenames matching the C# class name (without generic parameter).

**Rationale**: Consistent with the C# class names; easy grep from source. E.g., `Set.md`, `Fetch.md`,
`DecisionTable.md`.

Exception: Adapter files use kebab-case to match the format+variant: `json-newtonsoft.md`,
`xml-xpath.md`.

## ArrayMergeMode values

Confirmed from `TLio.Commands/Advanced/Merge.cs` and `CommandConverter.cs`:
- `"concat"` (default) — appends source array to target array
- `"replace"` — replaces target array with source array

## Compare result values

Confirmed from fixtures:
- `"equal"` — nodes have equal scalar values
- `"greater"` — first > second
- `"less"` — first < second
- `"different"` — nodes differ (non-comparable types)
