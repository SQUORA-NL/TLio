# TLio AI Reference

TLio is a data-format-agnostic scripting framework. Scripts are JSON arrays of command objects;
values can be literals or `=function()` expressions. The same script runs against JSON, XML, or
YAML documents by swapping the execution context — no script changes needed.

---

## Overview

### Adapter Selection

| Format | Adapter project | Execution context factory | Path style |
|--------|----------------|--------------------------|------------|
| JSON (Newtonsoft) | `TLio.Json` | `JsonExecutionContext.CreateDefault()` | JSONPath `$.a.b` |
| JSON (System.Text) | `TLio.Json.SystemText` | `SystemTextJsonExecutionContext.CreateDefault()` | JSONPath `$.a.b` (RFC 9535) |
| XML — slash paths | `TLio.Xml` | `XmlExecutionContext.CreateWithSlashPaths()` | `/root/child` |
| XML — XPath | `TLio.Xml` | `XmlExecutionContext.CreateWithNativeXPath()` | `//child`, `item[@id='1']` |
| YAML | `TLio.Yaml` | `YamlExecutionContext.CreateDefault()` | Dot-notation `$.a.b` |

**Choose `TLio.Json`** when: scripts use filter expressions, script expressions `()`, or maximum JSONPath compatibility is needed.  
**Choose `TLio.Json.SystemText`** when: strict RFC 9535 is required or Newtonsoft is excluded.  
**Choose XML slash-path** when: simple `/parent/child` hierarchies with no predicates.  
**Choose XML XPath** when: attribute predicates (`[@id='1']`), recursive descent (`//name`), or positional indexing.  
**Choose YAML** when: source documents are YAML. Multi-document YAML (`---` separator) is parsed as an array root.

### JSONPath: Newtonsoft vs System.Text.Json

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
| Script expressions `$.a[(@.length-1)]` | ✅ | ❌ |
| Negative index `$.a[-1]` | ✅ | ✅ |
| Union `$.a[0,2]` | ✅ | ✅ |

### Script Format

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

### Default Setup

```csharp
var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, JsonExecutionContext.CreateDefault());
```

### ETL Extension Pack

`flatten`, `restore`, `resolve`, `tocsv` are not included in `ParseOptions.CreateDefault()`.
Register explicitly:

```csharp
options.CommandsProvider.RegisterETL<JToken>();
```

### Text Extension Pack

`concat`, `length`, `substring`, `toLower`, `toUpper`, `trim`, `trimStart`, `trimEnd`,
`startswith`, `endswith`, `contains`, `replace`, `split`, `join`, `indexof`, `format`,
`parse`, `padleft`, `padright`, `newguid`, `isempty`, `toString` are in `TLio.Extensions.Text`.
Register explicitly:

```csharp
options.FunctionsProvider.RegisterText<JToken>();
// or JLio-compatible name:
options.FunctionsProvider.RegisterTextPack<JToken>();
```

---

## Adapters

### JSON — Newtonsoft

> JSON adapter using Newtonsoft.Json with Goessner JSONPath. Most permissive JSONPath variant;
> use when scripts require filter expressions or script expressions `()`.

**Node type**: `JToken`

```csharp
using TLio.Json;
using TLio.Client;

var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, JsonExecutionContext.CreateDefault());
```

**Path syntax**:

| Pattern | Example | Matches |
|---------|---------|---------|
| Root | `$` | Document root |
| Child | `$.name` | Direct property |
| Nested | `$.address.city` | Nested property |
| Array index | `$.items[0]` | First element (0-based) |
| Last element | `$.items[-1]` | Last element |
| Wildcard | `$.items[*]` | All array elements |
| Recursive | `$..name` | All `name` at any depth |
| Filter | `$.items[?(@.active == true)]` | Elements where `active` is true |
| Script expr | `$.items[(@.length-1)]` | Dynamic index via expression |
| Slice | `$.items[0:2]` | Elements 0 and 1 |
| Union | `$.items[0,2]` | Elements at index 0 and 2 |

---

### JSON — System.Text.Json

