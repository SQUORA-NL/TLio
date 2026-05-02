# ifElse

> Evaluates a condition and executes one of two script branches. The condition can be a
> literal boolean, a value path, or a `=function()` expression that returns a truthy node.

## Syntax

```json
{
  "command": "ifElse",
  "condition": <TLioValue>,
  "ifScript":   [ <commands> ],
  "elseScript": [ <commands> ]
}
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| condition | TLioValue | yes | — | Evaluated for truthiness. Use `true`/`false` literals, a path, or `=function()`. |
| ifScript | array | yes | — | Script executed when condition is truthy. |
| elseScript | array | no | — | Script executed when condition is falsy. Omit to do nothing on false. |

**Supports functions**: ✅ (condition only)

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{
  "command": "ifElse",
  "condition": "=fetch($.user.active)",
  "ifScript":   [{ "command": "set", "path": "$.status", "value": "enabled" }],
  "elseScript": [{ "command": "set", "path": "$.status", "value": "disabled" }]
}
```

## C# Fluent API

```csharp
var condition = new FixedValue<JToken>(JValue.FromObject(true));
var script = new TLioScript<JToken>()
    .IfElse(condition)
    .If(new TLioScript<JToken>().Set(JValue.CreateString("enabled")).OnPath("$.status").Build())
    .Else(new TLioScript<JToken>().Set(JValue.CreateString("disabled")).OnPath("$.status").Build());
```

## When to use

- Simple yes/no decisions with exactly two outcomes (true branch and false branch).
- Threshold checks: `"=fetch($.age) >= 18"`, flag setting, simple routing.
- Condition is a single boolean expression, path value, or function returning truthy/falsy.
- You want to write different values to the same path depending on a runtime condition.
- Inline branching inside a larger script without needing a separate rule table.

## When NOT to use

- Three or more distinct outcomes — use `decisionTable` instead.
- Business rules that change frequently or are maintained by non-developers — use `decisionTable` (add a rule row vs rewrite nested ifs).
- Producing a comparison label (`equal`/`greater`/`less`/`different`) — use `compare` first, then branch on the result with `ifElse`.
- Multiple overlapping conditions that need conflict resolution — use `decisionTable` with `bestMatch`.

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

- **Condition is an object instead of a boolean.**
  Wrong: `"condition": { "value": true }` — this is always truthy (non-null object).
  Right: `"condition": true` or `"condition": "=isEmpty($.x)"`.

- **Using a function expression without the `=` prefix.**
  Wrong: `"condition": "isEmpty($.x)"` — treated as a literal string, always truthy.
  Right: `"condition": "=isEmpty($.x)"`.

- **thenValue / elseValue wrapped in objects.**
  Wrong: `"value": { "value": "enabled" }` inside `ifScript`.
  Right: `"value": "enabled"` — plain JSON primitive.

- **Expecting `elseScript` to be required.**
  Omitting `elseScript` is valid — nothing happens when the condition is falsy.

- **Truthy confusion.**
  Truthy values: any non-null node, `true`, non-zero number, non-empty string.
  Falsy values: `null`, `false`, `0`, `""` (empty string).
  An object `{}` or array `[]` is truthy even when empty.

- **Using `ifElse` for 3+ outcomes via nesting.**
  Nested `ifElse` chains are fragile and hard to maintain. Use `decisionTable` instead.

## Failure modes and what the trace tells you

- **Trace: `"applied then-branch to X"`** — condition was truthy; `ifScript` executed on node X.
- **Trace: `"applied else-branch to X"`** — condition was falsy; `elseScript` executed on node X.
- **No branch applied / no trace entry** — the path matched zero nodes; neither branch ran.
  Check that the `path` (or root node) is not empty and that the condition expression resolves correctly.
- **Condition always truthy despite expecting false** — check for an object condition instead of a scalar boolean; objects are always truthy.
- **Function condition not evaluated** — missing `=` prefix; the string is treated as a literal.
