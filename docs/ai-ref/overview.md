# TLio AI Reference — Overview

TLio is a data-format-agnostic scripting framework. A script is a JSON array of command
objects. Each command reads and/or writes nodes in a document via path expressions. The
same script runs against JSON, XML, or YAML by swapping the execution context — no script
changes needed.

---

## Execution Model

```
script (JSON array)  +  document (JSON / XML / YAML)  +  execution context
        |
        v
  ScriptEngine<TNode>.Execute(scriptJson, data, context)
        |
        v
  ExecutionResult  →  trace log  +  mutated document
```

1. The engine iterates the command array in order.
2. Each command resolves its `"path"` (or `"fromPath"` / `"toPath"`) against the document.
3. Values can be literals or `=function()` expressions — evaluated at write time.
4. Each command appends one trace entry: `"success"`, `"noop"`, or `"failure"`.

---

## Adapter Selection

| Format | Adapter project | Execution context factory | Path style | When to use |
|--------|----------------|--------------------------|------------|-------------|
| JSON (Newtonsoft) | `TLio.Json` | `JsonExecutionContext.CreateDefault()` | JSONPath `$.a.b` | Default JSON choice; Goessner JSONPath; filter and script expressions |
| JSON (System.Text) | `TLio.Json.SystemText` | `SystemTextJsonExecutionContext.CreateDefault()` | JSONPath `$.a.b` (RFC 9535) | RFC 9535 strict; Newtonsoft excluded; no script expressions `()` |
| XML — slash paths | `TLio.Xml` | `XmlExecutionContext.CreateWithSlashPaths()` | `/order/customer` | Simple hierarchies; no predicates needed |
| XML — XPath | `TLio.Xml` | `XmlExecutionContext.CreateWithNativeXPath()` | `/order/customer`, `//child`, `/order/item[@id='1']` | Predicates, recursive descent, axes; indexing is 1-based |
| YAML | `TLio.Yaml` | `YamlExecutionContext.CreateDefault()` | Dot-notation `$.a.b` | YAML source documents; multi-doc `---` parsed as array root |

Full adapter details, path-syntax tables, and "When NOT to use" guidance:
[TLio_AI_Reference.md — Adapters](TLio_AI_Reference.md#adapters)

---

## Path Notation Basics

| Adapter | Root | Child | Nested | Array index | Wildcard |
|---------|------|-------|--------|-------------|----------|
| JSON (both) | `$` | `$.name` | `$.a.b` | `$.a[0]` | `$.a[*]` |
| XML slash-path | `/` | `/name` | `/a/b` | — | `/a/*` |
| XML XPath | `.` | `name` | `a/b` | `a/b[1]` (1-based) | `*` |
| YAML | `$` | `$.name` | `$.a.b` | `$.a[0]` | `$.a[*]` |

---

## Function Value Syntax

Any `"value"` field can be a function expression by prefixing with `=`:

```json
{ "command": "set", "path": "$.target", "value": "=fetch($.source)" }
{ "command": "put", "path": "$.id",     "value": "=newGuid()" }
{ "command": "add", "path": "$.full",   "value": "=concat($.first,' ',$.last)" }
```

Rules:
- Functions are strings — NOT objects. `"value": "=fetch($.x)"` is correct;
  `"value": {"fn": "fetch"}` is wrong.
- The `=` prefix triggers evaluation. To write a literal string starting with `=`,
  escape it: `"==formula"` → writes the string `"=formula"`.
- Path arguments starting with `$` or `@` inside a function call are resolved against
  the document ROOT (`$`), not the current element.

---

## Trace Outcomes

Every command produces one trace entry. An agent should check the outcome before
assuming the document was changed.

| Outcome | Meaning | Agent action |
|---------|---------|--------------|
| `"success"` | Command executed and changed the document | Continue |
| `"noop"` | Command ran but found nothing to change — path not found, already-exists skip, or no matches | Investigate path or precondition — NOT an error |
| `"failure"` | Command could not execute — validation error, function path-not-found, wrong type | Fix the script or input data |

A `"noop"` is not an error. It is the normal signal for a precondition mismatch
(e.g. `add` on a field that already exists, `set` on a field that does not exist).

---

## Decision Trees

For one-step navigation to the right tool, see the decision trees in the full reference:

- **Command picker** — which command for which write / transfer / logic / ETL goal:
  [TLio_AI_Reference.md — Choosing the Right Command](TLio_AI_Reference.md#choosing-the-right-command)

- **Function picker** — which function for string, numeric, date, or path operations:
  [TLio_AI_Reference.md — Choosing the Right Function](TLio_AI_Reference.md#choosing-the-right-function)

---

## Quick Setup

### Built-in commands and functions (JSON / Newtonsoft)

```csharp
using TLio.Json;
using TLio.Client;

var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, JsonExecutionContext.CreateDefault());
```

### Register extension packs

```csharp
// ETL commands: flatten, restore, resolve, tocsv
options.CommandsProvider.RegisterETL<JToken>();

// Text functions: concat, format, substring, toLower, toUpper, trim, split, join,
//   contains, startsWith, endsWith, indexOf, isEmpty, replace, parse, toString,
//   toFixed, newguid, padleft, padright, length
options.FunctionsProvider.RegisterText<JToken>();
```

Predicates for `ifElse` / `decisionTable` conditions — equals, notEquals, greaterThan,
greaterOrEqual, lessThan, lessOrEqual, and, or, not, exists, isNull, isString, isNumber,
isBoolean, isArray, isObject, in, matches — are built in and need no registration.

### Minimal script example

```json
[
  { "command": "put",    "path": "$.status",    "value": "active" },
  { "command": "add",    "path": "$.createdAt", "value": "=datetime()" },
  { "command": "remove", "path": "$.tempId" },
  { "command": "rename", "path": "$.oldName",   "name": "newName" }
]
```

---

## Critical Rules (summary)

Full details in [TLio_AI_Reference.md — Critical Rules Every Agent Must Know](TLio_AI_Reference.md#critical-rules-every-agent-must-know).

1. **add / set / put**: `add` = create-only (noop if exists); `set` = update-only (noop if absent, and it never creates the path); `put` = upsert (always writes, creating the path). When uncertain, use `put`.
1b. **Changing a name, not a value, is `rename`** — it keeps position and XML attributes, and is the only way to rename an XML document element.
1c. **XML paths start at the document node**: `/order/customer`, never `/customer`. See [xml-xpath.md](adapters/xml-xpath.md).
2. **Function path resolution uses ROOT**: inside a function call, paths resolve against `$`, not the current array element.
3. **dateCompare returns long** (`-1` / `0` / `1`), never a string.
4. **decisionTable results must be plain primitives** — not objects.
5. **ifElse condition must be a primitive** — `true`, `false`, or `"=function()"` — not an object.