> JSON adapter using System.Text.Json with RFC 9535-compliant JSONPath. Use when Newtonsoft is
> excluded or strict RFC compliance is required. Does **not** support script expressions `()`.

**Node type**: `JsonNode`

```csharp
using TLio.Json.SystemText;
using TLio.Client;

var options = ParseOptions<JsonNode>.CreateDefault();
var engine  = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, SystemTextJsonExecutionContext.CreateDefault());
```

**Path syntax**: identical to JSON (Newtonsoft) except script expressions `()` are not supported.

---

### XML — Slash Paths

> XML adapter using slash-separated paths. Simplest XML path model; use for straightforward
> hierarchies with no predicates.

**Node type**: `XElement`

```csharp
using TLio.Xml;
using TLio.Client;

var options = ParseOptions<XElement>.CreateDefault();
var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, XmlExecutionContext.CreateWithSlashPaths());
```

**Path syntax**:

| Pattern | Example | Matches |
|---------|---------|---------|
| Root | `/` | Document root element |
| Child | `/name` | Direct child element |
| Nested | `/address/city` | Nested element |
| Wildcard | `/items/*` | All children of `items` |

Notes: root is `/`; no predicate support; element names are case-sensitive.

---

### XML — Native XPath

> XML adapter using native XPath. Use when paths need attribute predicates, recursive descent,
> or positional indexing.

**Node type**: `XElement`

```csharp
var result = engine.Execute(scriptJson, data, XmlExecutionContext.CreateWithNativeXPath());
```

**Path syntax**:

| Pattern | Example | Matches |
|---------|---------|---------|
| Root | `.` | Document root element |
| Child | `name` | Direct child element |
| Nested | `address/city` | Nested element |
| Recursive | `//name` | All `name` at any depth |
| Indexed | `items/item[1]` | First `item` (1-based) |
| Attribute predicate | `items/item[@id='1']` | `item` with `id='1'` |
| Wildcard | `*` | All child elements |

Notes: root is `.` (dot), not `/`; XPath indexing is **1-based**.

---

### YAML

> YAML adapter using dot-notation paths. Multi-document YAML (separated by `---`) is parsed as
> an array root.

**Node type**: `YamlNode`

```csharp
using TLio.Yaml;
using TLio.Client;

var options = ParseOptions<YamlNode>.CreateDefault();
var engine  = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, YamlExecutionContext.CreateDefault());
```

**Path syntax**:

| Pattern | Example | Matches |
|---------|---------|---------|
| Root | `$` | Document root |
| Child | `$.name` | Direct key |
| Nested | `$.address.city` | Nested key |
| Array index | `$.items[0]` | First element (0-based) |
| Wildcard | `$.items[*]` | All array elements |

Notes: keys are case-sensitive; no filter expression support; multi-doc → use `$[0]`, `$[1]`, etc.

---

## Commands

### add

> Creates a new property or appends to an array; **skips silently if the property already
> exists**. Use `put` to update existing values, or `set` when the node must already exist.

**Supports functions**: ✅

```json
{ "command": "add", "path": "$.newProp", "value": <TLioValue> }
```

Two-argument form (add child to matched parent):

```json
{ "command": "add", "path": "$.address", "property": "country", "value": "NL" }
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Target path. With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to create on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression. |

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Add(JValue.CreateString("created")).OnPath("$.newField");
```

---

### compare

> Compares two nodes and writes a result string (`"equal"`, `"greater"`, `"less"`, or
> `"different"`) to a target path.

**Supports functions**: ❌

```json
{ "command": "compare", "fromPath": "$.a", "toPath": "$.b", "resultPath": "$.result" }
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Path to first node (left-hand side). Alias: `firstPath`. |
| toPath | string | yes | — | Path to second node (right-hand side). Alias: `secondPath`. |
| resultPath | string | yes | — | Path where result string is written (upsert). |

**Result values**:

| Value | Meaning |
|-------|---------|
| `"equal"` | Both nodes have equal scalar values |
| `"greater"` | First node's value > second node's value |
| `"less"` | First node's value < second node's value |
| `"different"` | Nodes differ and cannot be ordered (type mismatch, objects, arrays) |

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Compare().From("$.score").To("$.threshold").Result("$.verdict");
```

