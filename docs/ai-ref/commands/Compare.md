# compare

> Compares two nodes and writes the outcome to a target path — either a scalar label
> (`"equal"`, `"greater"`, `"less"`, `"different"`) for primitive comparisons, or a
> structured array of difference entries for objects and arrays.

## Syntax

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
    "arraySettings": [{ "arrayPath": "$.first.items", "keyPaths": ["@.id"], "uniqueIndexMatching": true }],
    "resultTypes": ["valueDifference", "structureDifference"]
  }
}
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| firstPath | string | yes | — | Path to the first node (left-hand side of comparison). JLio-compatible alias: `fromPath` (write-only — sets `firstPath`). |
| secondPath | string | yes | — | Path to the second node (right-hand side). JLio-compatible alias: `toPath` (write-only — sets `secondPath`). |
| resultPath | string | yes | — | Path where the result is written (upsert). |
| settings | object | no | — | Diff configuration. Omit for defaults. |
| settings.arraySettings | array | no | `[]` | Per-array key matching rules. |
| settings.arraySettings[].arrayPath | string | yes (within the entry) | — | Absolute path of the array the entry applies to. Matched against either side, so `"$.first.items"` and `"$.second.items"` both select the same rule. |
| settings.arraySettings[].keyPaths | string[] | no | `[]` | Relative paths identifying an array element. `"id"`, `".id"` and `"@.id"` are equivalent. When empty, elements are compared by index. |
| settings.arraySettings[].uniqueIndexMatching | bool | no | `false` | When true, an element matched at a different index also produces an `indexDifference` entry. |
| settings.resultTypes | string[] | no | `[]` | Keep only entries with these `differenceType` values. Empty = keep everything. |

**Functions in the value**: — no value field  
**Functions in the path**: ✅ `=indirect()` in `firstPath`, `secondPath` and `resultPath`

## Result shapes

### Scalar (default, primitives only)

When `settings` is omitted and both paths resolve to a single primitive node, a plain
string is written — unchanged from earlier TLio versions:

| Value | Meaning |
|-------|---------|
| `"equal"` | Both nodes have equal values |
| `"greater"` | First node's value > second node's value |
| `"less"` | First node's value < second node's value |
| `"different"` | Values differ and cannot be ordered |

### Structured diff (objects, arrays, or any explicit `settings`)

An array node is written, one element per difference entry:

```json
[
  {
    "foundDifference": true,
    "differenceType": "valueDifference",
    "differenceSubType": "lessThan",
    "firstPath": "$.first.a",
    "secondPath": "$.second.a",
    "description": "The values are different LessThan. Source: ($.first.a) --> 1 - Target:($.second.a) --> 2"
  }
]
```

| Field | Meaning |
|-------|---------|
| foundDifference | `true` only when the entry records an actual difference. Entries with `false` document a match. |
| differenceType | `noDifference`, `valueDifference`, `structureDifference`, `arrayDifference` or `typeDifference`. |
| differenceSubType | `equals`, `notEquals`, `lessThan`, `greaterThan` or `indexDifference`. |
| firstPath / secondPath | Paths of the compared nodes, in the notation of the active adapter. |
| description | Human-readable explanation. |

`differenceType` values:

| Value | Emitted when |
|-------|--------------|
| `noDifference` | The two nodes are equivalent. `foundDifference` is `false`. |
| `typeDifference` | The nodes are of a different kind (object / array / primitive / null). |
| `valueDifference` | Two primitives of the same kind hold different values. |
| `structureDifference` | A property exists on one side only. |
| `arrayDifference` | Array item count, membership or index differs — also used for the informational "both arrays have N items" and "both arrays contain a matching item" entries. |

## Formats

Works with all adapters. The whole diff runs through `INodeAdapter` / `IItemsFetcher`, so
JSON, XML and YAML produce the same entries — only the path notation differs
(`$.first.a`, `/first/a`, `$.first.a`). For XML the structured result is written as
repeated `<object>` elements under `resultPath`.

See [overview.md](../overview.md) for per-adapter path syntax.

## Verified example

### Scalar — using the `fromPath`/`toPath` aliases

Input:

```json
{ "a": 10, "b": 20 }
```

Script:

```json
[{ "command": "compare", "fromPath": "$.a", "toPath": "$.b", "resultPath": "$.result" }]
```

Result:

```json
{ "a": 10, "b": 20, "result": "less" }
```

