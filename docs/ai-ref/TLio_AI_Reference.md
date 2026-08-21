# TLio AI Reference

TLio is a data-format-agnostic scripting framework. Scripts are JSON arrays of command objects;
values can be literals or `=function()` expressions. The same script runs against JSON, XML, or
YAML documents by swapping the execution context — no script changes needed.

---

## Adapter Selection

| Format | Adapter project | Execution context factory | Path style | When to use | When NOT to use |
|--------|----------------|--------------------------|------------|-------------|-----------------|
| JSON (Newtonsoft) | `TLio.Json` | `JsonExecutionContext.CreateDefault()` | JSONPath `$.a.b` | Default JSON choice; scripts with filter or script expressions `()`, Goessner JSONPath features | When strict RFC 9535 compliance is required |
| JSON (System.Text) | `TLio.Json.SystemText` | `SystemTextJsonExecutionContext.CreateDefault()` | JSONPath `$.a.b` (RFC 9535) | RFC 9535 strict compliance; Newtonsoft excluded from dependencies | When scripts use script expressions `()` — not supported |
| XML — slash paths | `TLio.Xml` | `XmlExecutionContext.CreateWithSlashPaths()` | `/order/customer` | Simple hierarchical XML with no predicates | When XPath predicates, `//` recursive descent, or positional indexing is needed |
| XML — XPath | `TLio.Xml` | `XmlExecutionContext.CreateWithNativeXPath()` | `//child`, `/order/item[@id='1']` | Full XPath 1.0: predicates, axes, recursive descent | When simple slash paths are sufficient — prefer slash-path for simplicity |
| YAML | `TLio.Yaml` | `YamlExecutionContext.CreateDefault()` | Dot-notation `$.a.b` | YAML source documents; multi-doc YAML (`---`) parsed as array root | When you need array-index path syntax identical to JSON filter expressions |

**XML path note**: both XML modes anchor on the document node, so the document element is
part of every path — `/order/customer`, never `/customer`. XPath indexing is **1-based**
(`item[1]` = first item, not `item[0]`).

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

---

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

Only the outermost call needs the `=`. Nested calls may carry it or not — these are the same:

```json
{ "value": "=concat(fetch($.first), ' ', fetch($.last))" }
{ "value": "=concat(=fetch($.first), ' ', =fetch($.last))" }
```

See [notation-reference.md](notation-reference.md) §3a.

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

`concat`, `length`, `substring`, `right`, `toLower`, `toUpper`, `trim`, `trimStart`, `trimEnd`,
`startswith`, `endswith`, `contains`, `replace`, `regexreplace`, `regexextract`, `split`,
`join`, `indexof`, `format`, `parse`, `padleft`, `padright`, `newguid`, `isempty`, `toFixed`,
`toString` are in `TLio.Extensions.Text`. Register explicitly:

```csharp
options.FunctionsProvider.RegisterText<JToken>();
// or JLio-compatible name:
options.FunctionsProvider.RegisterTextPack<JToken>();
```

### Math and TimeDate Extension Packs

Neither is in `CreateDefault()` either. `multiply`, `divide`, `clamp` and `sign` are in the Math
pack; `dateDiff`, `dateAdd`, `datePart`, `formatDate`, `parseDate`, `startOfMonth` and
`endOfMonth` are in the TimeDate pack.

```csharp
options.FunctionsProvider.RegisterMath<JToken>();
options.FunctionsProvider.RegisterTimeDate<JToken>();
```

`if`, `coalesce`, `between`, `distinct`, `sort`, `sortBy` and `last` need no registration —
they are built in, like the other predicates and path functions.

---

## Choosing the Right Command

Use this decision tree to pick the command in one step.

### Property write (create / update / upsert)

| Situation | Command | Behaviour |
|-----------|---------|-----------|
| Field must NOT already exist (create-only) | `add` | Skips silently if field is present — never overwrites |
| Field MUST already exist (update-only) | `set` | Logs warning and skips if field is absent — never creates, and never scaffolds the parent path |
| Don't know / don't care (safe default) | `put` | Upsert: creates if absent, updates if present |

### Node transfer and deletion

| Goal | Command |
|------|---------|
| Copy a node, keep the source | `copy` |
| Move a node, delete the source | `move` |
| Delete a node entirely | `remove` |
| Change a node's **name**, keeping value, children and position | `rename` |
| Rename the XML document element | `rename` (nothing else can — the root has no parent) |

### Conditional logic

| Situation | Command |
|-----------|---------|
| Two outcomes, one condition | `ifElse` |
| Three or more outcomes, or evolving rule sets | `decisionTable` |
| Produce a comparison classification (equal / greater / less / different) | `compare` |
| Produce a structural diff of two documents / sub-trees | `compare` (with `settings`) |

### Combining data

| Goal | Command |
|------|---------|
| Deep-merge two objects | `merge` |
| Fan-out / join by key across collections | `resolve` |

### ETL and serialization

| Goal | Command | Note |
|------|---------|------|
| Flatten nested object to flat key-value | `flatten` | ETL pack required |
| Reconstruct nested from flat | `restore` | ETL pack required; pair with `flatten` + `IncludeMetadata=true` |
| Export to CSV | `tocsv` | ETL pack required |

---

## Choosing the Right Function

### String checks (return boolean)