---

### copy

> Copies all nodes matched by `fromPath` to the location(s) specified by `toPath`.
> Source nodes remain. Use `move` to copy-and-delete the source.

**Supports functions**: ❌

```json
{ "command": "copy", "fromPath": "$.source", "toPath": "$.destination" }
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Selects the node(s) to copy. |
| toPath | string | yes | — | Destination path where copies are written. |
| destinationAsArray | boolean | no | false | Aligns multiple results by array index rather than broadcasting. |

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Copy().From("$.original.name").To("$.copy.name");
```

---

### decisionTable

> Matches input values against a rule table and writes output values. Supports `firstMatch`,
> `bestMatch`, and `allMatches` strategies with configurable conflict resolution.

**Supports functions**: ✅ (rule result values only)

```json
{
  "command": "decisionTable",
  "path": "$.record",
  "config": {
    "inputs":  [ { "name": "age",  "path": "$.age" } ],
    "outputs": [ { "name": "tier", "path": "$.tier" } ],
    "rules": [
      { "priority": 1, "conditions": { "age": 18 }, "results": { "tier": "adult" } },
      { "priority": 2, "conditions": { "age": 65 }, "results": { "tier": "senior" } }
    ],
    "strategy": { "mode": "firstMatch", "conflictResolution": "priority" },
    "defaultResults": { "tier": "unknown" }
  }
}
```

**Options**:

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| path | string | yes | Selects the node(s) the table is evaluated against. |
| config | object | yes | Decision table definition (see below). Also accepted as `"decisionTable"` key (JLio alias). |

**Config fields**:

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| inputs | array | yes | — | Array of `{name, path}` — named input columns and their paths. |
| outputs | array | yes | — | Array of `{name, path}` — named output columns and write paths. |
| rules | array | yes | — | Array of rule objects. |
| strategy | object | no | `firstMatch/priority` | Execution strategy. |
| defaultResults | object | no | — | Written when no rule matches. |

**Strategy fields**: `mode` (`"firstMatch"` \| `"bestMatch"` \| `"allMatches"`), `conflictResolution` (`"priority"` \| `"lastWins"` \| `"merge"`).

**Rule object**: `{ priority, conditions: {inputName: value}, results: {outputName: value} }`

Notes: `"decisionTable"` key is accepted as alias for `"config"` (JLio compatibility, 008+).

---

### flatten

> Flattens a nested object to a single-level object with delimiter-separated keys, and
> optionally stores metadata for reconstruction with `restore`.

**Supports functions**: ❌  
**ETL extension** — requires `options.CommandsProvider.RegisterETL<TNode>()`

```json
{ "command": "flatten", "path": "$.nested", "flattenSettings": { "delimiter": "_" } }
```

**Options**:

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| path | string | yes | Selects the object node(s) to flatten. |
| flattenSettings | object | no | Flattening configuration (see below). |

**flattenSettings fields**:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| delimiter | string | `"."` | Key separator between levels. |
| maxDepth | integer | unlimited | Maximum nesting depth to flatten. |
| excludePaths | array | — | Dot-paths to exclude from flattening. |
| metadataPath | string | — | Where to write flattening metadata (enables `restore`). |
| includeArrayIndices | boolean | false | Include array indices in flattened keys. |
| preserveTypes | boolean | false | Preserve type information alongside values. |

---

### ifElse

> Evaluates a condition and executes one of two script branches. The condition can be a
> literal boolean, a value path, or a `=function()` expression that returns a truthy node.

**Supports functions**: ✅ (condition only)

```json
{
  "command": "ifElse",
  "condition": <TLioValue>,
  "ifScript":   [ <commands> ],
  "elseScript": [ <commands> ]
}
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| condition | TLioValue | yes | — | Evaluated for truthiness. Literal, path, or `=function()`. |
| ifScript | array | yes | — | Script executed when condition is truthy. |
| elseScript | array | no | — | Script executed when condition is falsy. |

**Example**:

```json
{
  "command": "ifElse",
  "condition": "=fetch($.user.active)",
  "ifScript":   [{ "command": "set", "path": "$.status", "value": "enabled" }],
  "elseScript": [{ "command": "set", "path": "$.status", "value": "disabled" }]
}
```

**C# Fluent API**:

```csharp
var condition = new FixedValue<JToken>(JValue.FromObject(true));
var script = new TLioScript<JToken>()
    .IfElse(condition)
    .If(new TLioScript<JToken>().Set(JValue.CreateString("enabled")).OnPath("$.status").Build())
    .Else(new TLioScript<JToken>().Set(JValue.CreateString("disabled")).OnPath("$.status").Build());