`fromPath`/`toPath` are the JLio-compatible spellings of `firstPath`/`secondPath` — this
fixture is the one place in the corpus that exercises them instead of the canonical names.

Verified by: `TLio.UnitTests/Fixtures/Compare/04-compare-from-to-path/fixture.json`

### Structured diff — object with a value difference and a structure difference

Input:

```json
{
  "first": { "a": 1, "b": "x", "only": true },
  "second": { "a": 2, "b": "x" }
}
```

Script:

```json
[{
  "command": "compare",
  "firstPath": "$.first",
  "secondPath": "$.second",
  "resultPath": "$.result",
  "settings": { "resultTypes": ["valueDifference", "structureDifference"] }
}]
```

Result at `$.result`:

```json
[
  {
    "foundDifference": true,
    "differenceType": "valueDifference",
    "differenceSubType": "lessThan",
    "firstPath": "$.first.a",
    "secondPath": "$.second.a",
    "description": "The values are different LessThan. Source: ($.first.a) --> 1 - Target:($.second.a) --> 2"
  },
  {
    "foundDifference": true,
    "differenceType": "structureDifference",
    "differenceSubType": "notEquals",
    "firstPath": "$.first.only",
    "secondPath": "$.second.only",
    "description": "The structure is different. Source: ($.first.only) --> True - Target:($.second.only) --> False"
  }
]
```

Verified by: `TLio.UnitTests/Fixtures/Compare/05-compare-object-diff/fixture.json`

### Array diff with key matching

Input:

```json
{
  "first": [{ "id": 1, "v": "a" }, { "id": 2, "v": "b" }],
  "second": [{ "id": 2, "v": "b" }, { "id": 1, "v": "a" }]
}
```

Script:

```json
[{
  "command": "compare",
  "firstPath": "$.first",
  "secondPath": "$.second",
  "resultPath": "$.result",
  "settings": {
    "arraySettings": [{ "arrayPath": "$.first", "keyPaths": ["@.id"], "uniqueIndexMatching": true }],
    "resultTypes": ["arrayDifference"]
  }
}]
```

Elements are paired by `id` rather than by position, so the reordered array reports no
`valueDifference`/`structureDifference` entries — only `arrayDifference` entries, and because
`uniqueIndexMatching` is on, one `indexDifference` per element that moved:

```json
[
  { "foundDifference": false, "differenceType": "arrayDifference", "differenceSubType": "equals",
    "firstPath": "$.first", "secondPath": "$.second",
    "description": "Both arrays have 2 items. Source: ($.first) - Target:($.second)" },
  { "foundDifference": false, "differenceType": "arrayDifference", "differenceSubType": "equals",
    "firstPath": "$.first", "secondPath": "$.second",
    "description": "Both arrays contain a matching item. Source: ($.first) - Target:($.second). Value:{\"id\":1,\"v\":\"a\"}" },
  { "foundDifference": true, "differenceType": "arrayDifference", "differenceSubType": "indexDifference",
    "firstPath": "$.first", "secondPath": "$.second",
    "description": "The indexes of the matched items are different. Source: ($.first)[0] - Target:($.second)[1]" },
  { "foundDifference": false, "differenceType": "arrayDifference", "differenceSubType": "equals",
    "firstPath": "$.first", "secondPath": "$.second",
    "description": "Both arrays contain a matching item. Source: ($.first) - Target:($.second). Value:{\"id\":2,\"v\":\"b\"}" },
  { "foundDifference": true, "differenceType": "arrayDifference", "differenceSubType": "indexDifference",
    "firstPath": "$.first", "secondPath": "$.second",
    "description": "The indexes of the matched items are different. Source: ($.first)[1] - Target:($.second)[0]" }
]
```

Verified by: `TLio.UnitTests/Fixtures/Compare/06-compare-array-key-matching/fixture.json`

### Report only real differences

`resultTypes` filters by `differenceType`, not by `foundDifference` — to drop the informational
"they match" entries, exclude `noDifference` and `arrayDifference`:

```json
"settings": { "resultTypes": ["valueDifference", "structureDifference", "typeDifference"] }
```

## Cross-format note

On XML, an empty element (`<k/>`) reads as *null* rather than as an empty object — so two
empty elements verdict `equal` and an empty element against a scalar verdicts `different`,
the same compact answers JSON gives for `null`, rather than a `typeDifference` against an
empty object. See `docs/behaviour-decisions.md`, section E5, for the full rationale.

