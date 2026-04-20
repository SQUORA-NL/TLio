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

**Supports functions**: ✅ (rule result values only)

## Notes

- The JSON key `"decisionTable"` is accepted as an alias for `"config"` (JLio compatibility, 008+).
- No C# fluent builder — construct the config object directly and serialize with `TLioConvert`.

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).