```

---

### merge

> Deep-merges the node(s) at `fromPath` (source) into the node(s) at `toPath` (destination).
> Objects are merged recursively; arrays follow `arrayMergeMode`.

**Supports functions**: ❌

```json
{ "command": "merge", "fromPath": "$.source", "toPath": "$.target" }
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Selects the source node(s). Alias: `path`. |
| toPath | string | yes | — | Selects the destination node(s). Alias: `targetPath`. |
| arrayMergeMode | string | no | `"concat"` | `"concat"` appends; `"replace"` overwrites. |

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Merge().From("$.patch").To("$.document");
```

---

### move

> Moves nodes from `fromPath` to `toPath` — equivalent to `copy` followed by `remove`
> on the source. Source nodes are deleted after the copy succeeds.

**Supports functions**: ❌

```json
{ "command": "move", "fromPath": "$.oldLocation", "toPath": "$.newLocation" }
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Selects the node(s) to move. |
| toPath | string | yes | — | Destination path where nodes are written. |
| destinationAsArray | boolean | no | false | Aligns multiple results by array index. |

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Move().From("$.draft.title").To("$.published.title");
```

---

### put

> **Upsert**: sets the value if the node already exists, creates it if absent.
> Use `set` when the node must pre-exist, or `add` to create-only.

**Supports functions**: ✅

```json
{ "command": "put", "path": "$.field", "value": <TLioValue> }
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects target node(s). With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to upsert on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression. |

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Put(JValue.CreateString("active")).OnPath("$.status");
```

---

### remove

> Removes all nodes matched by the path expression. Logs a warning if no nodes match;
> does not error.

**Supports functions**: ❌

```json
{ "command": "remove", "path": "$.fieldToDelete" }
```

**Options**:

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| path | string | yes | Selects the node(s) to remove. Wildcards remove multiple nodes. |

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Remove().OnPath("$.tempId");
```

---

### resolve

> Looks up matching entries in a reference collection and writes derived values to
> the target document. Supports relative `@.property` paths for writing results.

**Supports functions**: ❌  
**ETL extension** — requires `options.CommandsProvider.RegisterETL<TNode>()`

```json
{
  "command": "resolve",
  "path": "$.orders[*]",
  "settings": [
    {
      "referencesCollectionPath": "$.products",
      "resolveKeys": [
        { "keyPath": "@.productId", "referenceKeyPath": "$.id" }
      ],
      "values": [
        { "targetPath": "@.productName", "value": "=fetch($.name)" }
      ]
    }
  ]
}
```

**Options**:

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| path | string | yes | Selects the nodes to resolve (left side). |
| settings | array | yes | Array of resolve setting objects (see below). |

**Resolve setting fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| referencesCollectionPath | string | yes | Path to the reference collection. |
| resolveKeys | array | yes | Array of `{keyPath, referenceKeyPath}` join conditions. |
| values | array | yes | Array of `{targetPath, value}` to write on match. |

`keyPath` and `targetPath` use `@.property` (relative to current node).

---

### restore

> Reconstructs a nested object from data previously flattened by `flatten`. Uses stored
> metadata when available; falls back to delimiter-based inference in non-strict mode.

**Supports functions**: ❌  
**ETL extension** — requires `options.CommandsProvider.RegisterETL<TNode>()`

```json
{ "command": "restore", "path": "$", "restoreSettings": { "metadataPath": "$.meta", "removeMetadata": true } }
```

**Options**:

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| path | string | yes | Selects the flattened object node(s) to restore. |
| restoreSettings | object | no | Restoration configuration (see below). |