## C# Fluent API

```csharp
// Scalar comparison
var script = new TLioScript<JToken>()
    .Compare().From("$.score").To("$.threshold").Result("$.verdict");

// Structured diff with settings
var settings = new CompareSettings
{
    ArraySettings = { new CompareArraySettings { ArrayPath = "$.first", KeyPaths = { "@.id" } } },
    ResultTypes   = { DifferenceType.ValueDifference }
};

var diff = new TLioScript<JToken>()
    .Compare("$.first").With("$.second").Using(settings).SetResultOn("$.result");
```

## When to use

- You need to produce a comparison label (`equal`, `greater`, `less`, `different`) and store it for later use or inspection.
- You need to know *what* differs between two documents or sub-trees — which property, which array element, and how.
- Regression / contract testing: diff an expected sub-tree against an actual one and assert the result array is empty.
- Validation pipelines where the comparison result is itself an output (e.g., audit logs, assertion records).
- As a preparatory step before an `ifElse` that branches on the comparison result.

## When NOT to use

- You want to branch based on a comparison result without storing it — use `ifElse` directly with a function condition instead.
- You want to transform or route data based on a comparison — `compare` only classifies and describes; use `ifElse` or `decisionTable` for the actual transformation.
- The comparison result is never needed in the output document.

## Comparison: IfElse vs DecisionTable vs Compare

| Criterion | ifElse | decisionTable | compare |
|-----------|--------|---------------|---------|
| Number of outcomes | 2 | 3+ (or 2 if rules evolve) | 1 label, or a list of difference entries |
| Condition complexity | Single expression | Multiple input columns, multi-condition rules | Fixed: left node vs right node |
| Data transformation | Yes — writes any value | Yes — writes rule result values | No — only writes the comparison outcome |
| Rule maintenance | Rewrite JSON | Add/edit a rule row | Adjust `settings` |
| Overlapping rules | Not supported | bestMatch / allMatches | N/A |
| Use to branch on comparison | Yes — after `compare` writes result | Rarely | No |

## Common mistakes

- **Expecting a scalar for object or array inputs.**
  Comparing two objects or arrays always writes the structured array, never `"different"`.
  The scalar shape is reserved for single primitive nodes compared with default settings.

- **Expecting a scalar once `settings` is present.**
  Any non-empty `settings` switches the output to the structured array, even for primitives.

- **Reading `result.length == 0` as "identical".**
  Matching entries are reported too (`noDifference`, and the informational `arrayDifference`
  entries). Check `foundDifference` on each entry, or use `resultTypes` to filter.

- **Treating `"different"` as "not equal".**
  In the scalar shape, `"different"` means the values are incomparable (e.g. a string vs a
  number). Two numbers that are not equal produce `"greater"` or `"less"`.

- **`arrayPath` pointing at the wrong array.**
  It must be the absolute path of the array being compared (either side), not a relative
  path and not the path of an element. When it does not match, the array falls back to
  index-based comparison.

- **Key paths written as absolute paths.**
  `keyPaths` are relative to an array element: use `"id"` or `"@.id"`, not `"$.first[0].id"`.

- **Expecting `compare` to modify source data.**
  `compare` is read-only on `firstPath`/`fromPath` and `secondPath`/`toPath`. It writes only
  to `resultPath`.

- **Comparing nodes that do not exist.**
  If either path resolves to no node, nothing is written and a warning is logged.

## Failure modes and what the trace tells you

- **Trace: `result = 'equal' | 'greater' | 'less' | 'different'`** — the scalar shape was
  used: default settings and two single primitive nodes.
- **Trace: `N result(s), differences: true|false`** — the structured shape was used.
  `differences: false` means every entry is informational.
- **No trace / result path unchanged** — one or both paths did not resolve to a node.
  Verify `firstPath` and `secondPath` (or their `fromPath`/`toPath` aliases) point to existing
  nodes in the current document.
- **Fewer entries than expected** — `settings.resultTypes` is filtering them out.
- **Array reported as fully different despite matching content** — `arrayPath` did not match
  the array's absolute path, so index-based comparison was used on a reordered array.
- **Unexpected `typeDifference` on XML** — an element with a single repeated child is modelled
  as an array; an element whose children have distinct names is modelled as an object.
