# TLio AI Reference — Overview

TLio is a data-format-agnostic scripting framework. Scripts are JSON arrays of command
objects; values can be literals or `=function()` expressions. The same script runs against
JSON, XML, or YAML documents by swapping the execution context — no script changes needed.

## Adapter Selection

| Format | Adapter project | Execution context factory | Path style |
|--------|----------------|--------------------------|------------|
| JSON (Newtonsoft) | `TLio.Json` | `JsonExecutionContext.Create(data, script, options)` | JSONPath `$.a.b` |
| JSON (System.Text) | `TLio.Json.SystemText` | `SystemTextJsonExecutionContext.Create(data, script, options)` | JSONPath `$.a.b` (RFC 9535) |
| XML — slash paths | `TLio.Xml` | `XmlExecutionContext.CreateWithSlashPaths(data, script)` | `/root/child` |
| XML — XPath | `TLio.Xml` | `XmlExecutionContext.CreateWithNativeXPath(data, script)` | `//child`, `item[@id='1']` |
| YAML | `TLio.Yaml` | `YamlExecutionContext.Create(data, script, options)` | Dot-notation `$.a.b` |

**Choose `TLio.Json` (Newtonsoft)** when: scripts use filter expressions, script expressions
`()`, or you need maximum JSONPath compatibility.

**Choose `TLio.Json.SystemText`** when: strict RFC 9535 required or Newtonsoft is excluded
from your dependency constraints.

**Choose XML slash-path** when: paths are simple `/parent/child` hierarchies with no
predicates needed.

**Choose XML XPath** when: paths need attribute predicates (`[@id='1']`), recursive descent
(`//name`), or positional indexing (`item[1]`).

**Choose YAML** when: source documents are YAML. Multi-document YAML (`---` separator)
is parsed as an array root.

## JSONPath: Newtonsoft vs System.Text.Json

| Feature | Newtonsoft (`TLio.Json`) | System.Text.Json (`TLio.Json.SystemText`) |
|---------|--------------------------|------------------------------------------|
| Spec basis | Goessner (informal) | RFC 9535 |
| Root `$` | ✅ | ✅ |
| Child `$.name` | ✅ | ✅ |
| Nested `$.a.b.c` | ✅ | ✅ |
| Array index `$.a[0]` | ✅ | ✅ |
| Wildcard `$.a[*]` | ✅ | ✅ |
| Recursive descent `$..name` | ✅ | ✅ |
| Slice `$.a[0:2]` | ✅ | ✅ |
| Filter `$.a[?(@.x > 1)]` | ✅ | ✅ |
| Script expressions `$.a[(@.length-1)]` | ✅ | ❌ not supported |
| Negative index `$.a[-1]` | ✅ | ✅ |
| Union `$.a[0,2]` | ✅ | ✅ |

## Script Format

A TLio script is a JSON array of command objects. The `"command"` field is the discriminator.
All other property names are camelCase.

```json
[
  { "command": "set",    "path": "$.name",    "value": "Alice" },
  { "command": "add",    "path": "$.tags",    "value": ["admin"] },
  { "command": "remove", "path": "$.tempId" }
]
```

Function calls are string values prefixed with `=`:

```json
{ "command": "set", "path": "$.target", "value": "=fetch($.source)" }
```

The script is executed by `ScriptEngine<TNode>.Execute(scriptJson, data, context)`.

### Default setup (built-in commands + functions)

```csharp
var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, context);
```

## ETL Extension Pack

The four ETL commands (`flatten`, `restore`, `resolve`, `tocsv`) are not included in
`ParseOptions.CreateDefault()`. Register them explicitly:

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.CommandsProvider.RegisterETL<JToken>();
```

See [commands/Flatten.md](commands/Flatten.md), [commands/Restore.md](commands/Restore.md),
[commands/Resolve.md](commands/Resolve.md), [commands/ToCsv.md](commands/ToCsv.md).

## Escape Sequences

### Value escapes (FunctionConverter)

When a script value starts with a trigger character (`@`, `$`, `=`), double the first
character to produce a literal string instead of triggering path or function parsing.

| Want to write | Script value | Result type |
|---------------|-------------|-------------|
| Literal `@admin` | `"@@admin"` | `FixedValue("@admin")` |
| Literal `$ref` | `"$$ref"` | `FixedValue("$ref")` |
| Literal `=formula` | `"==formula"` | `FixedValue("=formula")` |
| Literal `@` inside a quoted string | `"'user@@example.com'"` | `FixedValue("user@example.com")` |

The same rules apply inside function arguments: `=concat('@@prefix', @$.name)` passes
`@prefix` as the first argument and evaluates `@$.name` as a path for the second.

### Path escapes (bracket notation)

When a property name contains the path delimiter (`.`) or other special characters,
use bracket-quoted notation instead of dot-notation.

| Adapter | Normal path | Bracket-notation path |
|---------|------------|----------------------|
| JSON (Newtonsoft / SystemText) | `$.version` | `$['version.major']` |
| YAML | `$.server` | `$['server.host']` |
| XML XPath | `item/name` | Use XPath predicates: `item[@id='1']` |
| XML slash-path | `/root/child` | URL-encode `/` in segment if needed |

**Note**: Bracket-notation reading is fully supported across all adapters.
Writing to a bracket-notation path via `set`/`add` requires `SplitParentAndLeaf`
to handle quoted leaf names — tracked as follow-up issue 010-bracket-write.