**restoreSettings fields**:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| delimiter | string | `"."` | Key separator used during the original `flatten`. |
| metadataPath | string | — | Path to the metadata written by `flatten`. |
| metadataKey | string | — | Key name where metadata is embedded in the flattened object. |
| strictMode | boolean | false | Fail if metadata is absent. |
| removeMetadata | boolean | false | Delete the metadata node after restoration. |

---

### set

> Sets the value of an **existing** node; logs a warning and skips if the node is absent.
> Use `put` for upsert (create-or-update) or `add` to create-only.

**Supports functions**: ✅

```json
{ "command": "set", "path": "$.target", "value": <TLioValue> }
```

Two-argument form (select parent, name child property):

```json
{ "command": "set", "path": "$.items[*]", "property": "active", "value": true }
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the target node(s). With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to set on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression. |

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Set(JValue.CreateString("Amsterdam")).OnPath("$.address.city");
```

---

### tocsv

> Converts an object or array of objects to a CSV-formatted string and writes it to
> the target node.

**Supports functions**: ❌  
**ETL extension** — requires `options.CommandsProvider.RegisterETL<TNode>()`

```json
{ "command": "tocsv", "path": "$.records", "csvSettings": { "delimiter": ",", "includeHeaders": true } }
```

**Options**:

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| path | string | yes | Selects the node(s) to convert. |
| csvSettings | object | no | CSV formatting configuration (see below). |

**csvSettings fields**:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| delimiter | string | `","` | Column separator. |
| includeHeaders | boolean | true | Emit a header row. |
| booleanFormat | string | `"true/false"` | How booleans render (e.g. `"1/0"`). |
| nullValueRepresentation | string | `""` | String for null values. |
| quoteAllFields | boolean | false | Force-quote every field. |
| escapeQuoteChar | string | `"\""` | Character used to escape inner quotes. |

---

## Functions

Functions are called in `"value"` fields with the `=` prefix: `"value": "=functionName(args)"`.  
All built-in functions are registered by `ParseOptions.CreateDefault()`. Text-pack functions require `RegisterText<TNode>()`.

### concat

> Concatenates two or more string arguments into a single string.

**Syntax**: `=concat(a, b)` or `=concat(a, b, c, ...)`  
**Pack**: Text (requires `RegisterText<TNode>()`)

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | First string segment. |
| 2 | string or path | yes | Second string segment. |
| 3+ | string or path | no | Additional segments (variadic). |

**Example**:

```json
{ "command": "add", "path": "$.full", "value": "=concat($.first,' ',$.last)" }
```

Input: `{ "first": "Alice", "last": "Smith" }` → `"full": "Alice Smith"`

---

### datetime

> Returns the **current UTC date/time** as a formatted string.

**Syntax**: `=datetime()` or `=datetime(format)`  
**Pack**: built-in

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (.NET format) | no | Default: `yyyy-MM-ddTHH:mm:ss.fffZ` |

**Common formats**:

| Format | Example output |
|--------|----------------|
| (default) | `"2026-04-06T14:30:00.000Z"` |
| `yyyy-MM-dd` | `"2026-04-06"` |
| `dd/MM/yyyy` | `"06/04/2026"` |
| `yyyyMMddHHmmss` | `"20260406143000"` |

**Example**: `{ "command": "put", "path": "$.createdAt", "value": "=datetime()" }`

```csharp
engine.Execute("[{\"command\":\"put\",\"path\":\"$.createdAt\",\"value\":\"=datetime()\"}]",
    JObject.Parse("{}"), JsonExecutionContext.CreateDefault());
```

---

### fetch

> Evaluates a path expression and returns the **first matched node's value**. Returns an
> optional default value when the path matches nothing.

**Syntax**: `=fetch(path)` or `=fetch(path, defaultValue)`  
**Pack**: built-in

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path selecting the source node. |
| 2 | any | no | Default value when path resolves to nothing. |

**Returns**: first matched node, or default if provided; logs warning and fails if no match and no default.

**Examples**:

```json
{ "command": "set",  "path": "$.target", "value": "=fetch($.source)" }
{ "command": "add",  "path": "$.name",   "value": "=fetch($.user.name,'Anonymous')" }
```