| Goal | Function | Notes |
|------|----------|-------|
| Substring anywhere in string | `contains` | Fails on null input |
| String starts with prefix | `startsWith` | Fails on null input |
| String ends with suffix | `endsWith` | Fails on null input |
| Find position of substring | `indexOf` | Returns -1 when not found; fails on null |
| Null-safe blank check | `isEmpty` | The ONLY null-safe predicate; others fail on null input |

### String assembly

| Goal | Function | When to use |
|------|----------|-------------|
| Fixed fields, mixed separators | `concat` | Variadic; any mix of literal strings and path values |
| Array of values, single separator | `join` | Pass an array path and one separator string |
| Prose template with `{0}` placeholders | `format` | Numbered placeholders; use when template text is fixed |

### String decomposition

| Goal | Function |
|------|----------|
| Split on delimiter | `split` |
| Extract by position | `substring` |
| Extract the last N characters | `right` (`left` is `substring(s,0,n)`) |
| Find position of substring | `indexOf` |
| Pull a fragment out by pattern | `regexExtract` — `""` when no match, not a failure |
| Rewrite by pattern | `regexReplace` — `replace` is literal-only |

### Case and whitespace

| Goal | Function |
|------|----------|
| Both ends | `trim` |
| Leading only | `trimStart` |
| Trailing only | `trimEnd` |
| Lowercase | `toLower` |
| Uppercase | `toUpper` |

### Numeric aggregates

| Goal | Function | Notes |
|------|----------|-------|
| Total | `sum` | |
| Mean | `avg` | Skewed by outliers |
| Middle value | `median` | Robust to outliers; prefer over `avg` for skewed data |
| Count of items | `count` | |
| Smallest | `min` | |
| Largest | `max` | |
| Conditional total | `sumif` | |
| Conditional count | `countif` | |

### Arithmetic

| Goal | Function | Notes |
|------|----------|-------|
| Add | `sum` | Variadic; flattens arrays |
| Subtract | `subtract` | Binary |
| Multiply | `multiply` | Variadic; flattens arrays — mirrors `sum`. Found-null multiplies as **0** |
| Divide | `divide` | Binary — mirrors `subtract`. Zero divisor fails |
| Remainder | `modulo` | Binary |
| Power | `pow` | Binary |
| Bound to a range | `clamp` | `value, low, high`, both inclusive; `low > high` fails |
| Direction of a number | `sign` | `-1` / `0` / `1` as a long |
| Free-form expression string | `calculate` | Parses one expression at run time |

Use `multiply`/`divide` for a product or quotient of values. `calculate` is for a genuinely
free-form expression — not for multiplying a list of numbers assembled with `concat`.

### Rounding

| Goal | Function |
|------|----------|
| Nearest | `round` |
| Always up | `ceiling` |
| Always down | `floor` |

### Date and time

| Goal | Function | Return type |
|------|----------|-------------|
| Is date in range? | `isDateBetween` | Boolean; both bounds inclusive |
| Which date is earlier? | `dateCompare` | Long: -1 (d1 before d2) / 0 (equal) / 1 (d1 after d2) — NOT a string |
| How far apart? (age, term, tenure) | `dateDiff` | Long; whole units truncated toward zero; negative when `to` precedes `from` |
| Shift a date | `dateAdd` | Date string; month-end clamps (31 Jan + 1 month → 28/29 Feb) |
| One component of a date | `datePart` | Long |
| Earliest in array | `minDate` | Date value |
| Latest in array | `maxDate` | Date value |
| Current UTC time | `datetime` | Formatted string — can only ever format *now* |
| Render a stored date | `formatDate` | String; this is the one `datetime` cannot do |
| Read a non-ISO date | `parseDate` | Canonical ISO date string — the normaliser |
| First / last day of month | `startOfMonth` / `endOfMonth` | Date string; leap-year correct |

Units for `dateDiff` and `dateAdd`, singular or plural: `years`, `months`, `weeks`, `days`,
`hours`, `minutes`, `seconds`. **`years` and `months` are calendar-aware** — `years` counts
birthdays passed, not `days/365.25`.

