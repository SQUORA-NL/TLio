# decisionTable

> Matches input values against a rule table and writes output values. Supports
> `firstMatch`, `bestMatch`, and `allMatches` strategies with configurable conflict
> resolution.

## Syntax

```json
{
  "command": "decisionTable",
  "path": "$.record",
  "config": {
    "inputs":  [ { "name": "age",    "path": "$.age" } ],
    "outputs": [ { "name": "tier",   "path": "$.tier" } ],
    "rules": [
      { "priority": 1, "conditions": { "age": 18 }, "results": { "tier": "adult" } },
      { "priority": 2, "conditions": { "age": 65 }, "results": { "tier": "senior" } }
    ],
    "strategy": { "mode": "firstMatch", "conflictResolution": "priority" },
    "defaultResults": { "tier": "unknown" }
  }
}
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the node(s) the table is evaluated against. |
| config | object | yes | — | Decision table definition (see Config below). |

### Config object

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| inputs | array | yes | — | Array of `{name, path}` — named input columns and their paths. |
| outputs | array | yes | — | Array of `{name, path}` — named output columns and write paths. |
| rules | array | yes | — | Array of rule objects (see Rules below). |
| strategy | object | no | `firstMatch/priority` | Execution strategy (see Strategy below). |
| defaultResults | object | no | — | `{name: value}` map written when no rule matches. |

### Rule object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| priority | integer | no | Lower number = higher priority (used with `priority` conflict resolution). |
| conditions | object | yes | `{inputName: expectedValue}` — all conditions must match. |
| results | object | yes | `{outputName: value}` — values to write when rule matches. |

### Strategy object

| Field | Values | Default | Description |
|-------|--------|---------|-------------|
| mode | `"firstMatch"`, `"bestMatch"`, `"allMatches"` | `"firstMatch"` | How many matching rules to apply. |
| conflictResolution | `"priority"`, `"lastWins"`, `"merge"` | `"priority"` | How to resolve multiple matches. |

**Functions in the value**: ✅ rule result values only  
**Functions in the path**: ✅ via the commands the rules execute

## Verified example

```json
{
  "input": { "status": "active" },
  "script": [
    {
      "command": "decisionTable",
      "path": "$",
      "decisionTable": {
        "inputs": [{ "name": "status", "path": "$.status" }],
        "outputs": [{ "name": "label", "path": "$.label" }],
        "rules": [
          { "priority": 1, "conditions": { "status": "active" }, "results": { "label": "Active" } }
        ]
      }
    }
  ],
  "result": { "status": "active", "label": "Active" }
}
```

Verified by: `TLio.UnitTests/Fixtures/DecisionTable/01-decision-table-key/fixture.json`. Note
the script uses the `"decisionTable"` key rather than `"config"` — this fixture is also the live
proof that the JLio-compatibility alias works.

### bestMatch and allMatches, from the C# test suite

The single fixture file only exercises `firstMatch`. `bestMatch` and `allMatches` are covered by
`TLio.UnitTests/CommandsTests/DecisionTableAdvancedTests.cs`, built via the C# config object
rather than a JSON script; the equivalent JSON is reconstructed here (not copied verbatim from a
`.cs` file) but the input/rules/result values are the exact ones the test asserts.

`bestMatch` — the tied-conditions case, `::BestMatch_TiedConditions_LowerPriorityNumberWins`:

```json
{
  "input": { "x": "yes" },
  "script": [{
    "command": "decisionTable", "path": "$",
    "config": {
      "inputs":  [{ "name": "x", "path": "$.x" }],
      "outputs": [{ "name": "r", "path": "$.r" }],
      "rules": [
        { "priority": 5, "conditions": { "x": "=yes" }, "results": { "r": "low-prio" } },
        { "priority": 1, "conditions": { "x": "=yes" }, "results": { "r": "high-prio" } }
      ],
      "strategy": { "mode": "bestMatch" }
    }
  }],
  "result": { "x": "yes", "r": "high-prio" }
}
```

Both rules match with one condition each, so the tie is broken purely by priority — lower number
wins.

`allMatches` with `conflictResolution: "priority"` — `::AllMatches_Priority_LowestPriorityNumberWins`:

```json
{
  "input": { "v": 50 },
  "script": [{
    "command": "decisionTable", "path": "$",
    "config": {
      "inputs":  [{ "name": "v", "path": "$.v" }],
      "outputs": [{ "name": "out", "path": "$.out" }],
      "rules": [
        { "priority": 0, "conditions": { "v": ">=0" }, "results": { "out": "rule-0" } },
        { "priority": 1, "conditions": { "v": ">=0" }, "results": { "out": "rule-1" } }
      ],
      "strategy": { "mode": "allMatches", "conflictResolution": "priority" }
    }
  }],
  "result": { "v": 50, "out": "rule-0" }
}
```

Both rules match; `priority` conflict resolution picks rule-0's result over rule-1's because 0 is
the lower priority number, even though rule-1 is evaluated later.

## Notes

- The JSON key `"decisionTable"` is accepted as an alias for `"config"` (JLio compatibility, 008+).
- No C# fluent builder — construct the config object directly and serialize with `TLioConvert`.
- A nested function argument written as `@.field` (e.g. a result of `"=concat(@.a,'-',@.b)"`)
  resolves relative to the node currently being processed, the same as a bare `@.field` result —
  not relative to the document root. This holds for every function, not just ones written inside
  `decisionTable`; see [Notation Reference](../notation-reference.md).

## Performance

`ApplyResults` evaluates a rule's result value *before* it touches the output path — it resolves
the value first and only then calls `EnsurePath`/writes, instead of ensuring the path and writing
into it afterward. A result that fails to evaluate (its function returns no data) therefore skips
path construction entirely: no `{}` placeholder is ensured into the document just to sit there
unused. This also fixes an ordering bug under conflict resolution — with `allMatches` combined
with `priority` or `lastWins`, a later, lower-priority rule whose value fails to evaluate can no
longer clobber an earlier rule's already-written value at the same output path by ensuring an
empty container over it. The value itself does not depend on which parent it is written to, so
one evaluation is reused across every parent a wildcard output path selects, rather than
re-evaluating the value once per parent.

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## When to use

- Classification with 3 or more distinct outcomes (e.g., bronze/silver/gold tiers).
- Tiered rules where multiple input columns combine to determine an output.
- Overlapping rule sets where conflict resolution is needed (`bestMatch` picks the rule with the lowest priority number among all matches; `allMatches` applies every matching rule).
- Business rules that change frequently — adding a rule row is safer than rewriting nested `ifElse` chains.
- Rules are maintained by non-developers or sourced from external configuration.
- You need a `defaultResults` fallback when no rule matches.

## When NOT to use

- Simple two-outcome condition — use `ifElse` instead; it is cleaner and more readable.
- Producing a comparison label (`equal`/`greater`/`less`) — use `compare` for that.
- The condition is a single boolean flag — `ifElse` is the right tool.
- Rules have no structured conditions and just route based on a boolean — `ifElse` is cleaner.

## Comparison: IfElse vs DecisionTable vs Compare

| Criterion | ifElse | decisionTable | compare |
|-----------|--------|---------------|---------|
| Number of outcomes | 2 | 3+ (or 2 if rules evolve) | 1 label (equal/greater/less/different) |
| Condition complexity | Single expression | Multiple input columns, multi-condition rules | Fixed: left node vs right node |
| Data transformation | Yes — writes any value | Yes — writes rule result values | No — only writes a classification string |
| Rule maintenance | Rewrite JSON | Add/edit a rule row | N/A |
| Overlapping rules | Not supported | bestMatch / allMatches | N/A |
| Use to branch on comparison | Yes — after `compare` writes result | Rarely | No |

## Common mistakes

- **Result values wrapped in objects.**
  Wrong: `"results": { "tier": { "value": "gold" } }` — the object is written verbatim, not the string.
  Right: `"results": { "tier": "gold" }` — use plain strings or numbers.

- **Misunderstanding strategy modes.**
  - `firstMatch` — stops at the first rule that passes (rule order matters).
  - `bestMatch` — evaluates ALL rules, then picks the one with the lowest priority number.
  - `allMatches` — applies every matching rule in turn; later rules can overwrite earlier ones.
  Choosing the wrong mode leads to unexpected output when multiple rules match.

- **Priority numbering confusion.**
  Lower priority number = higher importance. Priority `1` beats priority `10`.
  With `bestMatch`, all rules are evaluated first — a rule at priority `5` will win over one at priority `10` even if the priority-`10` rule appears first in the array.

- **Missing `defaultResults`.**
  When no rule matches and `defaultResults` is absent, output paths are not written at all.
  Add `defaultResults` to guarantee a fallback value.

- **Input name mismatch.**
  `conditions` keys must exactly match the `name` fields in `inputs`. A typo silently causes a condition to never match.

- **Using `decisionTable` for a two-outcome boolean condition.**
  This works but is unnecessarily verbose. Use `ifElse` for two-outcome decisions.

## Failure modes and what the trace tells you

- **Trace: `"applied decision table to N node(s) at 'path'. rule[priority=X] → Y, Z set."`**
  Rule with priority X matched; output fields Y and Z were written to their configured paths.
  (`allMatches` reads `"N rules matched → Y, Z set."` instead; `bestMatch` reads
  `"best-match rule[priority=X] → Y, Z set."`)

- **Trace: `"applied decision table to N node(s) at 'path'. no rule matched → defaults applied (Y, Z set)."`**
  No rule matched; `defaultResults` values were written.

- **Trace: `"applied decision table to N node(s) at 'path'. no rule matched, no defaults."`**
  No rule matched and no `defaultResults` configured — output paths unchanged.
  Check that input paths resolve correctly and that condition values match the actual data types.

- **Expected rule did not match** — verify that `conditions` key names match the `inputs[].name` values exactly, and that value types agree (string `"18"` does not match number `18`).

- **Wrong rule selected with `bestMatch`** — all rules are evaluated; the one with the numerically lowest `priority` wins. Check that the intended rule has the lowest priority number.

- **Multiple rules overwriting each other with `allMatches`** — this is expected behavior; later-applied rules can overwrite earlier results. Use `firstMatch` or `bestMatch` if you want only one rule to apply.