```csharp
engine.Execute("[{\"command\":\"add\",\"path\":\"$.name\",\"value\":\"=fetch($.user.name,'Anonymous')\"}]",
    JObject.Parse("{}"), JsonExecutionContext.CreateDefault());
```

---

### format

> Replaces `{0}`, `{1}`, … placeholders in a template string with the supplied argument values.

**Syntax**: `=format(template, value0)` or `=format(template, value0, value1, ...)`  
**Pack**: Text (requires `RegisterText<TNode>()`)

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | Template with `{0}`, `{1}`, … placeholders. |
| 2+ | any | yes (min 1) | Replacement values. |

**Example**:

```json
{ "command": "add", "path": "$.greeting", "value": "=format('Hello, {0}!', $.name)" }
```

Input: `{ "name": "Alice" }` → `"greeting": "Hello, Alice!"`

---

### indirect

> Two-step path resolution: reads a **string value** at the given path, then uses that
> string as a second path expression to retrieve the final value.

**Syntax**: `=indirect(pathToPath)`  
**Pack**: built-in

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path to a node whose **string content** is itself a path. |

**Example**:

Given `{ "pathRef": "$.source", "source": "hello" }`:

```json
{ "command": "set", "path": "$.target", "value": "=indirect($.pathRef)" }
```

Result: `$.target` = `"hello"` (resolved via `$.pathRef` → `"$.source"` → `"hello"`)

```csharp
engine.Execute("[{\"command\":\"set\",\"path\":\"$.target\",\"value\":\"=indirect($.pathRef)\"}]",
    JObject.Parse("{\"pathRef\":\"$.source\",\"source\":\"hello\"}"),
    JsonExecutionContext.CreateDefault());
```

---

### length

> Returns the number of characters in a string.

**Syntax**: `=length(str)`  
**Pack**: Text (requires `RegisterText<TNode>()`)

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to measure. |

**Returns**: numeric node with character count (0 for null/empty).

**Example**: `{ "command": "add", "path": "$.len", "value": "=length($.name)" }`

Input: `{ "name": "Alice" }` → `"len": 5`

---

### newGuid

> Generates a new random UUID string each time it is evaluated.

**Syntax**: `=newGuid()`  
**Pack**: built-in (camelCase) + Text pack (`"newguid"` lowercase alias)

No arguments.

**Example**:

```json
{ "command": "add", "path": "$.id", "value": "=newGuid()" }
```

Output: `{ "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" }` (any valid UUID v4)

Notes: UUID is generated at execution time; `"newguid"` (lowercase) is registered by `RegisterText<TNode>()`.

---

### parse

> Parses a JSON string into a structured node (object, array, or primitive).

**Syntax**: `=parse(str)`  
**Pack**: Text (requires `RegisterText<TNode>()`)

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | A valid JSON string to parse. |

**Example**:

```json
{ "command": "add", "path": "$.obj", "value": "=parse($.jsonString)" }
```

Input: `{ "jsonString": "{\"name\":\"Alice\"}" }` → `"obj": { "name": "Alice" }`

Complement of `=toString()`. Fails if the argument is not valid JSON.

---

### partial

> Selects one element from a **multi-match path expression** by zero-based index.

**Syntax**: `=partial(path)` or `=partial(path, index)`  
**Pack**: built-in

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path expected to match multiple nodes. |
| 2 | integer | no | Zero-based index. Defaults to `0`. |

**Example**:

```json
{ "command": "set", "path": "$.pick", "value": "=partial($.items[*], 2)" }
```

Given `{ "items": ["first","second","third"] }` → `"pick": "third"`

```csharp
engine.Execute("[{\"command\":\"set\",\"path\":\"$.pick\",\"value\":\"=partial($.items[*],1)\"}]",
    JObject.Parse("{\"items\":[\"first\",\"second\",\"third\"]}"),
    JsonExecutionContext.CreateDefault());
```

---

### path

> Returns the current node's absolute path as a string. Alias for `=scriptpath()`.

**Syntax**: `=path()`  
**Pack**: built-in (alias registered in `ParseOptions.CreateDefault()`)