`datePart` parts: `year`, `month`, `day`, `hour`, `minute`, `second`, `quarter`, `dayofweek`
(**ISO: 1 = Monday, 7 = Sunday** — not .NET's 0 = Sunday), `dayofyear`, `weekofyear`
(ISO 8601), `daysinmonth`.

### Choosing a value

| Goal | Function | Notes |
|------|----------|-------|
| One value, two ways | `if` | **Lazy** — the untaken branch is never evaluated, so `=if(=exists($.a),=fetch($.a),'-')` is safe. Exactly 3 arguments |
| One fallback | `fetch($.path, default)` | The single-fallback case |
| Several candidate sources | `coalesce` | First argument that is neither null nor `""`; fails if none qualify, so end with a literal |
| Is a number inside a range? | `between` | Inclusive both ends; the numeric sibling of `isDateBetween` |

`if` does not replace `ifElse`. Structural branching — adding an object, removing a node,
running several commands — still needs the command. `if` replaces `ifElse`-used-as-a-ternary.

`coalesce` skips only null and `""`. `0`, `false`, `[]` and `{}` all qualify, which is where it
differs from a JavaScript `||`.

### Collections

| Goal | Function | Notes |
|------|----------|-------|
| Remove duplicates | `distinct` | Order-preserving, first occurrence wins; equality is deep |
| Order scalars | `sort` | `asc` (default) / `desc`; stable. Numeric **only** when every element is numeric, otherwise ordinal string order |
| Order objects by a field | `sortBy` | Key is a plain property chain (`'premium'`, `'$.rating.factor'`), **not** a path expression — no wildcards or predicates. Missing keys sort last in both directions |
| Last match of a path | `last` | `partial` counts from the front only |

These return a **collection**, so the containing command is normally a `put` to an array path.
They are new capability rather than shorthand: before them, de-duplicating required `merge` with
`uniqueItemsWithoutKeys` and a second document, and ordering was unreachable.

Note `=distinct($.items)` on an empty array answers `[]`, but `=distinct($.items[*])` on the
same document **fails** — `[*]` genuinely matches nothing, and path-not-found is an error.

### Advanced and path functions

| Goal | Function | Notes |
|------|----------|-------|
| Copy a value inline | `fetch` | Returns first match; optional default argument |
| Dynamic path stored in data | `indirect` | Two-step: reads path string, then resolves it |
| Select one from multi-match | `partial` | Zero-based index; default 0 |
| Get path of current node | `scriptpath` / `path` | `path()` is the JLio-compatible alias |
| Wrap node in parent object | `promote` | Optional explicit key name |
| Unique ID | `newGuid` | `newguid` (lowercase) from Text pack |
| JSON string → node | `parse` | Fails if argument is not valid JSON |
| Node → JSON string | `toString` | Objects emit compact JSON; null emits `""` |

---

## Critical Rules Every Agent Must Know

### 1. Function path resolution uses document ROOT, not current node

Inside any function call, `@.field` resolves against the ROOT (`$`), not the current array
element. To apply a function to each array element, use absolute indexed paths:
`$.users[0].email`, `$.users[1].email`. Wildcard paths `$.users[*].email` inside a function
produce a FLAT LIST — correct for aggregates (`sum`, `count`), wrong for per-element
transformation.

### 2. Function value syntax

Functions are written as string values: `"value": "=functionName(arg1, arg2)"`. NOT as
objects. The `=` prefix triggers function evaluation — on the outermost call only; nested
calls may omit it (`=concat(fetch($.a), '!')`).

### 2a. A JSON string value stays a string

`"value": "007"` writes the string `007`; `"value": "true"` writes the string `true`. Write
the JSON literal itself for other types: `"value": 7`, `"value": true`, `"value": null`.
Bare literals are typed only inside an argument list, where JSON cannot carry the type:
`=substring($.a, 0, 3)`.

### 2b. Notation problems are logged, not silent

Unresolvable paths, `@field` written without the dot, and nested calls to unregistered
functions all produce warnings on the execution log. Check `context.GetLogEntries()` when a
command writes nothing.

### 3. add vs set vs put

| Command | Field already exists | Field is absent |
|---------|---------------------|-----------------|
| `add` | Noop (trace: `"noop"`, detail: `"already exists"`) | Creates the field |
| `set` | Updates the field | Noop (trace: `"noop"`, detail: `"property not found"`) |
| `put` | Updates the field | Creates the field |

When uncertain, use `put`.

`set` also refuses to build a **missing parent path**: `set $.a.b` where `a` does not exist
is a noop with `"no nodes matched"`, not a scaffolded `{"a":{"b":…}}`. `add` and `put` do
create the path. To change a *name* rather than a value, use `rename`.

### 4. Trace outcome meanings

| Outcome | Meaning | Agent action |
|---------|---------|--------------|
| `"success"` | Command executed and changed data | Continue |
| `"noop"` | Command ran but found nothing to change (path not found, already-exists skip, no matches) | Investigate path or precondition — a noop is NOT an error |
| `"failure"` | Command could not execute (validation error, function path-not-found, wrong type) | Fix the script or input |

A `"noop"` is not an error. It signals a path or precondition mismatch to investigate.

### 5. Script format

Scripts are JSON arrays of command objects. Each command has a `"command"` key. All property
names are camelCase strings.

### 6. dateCompare returns long

`-1` = d1 before d2, `0` = equal, `1` = d1 after d2. NEVER returns strings.

### 7. decisionTable result values must be plain primitives

`"results": {"tier": "gold"}` is correct. `"results": {"tier": {"value": "gold"}}` writes
the entire object as the output — wrong.

### 8. ifElse condition must be a JSON primitive

`"condition": true` (boolean) or `"condition": "=isEmpty($.x)"` (function string). NOT
`"condition": {"value": true}`.

---

## Adapters

### JSON — Newtonsoft

> JSON adapter using Newtonsoft.Json with Goessner JSONPath.

**When to use**: default choice for JSON; scripts that use filter expressions `?()`,
script expressions `()`, recursive descent `$..`, or maximum JSONPath compatibility.

**When NOT to use**: when strict RFC 9535 compliance is required or Newtonsoft.Json
must not be added as a dependency.

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

> JSON adapter using System.Text.Json with RFC 9535-compliant JSONPath.

**When to use**: strict RFC 9535 path compliance is required; Newtonsoft.Json is excluded
from your dependency constraints; System.Text.Json performance characteristics are needed.

**When NOT to use**: when scripts use script expressions `()` — they are not supported.
Also avoid when you need Goessner-specific behaviours.

**Node type**: `JsonNode`

```csharp
using TLio.Json.SystemText;
using TLio.Client;

var options = ParseOptions<JsonNode>.CreateDefault();
var engine  = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, SystemTextJsonExecutionContext.CreateDefault());
```

**Path syntax**: identical to JSON (Newtonsoft) except script expressions `()` are not
supported.

---

### XML — Slash Paths

> XML adapter using slash-separated paths. Simplest XML path model.

**When to use**: straightforward hierarchical XML with no predicates; paths are simple
`/parent/child` chains; readable scripts are preferred over full XPath power.

**When NOT to use**: when you need XPath predicates (`[@id='1']`), recursive descent
(`//name`), positional indexing, or any XPath axis.

**Node type**: `XElement`

```csharp
using TLio.Xml;
using TLio.Client;

var options = ParseOptions<XElement>.CreateDefault();
var engine  = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
var result  = engine.Execute(scriptJson, data, XmlExecutionContext.CreateWithSlashPaths());
```

**Path syntax** — anchored on the document node, as XPath defines it. The document
element is always named in the path:

| Pattern | Example | Matches |
|---------|---------|---------|
| Document node | `/` | Not an element — selects nothing |
| Document element | `/order` | The root element itself |
| Child | `/order/name` | Direct child element |
| Nested | `/order/address/city` | Nested element |
| Wildcard | `/order/items/*` | All children of `items` |

Notes: no predicate support; element names are case-sensitive. `/name` and `name` mean
"a `name` child of the document node" and match nothing — a relative step never skips a
level. Use `//name` for "at any depth".

---

### XML — Native XPath

> XML adapter using native XPath 1.0.

**When to use**: paths need attribute predicates (`[@id='1']`), recursive descent (`//name`),
XPath axes, or positional indexing. Use when slash-path is not powerful enough.

**When NOT to use**: when simple slash paths are sufficient — prefer slash-path for
simplicity and readability. Note that XPath indexing is **1-based** (`item[1]` = first,
not `item[0]`).

**Node type**: `XElement`

```csharp
var result = engine.Execute(scriptJson, data, XmlExecutionContext.CreateWithNativeXPath());
```

**Path syntax**:

| Pattern | Example | Matches |
|---------|---------|---------|
| Document node | `/` | Not an element — selects nothing |
| Document element | `/order` | The root element itself |
| Child | `/order/name` | Direct child element |
| Nested | `/order/address/city` | Nested element |
| Recursive | `//name` | All `name` at any depth |
| Indexed | `/order/items/item[1]` | First `item` (1-based) |
| Attribute predicate | `/order/items/item[@id='1']` | `item` with `id='1'` |
| Wildcard | `/order/*` | All children of the document element |

Notes: `/` is the document node and `/order` the document element, so every path names it.
A bare `name` is a child of the document node and matches nothing. XPath indexing is **1-based**.

---

### YAML

> YAML adapter using dot-notation paths.

**When to use**: source documents are YAML. Multi-document YAML (separated by `---`) is
parsed as an array root — access documents via `$[0]`, `$[1]`, etc.

**When NOT to use**: when you need filter expressions or array-index path syntax identical
to JSONPath. YAML paths use dot-notation; filter expressions are not supported.

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

Notes: keys are case-sensitive; no filter expression support; multi-doc → use `$[0]`,
`$[1]`, etc.

---

## Commands

### add

> Creates a new property or appends to an array; **skips silently if the property already
> exists**. Use `put` to update existing values, or `set` when the node must already exist.

**When to use**: you want create-only semantics — the field must not already exist. Ideal
for initialising defaults without risking overwrites.

**When NOT to use**: when the field might already exist and you want to update it — use
`put` (upsert) or `set` (update-only). Do not use `add` when you need guaranteed writes.

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

> Compares two nodes and writes the outcome to a target path — a scalar label
> (`"equal"`, `"greater"`, `"less"`, `"different"`) for primitive comparisons, or a
> structured array of difference entries for objects and arrays.

**When to use**: you need a classification of the relationship between two values, or a
structural diff describing *what* differs between two documents / sub-trees.

**When NOT to use**: when you want to branch immediately — use `ifElse` for branching, or
`dateCompare` (which returns `-1/0/1`) for date ordering.

**Supports functions**: ❌

```json
{ "command": "compare", "fromPath": "$.a", "toPath": "$.b", "resultPath": "$.result" }
```

```json
{
  "command": "compare",
  "firstPath": "$.first",
  "secondPath": "$.second",
  "resultPath": "$.result",
  "settings": {
    "arraySettings": [{ "arrayPath": "$.first", "keyPaths": ["@.id"], "uniqueIndexMatching": true }],
    "resultTypes": ["valueDifference", "structureDifference"]
  }
}
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Path to first node (left-hand side). Alias: `firstPath`. |
| toPath | string | yes | — | Path to second node (right-hand side). Alias: `secondPath`. |
| resultPath | string | yes | — | Path where the result is written (upsert). |
| settings | object | no | — | Diff configuration. Omit for defaults. |
| settings.arraySettings[].arrayPath | string | — | — | Absolute path of the array the rule applies to (matched against either side). |
| settings.arraySettings[].keyPaths | string[] | no | `[]` | Relative element keys (`"id"`, `".id"`, `"@.id"`). Empty = index-based comparison. |
| settings.arraySettings[].uniqueIndexMatching | bool | no | `false` | Also report items matched at a different index. |
| settings.resultTypes | string[] | no | `[]` | Keep only entries with these `differenceType` values. Empty = keep all. |

**Scalar result values** (default settings, both sides a single primitive):

| Value | Meaning |
|-------|---------|
| `"equal"` | Both nodes have equal scalar values |
| `"greater"` | First node's value > second node's value |
| `"less"` | First node's value < second node's value |
| `"different"` | Values differ and cannot be ordered |

**Structured result** (objects, arrays, or any explicit `settings`) — an array of entries:

```json
{
  "foundDifference": true,
  "differenceType": "valueDifference",
  "differenceSubType": "lessThan",
  "firstPath": "$.first.a",
  "secondPath": "$.second.a",
  "description": "The values are different LessThan. Source: ($.first.a) --> 1 - Target:($.second.a) --> 2"
}
```

`differenceType`: `noDifference` | `valueDifference` | `structureDifference` | `arrayDifference` | `typeDifference`.
`differenceSubType`: `equals` | `notEquals` | `lessThan` | `greaterThan` | `indexDifference`.

Entries with `foundDifference: false` document a match — an empty result array is not the
only "no differences" outcome. Works identically over JSON, XML and YAML; only the path
notation differs.

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Compare().From("$.score").To("$.threshold").Result("$.verdict");

var diff = new TLioScript<JToken>()
    .Compare("$.first").With("$.second").Using(settings).SetResultOn("$.result");
```

---

### copy

> Copies all nodes matched by `fromPath` to the location(s) specified by `toPath`.
> Source nodes remain. Use `move` to copy-and-delete the source.

**When to use**: you need the value in a new location and the original must be preserved.

**When NOT to use**: when the source should be deleted after copying — use `move` instead.

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

**When to use**: three or more outcomes based on data values; rule sets that evolve
independently of the script; business-rule tables maintained by non-developers.

**When NOT to use**: when there are exactly two outcomes from a single condition — `ifElse`
is simpler. Do not use when rule result values need to be objects — results must be plain
primitives.

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
Result values must be plain primitives — wrapping in an object writes the object itself.

---

### flatten

> Flattens a nested object to a single-level object with delimiter-separated keys, and
> optionally stores metadata for reconstruction with `restore`.

**When to use**: you need to process nested data in a flat key-value format, or prepare
data for CSV export. Always set `metadataPath` if you plan to `restore` afterward.

**When NOT to use**: when you need to preserve nested structure for further path-based
operations. Do not flatten without metadata if you intend to restore.

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

**When to use**: exactly two outcomes from a single condition. Condition can be a literal
boolean, a path, or a function expression like `=isEmpty($.x)`.

**When NOT to use**: three or more outcomes — use `decisionTable`. The condition must be
a JSON primitive, not an object; `"condition": {"value": true}` is wrong.

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
> Objects are merged recursively; arrays follow `arrayMergeMode` or, when configured,
> per-array key matching from `settings`.

**When to use**: combining two objects where overlapping keys should be resolved by the
source overwriting the destination. Common for applying patches or partial updates.
Also reconciling collections by identity via `settings.arraySettings[].keyPaths`.

**When NOT to use**: when you want a simple field copy — use `copy`. When you need to
join a collection against a lookup table and project fields — use `resolve`.

**Supports functions**: ❌

```json
{ "command": "merge", "fromPath": "$.source", "toPath": "$.target" }
```

**Options**:

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Selects the source node(s). Alias: `path`. |
| toPath | string | yes | — | Selects the destination node(s). Alias: `targetPath`. Must differ from `fromPath`. |
| arrayMergeMode | string | no | `"concat"` | `"concat"` appends; `"replace"` overwrites; `"mergeByKey"` appends only items not already present. |
| settings | object | no | — | Fine-grained configuration — see below. |

**`settings`**:

| Sub-option | Type | Default | Description |
|------------|------|---------|-------------|
| strategy | string | `"fullMerge"` | `"fullMerge"` adds and overwrites; `"onlyStructure"` adds missing properties only; `"onlyValues"` updates existing properties only. |
| arraySettings[].arrayPath | string | — | Path of the **target** array (root indicator ignored, so `$.a.items`, `a.items` and `/a/items` are equivalent). |
| arraySettings[].keyPaths | string[] | `[]` | Fields identifying an array element; matching elements are merged, the rest appended. Plain and `@.` notation. |
| arraySettings[].uniqueItemsWithoutKeys | bool | `false` | Without `keyPaths`, skip items already present (deep-equal) in the target array. |
| matchSettings.keyPaths | string[] | `[]` | Objects are merged only when all these fields are equal on source and target. |

```json
{
  "command": "merge", "fromPath": "$.incoming", "toPath": "$.current",
  "settings": { "arraySettings": [ { "arrayPath": "$.current.items", "keyPaths": ["id"] } ] }
}
```

XML has no native array type; array semantics apply to a repeated-element container
only when `arraySettings.arrayPath` names its path.

**C# Fluent API**:

```csharp
var script = new TLioScript<JToken>()
    .Merge().From("$.patch").To("$.document");

var keyed = new TLioScript<JToken>()
    .Merge().From("$.incoming").WithArrayKeys("$.current.items", "id").To("$.current");
```

---

### move

> Moves nodes from `fromPath` to `toPath` — equivalent to `copy` followed by `remove`
> on the source. Source nodes are deleted after the copy succeeds.

**When to use**: renaming a field or relocating a node where the original location must
not remain. Atomic copy-then-delete.

**When NOT to use**: when the source must be preserved — use `copy` instead.

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

**When to use**: the safe default when you are unsure whether a field exists. Creates or
updates — always writes.

**When NOT to use**: when you specifically need create-only (`add`) or update-only (`set`)
semantics for correctness guarantees.

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

**When to use**: permanently deleting a field or node. Wildcards remove multiple nodes at once.

**When NOT to use**: when you want to move a node to a new location — use `move`. A
no-match produces a `"noop"` trace, not a failure.

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

### rename

> Changes the name under which a node is known, keeping its value, children and position.
> Logs a warning if no nodes match or the node has no name; does not error.

**When to use**: renaming a field while keeping its position, or renaming an XML element —
including the document element, which nothing else can reach. Preserves XML attributes and
sibling order, which `copy` + `remove` does not.

**When NOT to use**: when the node should change *place* — use `move`. When its *value*
should change — use `set` or `put`. The `name` option is a literal; functions are not
evaluated there.

**Supports functions**: ❌

```json
{ "command": "rename", "path": "/order", "name": "opdracht" }
```

**Options**:

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| path | string | yes | Selects the node(s) to rename. Wildcards and `//` rename every match. |
| name | string | yes | The new name, as a literal. |

**What can be renamed** — a node's name lives in a different place per format:

| Format | Name belongs to | Root renameable? |
|--------|-----------------|------------------|
| XML | the element itself | ✅ `<order>` → `<opdracht>` |
| JSON / YAML | the parent property, key, or mapping entry | ❌ — a root has no name |

Renaming a JSON/YAML root, or an array element (named by position), warns and changes
nothing. It is a noop, not a failure.

**C# Fluent API**:

```csharp
var script = new TLioScript<XElement>()
    .Rename("opdracht").OnPath("/order");
```

---

### resolve

> Looks up matching entries in a reference collection and writes derived values to
> the target document. Supports relative `@.property` paths for writing results.

**When to use**: fan-out / join by key — enriching an array of records with fields from
a separate reference collection, similar to a LEFT JOIN.

**When NOT to use**: when you need to deep-merge objects (use `merge`) or simply copy a
single value (use `copy` or `fetch`).

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

**When to use**: after a `flatten` operation when you need to reconstruct the original
nested structure. Always pair with `flatten` that had `metadataPath` set (`IncludeMetadata=true`).

**When NOT to use**: without prior `flatten` + metadata. In `strictMode: true`, missing
metadata causes a failure rather than a noop.

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

**When to use**: update-only semantics — you want a noop (not a creation) when the field
is absent. Useful for enforcing that a field was expected to already exist.

**When NOT to use**: when the field might not exist and you want to create it — use `put`.
Do not use `set` as a general-purpose write command when field existence is uncertain.

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

**When to use**: exporting structured data to CSV format for downstream file or reporting
consumption.

**When NOT to use**: when you need structured output — CSV is a string, not a structured
node. For structured transformation, use `flatten` + `resolve` instead.

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
All built-in functions are registered by `ParseOptions.CreateDefault()`. Text-pack functions
require `RegisterText<TNode>()`.

### concat

> Concatenates two or more string arguments into a single string.

**When to use**: assembling strings from multiple fields or literals with different separators.
**When NOT to use**: joining an array of values with one separator — use `join` instead.

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

**When to use**: stamping a record with the current time at script execution. Returns
the wall-clock UTC time — not deterministic across runs.

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

---

### fetch

> Evaluates a path expression and returns the **first matched node's value**. Returns an
> optional default value when the path matches nothing.

**When to use**: copying a value inline within a function argument or value expression.
The workhorse for value transfer in function contexts. Provide a default to avoid failures
on missing paths.

**When NOT to use**: when you need to copy the node itself (structure included) to another
path — use `copy`. When you need multi-match results — use `partial`.

**Syntax**: `=fetch(path)` or `=fetch(path, defaultValue)`
**Pack**: built-in

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path selecting the source node. |
| 2 | any | no | Default value when path resolves to nothing. |

**Returns**: first matched node, or default if provided; logs warning and fails if no match
and no default.

**Examples**:

```json
{ "command": "set",  "path": "$.target", "value": "=fetch($.source)" }
{ "command": "add",  "path": "$.name",   "value": "=fetch($.user.name,'Anonymous')" }
```

---

### format

> Replaces `{0}`, `{1}`, … placeholders in a template string with the supplied argument values.

**When to use**: prose templates where the structure is fixed and the values are variable.
Cleaner than nested `concat` calls when the template has surrounding text.

**When NOT to use**: joining an array with a separator — use `join`. Simple two-field
concatenation — `concat` is more direct.

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

**When to use**: the path to read is itself stored in the data — dynamic dispatch based
on a runtime value.

**When NOT to use**: when the path is statically known — use `fetch` directly.

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

---

### length

> Returns the number of characters in a string.

**When to use**: measuring string length for validation or conditional logic.
**When NOT to use**: counting array elements — use `count` instead.

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

**When to use**: assigning a unique identifier to a new record at script execution time.

**Syntax**: `=newGuid()`
**Pack**: built-in (camelCase) + Text pack (`"newguid"` lowercase alias)

No arguments.

**Example**:

```json
{ "command": "add", "path": "$.id", "value": "=newGuid()" }
```

Output: `{ "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" }` (any valid UUID v4)

Notes: UUID is generated at execution time; `"newguid"` (lowercase) is registered by
`RegisterText<TNode>()`.

---

### parse

> Parses a JSON string into a structured node (object, array, or primitive).

**When to use**: a field contains a JSON string that needs to be treated as a structured
node for further processing. Complement of `=toString()`.

**When NOT to use**: when the value is already a structured node. Fails if the argument
is not valid JSON.

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

---

### partial

> Selects one element from a **multi-match path expression** by zero-based index.

**When to use**: a path expression returns multiple nodes and you need exactly one of
them by position.

**When NOT to use**: when the path returns a single node — use `fetch` instead.

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

---

### path

> Returns the current node's absolute path as a string. Alias for `=scriptpath()`.

**When to use**: JLio-compatible scripts; when you need the canonical absolute path of
the node being processed.

**Syntax**: `=path()`
**Pack**: built-in (alias registered in `ParseOptions.CreateDefault()`)

No arguments. Identical behaviour to `=scriptpath()`.

**Example**:

```json
{ "command": "add", "path": "$.items[*].loc", "value": "=path()" }
```

Input: `{ "items": [{"id":1},{"id":2}] }` → each item gets `"loc": "$.items[0]"` /
`"$.items[1]"`

Notes: `=path()` and `=scriptpath()` share the same implementation. Use `=path()` for
JLio compatibility.

---

### promote

> Wraps the matched node in a new object using either the node's own **property name**
> or an **explicit name** as the key.

**When to use**: when you need to nest an existing value under a named key, e.g. to
build envelope objects or reshape data for a downstream API.

**When NOT to use**: when you need to move (not wrap) a node — use `move` or `copy`.

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

---

### replace

> Replaces all occurrences of a substring within a string.

**When to use**: normalising delimiters, removing characters, or substituting substrings.
Case-sensitive by default.

**When NOT to use**: when you need case-insensitive replacement or regex — not supported.

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

**When to use**: when you need the canonical path of the node currently being processed,
e.g. to store self-referential metadata or debug output.

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

**When to use**: extracting a known positional slice of a string (e.g. first 3 chars,
chars from position 5).

**When NOT to use**: when you need to find a position first — use `indexOf` to get the
start index, then `substring`.

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

**When to use**: serialising a structured node to a JSON string, e.g. before storing in
a string field or passing to a system that expects string payloads.

**When NOT to use**: when you want to write the node as a structured value — use `copy`
or `fetch`. `toString` on null returns `""`, not `"null"`.

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

Notes: `FunctionName` must be overridden explicitly because the CLR type name
`ToStringFunction` would otherwise produce `"tostringfunction"`.

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

**When to use**: normalising user input or data ingested from external sources.
**When NOT to use**: when you only want one side — use `trimStart` or `trimEnd`.

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

| Command | When to use (one line) | Supports functions | Fluent | Pack |
|---------|------------------------|-------------------|--------|------|
| add | Create-only; noop if field exists | ✅ | `.Add(v).OnPath(p)` | built-in |
| compare | Classify two values, or structurally diff two sub-trees | ❌ | `.Compare(a).With(b).Using(s).SetResultOn(r)` | built-in |
| copy | Copy node, keep source | ❌ | `.Copy().From(a).To(b)` | built-in |
| decisionTable | 3+ outcomes or evolving rule sets | ✅ (results only) | — | built-in |
| flatten | Nested → flat key-value | ❌ | — | ETL |
| ifElse | Two outcomes, one condition | ✅ (condition only) | `.IfElse(c).If(...).Else(...)` | built-in |
| merge | Deep-merge two objects | ❌ | `.Merge().From(a).To(b)` | built-in |
| move | Copy node, delete source | ❌ | `.Move().From(a).To(b)` | built-in |
| put | Upsert — safe default write | ✅ | `.Put(v).OnPath(p)` | built-in |
| remove | Delete a node | ❌ | `.Remove().OnPath(p)` | built-in |
| resolve | Join / fan-out by key | ❌ | — | ETL |
| restore | Flat key-value → nested (reverses flatten) | ❌ | — | ETL |
| set | Update-only; noop if field absent | ✅ | `.Set(v).OnPath(p)` | built-in |
| tocsv | Export array to CSV string | ❌ | — | ETL |

### Function Summary

| Function | Syntax | When to use (one line) | Pack |
|----------|--------|------------------------|------|
| concat | `=concat(a,b,...)` | Fixed fields, mixed separators | Text |
| datetime | `=datetime()` / `=datetime(fmt)` | Current UTC timestamp | built-in |
| fetch | `=fetch(path)` / `=fetch(path,default)` | Read a value inline; workhorse for value transfer | built-in |
| format | `=format(tpl,v0,...)` | Prose template with `{0}` placeholders | Text |
| indirect | `=indirect(pathToPath)` | Path stored in data — dynamic dispatch | built-in |
| length | `=length(str)` | String character count | Text |
| newGuid | `=newGuid()` | Generate unique ID | built-in / Text (`newguid`) |
| parse | `=parse(str)` | JSON string → structured node | Text |
| partial | `=partial(path)` / `=partial(path,i)` | One element from multi-match by index | built-in |
| path | `=path()` | Absolute path of current node (JLio alias) | built-in |
| promote | `=promote(path)` / `=promote(path,name)` | Wrap node in parent object | built-in |
| replace | `=replace(str,old,new)` | Substitute substring | Text |
| scriptpath | `=scriptpath()` / `=scriptpath(@.child)` | Absolute path of current node | built-in |
| substring | `=substring(str,start)` / `=substring(str,start,n)` | Extract by position | Text |
| toLower | `=toLower(str)` | Lowercase | Text |
| toString | `=toString(node)` | Node → JSON string | Text |
| toUpper | `=toUpper(str)` | Uppercase | Text |
| trim | `=trim(str)` | Both-end whitespace removal | Text |
| trimEnd | `=trimEnd(str)` | Trailing whitespace removal | Text |
| trimStart | `=trimStart(str)` | Leading whitespace removal | Text |
| toFixed | `=toFixed(v,n)` / `=toFixed(v,n,sep)` | Money-style text with exactly n decimals | Text |
| right | `=right(str,n)` | Last n characters | Text |
| regexReplace | `=regexReplace(str,pattern,repl)` | Rewrite by pattern; `$1` backreferences work | Text |
| regexExtract | `=regexExtract(str,pattern)` / `=regexExtract(str,pattern,group)` | Pull a fragment out; `""` when no match | Text |
| multiply | `=multiply(a,b,...)` | Product of values — mirrors `sum` | Math |
| divide | `=divide(a,b)` | Quotient — mirrors `subtract` | Math |
| clamp | `=clamp(v,low,high)` | Bound a value to a range | Math |
| sign | `=sign(v)` | `-1` / `0` / `1` | Math |
| dateDiff | `=dateDiff(from,to)` / `=dateDiff(from,to,unit)` | How far apart two dates are; unit defaults to `days` | TimeDate |
| dateAdd | `=dateAdd(date,n)` / `=dateAdd(date,n,unit)` | Shift a date; `n` may be negative | TimeDate |
| datePart | `=datePart(date,part)` | One component of a date, as a long | TimeDate |
| formatDate | `=formatDate(date,fmt)` | Render a **stored** date — `datetime` only formats now | TimeDate |
| parseDate | `=parseDate(text)` / `=parseDate(text,fmt)` | Normalise a non-ISO date to canonical ISO | TimeDate |
| startOfMonth | `=startOfMonth(date)` | First day of that month | TimeDate |
| endOfMonth | `=endOfMonth(date)` | Last day of that month; leap-year correct | TimeDate |
| if | `=if(cond,whenTrue,whenFalse)` | One value, two ways — lazy | built-in |
| coalesce | `=coalesce(a,b,...)` | First value that is neither null nor `""` | built-in |
| distinct | `=distinct(array)` | Remove duplicates, order-preserving | built-in |
| sort | `=sort(array)` / `=sort(array,dir)` | Order scalars, stable | built-in |
| sortBy | `=sortby(array,keyPath)` / `=sortby(array,keyPath,dir)` | Order objects by a field | built-in |
| last | `=last(path)` | Last match of a path | built-in |

Full pages for every function, including the traps, live in `docs/ai-ref/functions/` and are
what `tlio_describe` serves.

### Predicate Summary (conditions)

All built in — no pack registration needed, because `ifElse` and `decisionTable` are core commands.
Every one returns a boolean node; a path that matches nothing is an answer, not an error.

| Function | Syntax | When to use (one line) |
|----------|--------|------------------------|
| equals | `=equals(a,b)` | Value equality, bridging number/text |
| notEquals | `=notEquals(a,b)` | Everything except one value |
| greaterThan | `=greaterThan(a,b)` | Exclusive lower threshold |
| greaterOrEqual | `=greaterOrEqual(a,b)` | Inclusive lower threshold / range start |
| lessThan | `=lessThan(a,b)` | Exclusive upper threshold |
| lessOrEqual | `=lessOrEqual(a,b)` | Inclusive upper threshold / range end |
| between | `=between(v,low,high)` | A whole range in one call, both bounds inclusive |
| and | `=and(c1,c2,...)` | All conditions must hold |
| or | `=or(c1,c2,...)` | Any condition may hold |
| not | `=not(c)` | Invert a predicate that has no negative twin |
| exists | `=exists(path)` | Path matched something (null still counts) |
| isNull | `=isNull(v)` | Value is null, or path matched nothing |
| isString | `=isString(v)` | Document type is text |
| isNumber | `=isNumber(v)` | Document type is numeric |
| isBoolean | `=isBoolean(v)` | Document type is boolean |
| isArray | `=isArray(v)` | Value is a list |
| isObject | `=isObject(v)` | Value has named properties |
| in | `=in(v,o1,o2,...)` | Membership in a set or an array from the document |
| matches | `=matches(v,regex)` | Format validation by regular expression |
| isEmpty | `=isEmpty(v)` | Null, empty string, or empty array (Text pack) |

**Truthiness**: only a boolean, or the text `"true"`/`"false"`, is true. A number or a non-empty
string is **not** — wrap it in a predicate: `=greaterThan($.count, 0)`, not `=and($.count)`.