No arguments. Identical behaviour to `=scriptpath()`.

**Example**:

```json
{ "command": "add", "path": "$.items[*].loc", "value": "=path()" }
```

Input: `{ "items": [{"id":1},{"id":2}] }` → each item gets `"loc": "$.items[0]"` / `"$.items[1]"`

Notes: `=path()` and `=scriptpath()` share the same implementation (`ScriptPath<TNode>`). Use `=path()` for JLio compatibility.

---

### promote

> Wraps the matched node in a new object using either the node's own **property name**
> or an **explicit name** as the key.

**Syntax**: `=promote(path)` or `=promote(path, propertyName)`  
**Pack**: built-in

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path to the node to promote. |
| 2 | string | no | Explicit key name. When omitted, uses the node's parent property name. |

**Examples**:

```json
{ "command": "set", "path": "$.result", "value": "=promote($.person)" }
```

Given `{ "person": {"name":"Alice"} }` → `$.result` = `{ "person": {"name":"Alice"} }`

```json
{ "command": "add", "path": "$.wrapped", "value": "=promote($.rawValue,'data')" }
```

Given `{ "rawValue": 42 }` → `$.wrapped` = `{ "data": 42 }`

```csharp
engine.Execute("[{\"command\":\"add\",\"path\":\"$.wrapped\",\"value\":\"=promote($.rawValue,'data')\"}]",
    JObject.Parse("{\"rawValue\":42}"), JsonExecutionContext.CreateDefault());
```

---

### replace

> Replaces all occurrences of a substring within a string.

**Syntax**: `=replace(str, old, new)`  
**Pack**: Text (requires `RegisterText<TNode>()`)

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. |
| 2 | string or path | yes | The substring to find (case-sensitive). |
| 3 | string or path | yes | The replacement string. |

**Example**:

```json
{ "command": "set", "path": "$.code", "value": "=replace($.code, '-', '_')" }
```

Input: `{ "code": "my-value-key" }` → `"code": "my_value_key"`

---

### scriptpath

> Returns the **absolute path** of the currently executing node as a string.

**Syntax**: `=scriptpath()` or `=scriptpath(@.child)`  
**Pack**: built-in. Also registered as `"path"` (alias, 008+).

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (relative path) | no | Relative path starting with `@`. |

**Example**:

```json
{ "command": "set", "path": "$.items[0].selfPath", "value": "=scriptpath()" }
```

Result: `$.items[0].selfPath` = `"$.items[0]"`

Notes: `=path()` and `=scriptpath()` are identical at runtime.

---

### substring

> Extracts a portion of a string starting at a given index.

**Syntax**: `=substring(str, start)` or `=substring(str, start, count)`  
**Pack**: Text (requires `RegisterText<TNode>()`)

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The source string. |
| 2 | number | yes | Zero-based start index. |
| 3 | number | no | Maximum number of characters to extract. |

**Example**:

```json
{ "command": "add", "path": "$.abbr", "value": "=substring($.name, 0, 3)" }
```

Input: `{ "name": "Alice" }` → `"abbr": "Ali"`

---

### toLower

> Converts a string to lowercase.

**Syntax**: `=toLower(str)`  
**Pack**: Text. Registered as both `"toLower"` (camelCase, 008+) and `"tolower"` (legacy).

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to convert. |

**Example**: `{ "command": "add", "path": "$.lower", "value": "=toLower($.name)" }`

Input: `{ "name": "Alice" }` → `"lower": "alice"`

---

### toString

> Converts any node to its string representation.

**Syntax**: `=toString(node)`  
**Pack**: Text (requires `RegisterText<TNode>()`). Registered as `"toString"` (camelCase).

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any | yes | The node to convert. |

**Returns**:
- Object / array → compact JSON (e.g. `{"a":1,"b":2}`)
- String → unchanged
- Number / boolean → value as string
- Null → `""`

**Example**: `{ "command": "add", "path": "$.str", "value": "=toString($.obj)" }`

Input: `{ "obj": {"a":1,"b":2} }` → `"str": "{\"a\":1,\"b\":2}"`

Notes: `FunctionName` must be overridden explicitly because the CLR type name `ToStringFunction` would otherwise produce `"tostringfunction"`.

---

### toUpper

> Converts a string to uppercase.

**Syntax**: `=toUpper(str)`  
**Pack**: Text. Registered as both `"toUpper"` (camelCase, 008+) and `"toupper"` (legacy).

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to convert. |

**Example**: `{ "command": "add", "path": "$.upper", "value": "=toUpper($.name)" }`

Input: `{ "name": "Alice" }` → `"upper": "ALICE"`

---

### trim

> Removes leading and trailing whitespace from a string.

**Syntax**: `=trim(str)`  
**Pack**: Text (requires `RegisterText<TNode>()`)

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to trim. |

**Example**: `{ "command": "set", "path": "$.name", "value": "=trim($.name)" }`

Input: `{ "name": "  Alice  " }` → `"name": "Alice"`

---

### trimEnd

> Removes trailing (right-side) whitespace from a string.

**Syntax**: `=trimEnd(str)`  
**Pack**: Text. Registered as both `"trimEnd"` (camelCase, 008+) and `"trimend"` (legacy).

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to right-trim. |

**Example**: `{ "command": "set", "path": "$.name", "value": "=trimEnd($.name)" }`

Input: `{ "name": "Alice  " }` → `"name": "Alice"`

---

### trimStart

> Removes leading (left-side) whitespace from a string.

**Syntax**: `=trimStart(str)`  
**Pack**: Text. Registered as both `"trimStart"` (camelCase, 008+) and `"trimstart"` (legacy).

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to left-trim. |

**Example**: `{ "command": "set", "path": "$.name", "value": "=trimStart($.name)" }`

Input: `{ "name": "  Alice" }` → `"name": "Alice"`

---

## Quick Reference

### Command Summary

| Command | Supports functions | Fluent | Pack |
|---------|-------------------|--------|------|
| add | ✅ | `.Add(v).OnPath(p)` | built-in |
| compare | ❌ | `.Compare().From(a).To(b).Result(r)` | built-in |
| copy | ❌ | `.Copy().From(a).To(b)` | built-in |
| decisionTable | ✅ (results only) | — | built-in |
| flatten | ❌ | — | ETL |
| ifElse | ✅ (condition only) | `.IfElse(c).If(...).Else(...)` | built-in |
| merge | ❌ | `.Merge().From(a).To(b)` | built-in |
| move | ❌ | `.Move().From(a).To(b)` | built-in |
| put | ✅ | `.Put(v).OnPath(p)` | built-in |
| remove | ❌ | `.Remove().OnPath(p)` | built-in |
| resolve | ❌ | — | ETL |
| restore | ❌ | — | ETL |
| set | ✅ | `.Set(v).OnPath(p)` | built-in |
| tocsv | ❌ | — | ETL |

### Function Summary

| Function | Syntax | Pack |
|----------|--------|------|
| concat | `=concat(a,b,...)` | Text |
| datetime | `=datetime()` / `=datetime(fmt)` | built-in |
| fetch | `=fetch(path)` / `=fetch(path,default)` | built-in |
| format | `=format(tpl,v0,...)` | Text |
| indirect | `=indirect(pathToPath)` | built-in |
| length | `=length(str)` | Text |
| newGuid | `=newGuid()` | built-in / Text (`newguid`) |
| parse | `=parse(str)` | Text |
| partial | `=partial(path)` / `=partial(path,i)` | built-in |
| path | `=path()` | built-in (alias of scriptpath) |
| promote | `=promote(path)` / `=promote(path,name)` | built-in |
| replace | `=replace(str,old,new)` | Text |
| scriptpath | `=scriptpath()` / `=scriptpath(@.child)` | built-in |
| substring | `=substring(str,start)` / `=substring(str,start,n)` | Text |
| toLower | `=toLower(str)` | Text |
| toString | `=toString(node)` | Text |
| toUpper | `=toUpper(str)` | Text |
| trim | `=trim(str)` | Text |
| trimEnd | `=trimEnd(str)` | Text |
| trimStart | `=trimStart(str)` | Text |
